using BT; // v2-recompile
using UnityEngine;

/// <summary>
/// 敌人完整行为树（BT）控制器，用于替代 EnemyStateMachine（FSM）驱动敌人 AI。
///
/// FSM 相关代码完整保留在 Assets/Scripts/Enemy/EnemyState 下，不再默认启用；
/// Enemy 组件通过 useBehaviorTree 开关选择走 BT（默认）还是 FSM（回退）。
///
/// 树结构（Selector 按优先级执行，高优先级激活时会打断低优先级节点）：
///   Dead(死亡) > Spawn(出生) > Retreat(后撤) > Break(破防) > Hit(受击)
///     > Combat(战斗: Roar咆哮 / Bite撕咬 / Claw爪击)
///     > Chase(追击) > Alert(警戒) > Investigate(调查) > ReturnToLair(返回巢穴)
///     > Wander(待机-巡逻循环) > None(兜底/未初始化)
///   Wander = Sequence[ Idle(待机) , Patrol(巡逻) ]
///
/// 行为流转：
///   None -> Spawn -> Idle <-> Patrol
///   Idle/Patrol -> 发现玩家 -> Alert -> (锁定) Roar -> Chase -> Bite/Claw -> Chase ...
///   Alert 丢失目标 -> Investigate(前往最后目击点) -> ReturnToLair -> Idle/Patrol
///   Chase 超出追击距离(leash) -> ReturnToLair
///   任意状态受击 -> Hit / 破防 -> Break（结束后根据是否锁定继续追击）
///   任意状态血量归零 -> Dead（终态）
/// </summary>
public class EnemyBehaviorTree
{
    /// <summary>到达巡逻点后等待 Idle 动画收尾的超时兜底（秒）。</summary>
    public const float PatrolIdleReturnTimeout = 1f;
    /// <summary>攻击动画结束后的兜底宽限时间（秒）。</summary>
    public const float AttackAnimationGraceSeconds = 1.5f;

    private readonly Enemy enemy;
    private readonly Selector root;

    // ---- 共享上下文 ----

    /// <summary>Idle 动画退出标记：巡逻等待阶段消费。</summary>
    private bool idleAnimationExited;

    /// <summary>破防请求 / 激活标记。</summary>
    private bool breakRequested;
    private bool breakActive;

    /// <summary>后撤请求。</summary>
    private bool retreatRequested;
    private Transform retreatPlayer;
    private float retreatEndTime;

    /// <summary>出生是否已完成。</summary>
    private bool hasSpawned;

    /// <summary>受击请求（由 EnemyHeath 转发）。</summary>
    private bool hitRequested;

    /// <summary>警戒期间记录的最后目击玩家位置。</summary>
    private Vector3 lastSeenPlayerPos;

    /// <summary>调查请求 / 调查点。</summary>
    private bool investigateRequested;
    private Vector3 investigatePoint;

    /// <summary>战斗标记：咆哮请求、本回合是否已咆哮、撕咬/爪击冷却。</summary>
    private bool roarRequested;
    private bool hasRoaredInCombat;
    private float biteCooldownTimer;
    private float clawCooldownTimer;
    private float repositionCooldownTimer;

    /// <summary>返回巢穴请求。</summary>
    private bool returnToLairRequested;

    /// <summary>当前行为名（调试用，与 FSM 的 currentStateName 对齐）。</summary>
    public string CurrentBehavior { get; private set; } = nameof(EnemyIdleState);

    public EnemyBehaviorTree(Enemy enemyController)
    {
        enemy = enemyController;
        root = new Selector(
            new DeadNode(this),
            new SpawnNode(this),
            new RetreatNode(this),
            new BreakNode(this),
            new HitNode(this),
            new CombatNode(this),
            new ChaseNode(this),
            new AlertNode(this),
            new InvestigateNode(this),
            new ReturnToLairNode(this),
            new WanderNode(this),
            new NoneNode(this));
    }

    /// <summary>是否处于破防状态（伤害倍率等外部逻辑依赖此状态）。</summary>
    public bool IsBreakActive => breakActive;

    public void Update()
    {
        TickCooldowns();
        root.Tick();
    }

    /// <summary>玩家"一笔画"后撤指令。</summary>
    public void RequestRetreat(Transform player)
    {
        var data = enemy.Data;
        var duration = data != null ? data.retreatDuration : 0.35f;
        if (duration <= 0f)
        {
            return;
        }

        retreatPlayer = player;
        retreatEndTime = Time.time + Mathf.Max(0f, duration);
        retreatRequested = true;
    }

    /// <summary>破防指令（由 CharacterHealthBase 转发）。</summary>
    public void RequestBreak()
    {
        breakRequested = true;
    }

    /// <summary>受击指令（由 EnemyHeath 转发）。</summary>
    public void RequestHit(Transform attacker)
    {
        hitRequested = true;
        if (attacker != null)
        {
            enemy.BeginChaseLock(attacker);
        }
    }

    /// <summary>对外接口：让敌人前往指定位置调查（可被其他系统/测试调用）。</summary>
    public void RequestInvestigate(Vector3 point)
    {
        StartInvestigate(point);
    }

    /// <summary>警戒丢失目标后，前往指定位置调查。</summary>
    private void StartInvestigate(Vector3 point)
    {
        investigatePoint = point;
        investigateRequested = true;
    }

    /// <summary>动画退出事件（EnemyAnimatorTranslate -> Enemy.OnAnimationExitEvent 转发）。</summary>
    public void OnAnimationExitEvent(AnimationState state)
    {
        switch (state)
        {
            case AnimationState.Idle:
                idleAnimationExited = true;
                break;

            case AnimationState.Hit:
                // 受击状态中：Hit 动画退出即结束受击。
                if (hitRequested)
                {
                    hitRequested = false;
                    return;
                }

                // 破防期间的 Hit 动画退出由 Break 节点计时处理，不额外触发追击。
                if (breakActive)
                {
                    return;
                }

                var player = Object.FindObjectOfType<Player>();
                if (player == null)
                {
                    return;
                }

                enemy.BeginChaseLock(player.transform);
                break;
        }
    }

    private void SetBehavior(string name)
    {
        if (CurrentBehavior == name)
        {
            return;
        }

        CurrentBehavior = name;
        Debug.Log($"[EnemyBT] frame={Time.frameCount} time={Time.time:F2} {enemy.name} -> {name}", enemy);
    }

    private void TickCooldowns()
    {
        if (biteCooldownTimer > 0f)
        {
            biteCooldownTimer = Mathf.Max(0f, biteCooldownTimer - Time.deltaTime);
        }

        if (clawCooldownTimer > 0f)
        {
            clawCooldownTimer = Mathf.Max(0f, clawCooldownTimer - Time.deltaTime);
        }

        if (repositionCooldownTimer > 0f)
        {
            repositionCooldownTimer = Mathf.Max(0f, repositionCooldownTimer - Time.deltaTime);
        }
    }

    /// <summary>是否满足攻击条件：有锁定目标且处于攻击范围内，且技能不在冷却。</summary>
    private bool CanUseAttack(float range, float cooldownTimer)
    {
        // 攻击动画播放中不允许切换新攻击，避免动画中途被打断。
        if (enemy.IsAttackAnimationPlaying)
        {
            return false;
        }

        if (cooldownTimer > 0f)
        {
            return false;
        }

        if (!enemy.HasChaseLock || enemy.ChaseTarget == null)
        {
            return false;
        }

        var flatDelta = enemy.ChaseTarget.position - enemy.transform.position;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude <= range * range;
    }

    // ================= 组合节点 =================

    /// <summary>战斗组合：Roar(咆哮) > Bite(撕咬) > Claw(爪击)。</summary>
    private sealed class CombatNode : Selector
    {
        public CombatNode(EnemyBehaviorTree tree) : base(new RoarNode(tree), new RepositionNode(tree), new BiteNode(tree), new ClawNode(tree))
        {
        }
    }

    /// <summary>待机-巡逻循环：Idle(等待 patrolWaitTime) -> Patrol(走向随机巡逻点)。</summary>
    private sealed class WanderNode : Sequence
    {
        public WanderNode(EnemyBehaviorTree tree) : base(new IdleNode(tree), new PatrolNode(tree))
        {
        }
    }

    // ================= 行为节点 =================

    /// <summary>死亡（终态）：播放死亡动画，停止一切行为。</summary>
    private sealed class DeadNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;

        public DeadNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (!enemy.IsDead)
            {
                return;
            }

            tree.SetBehavior("EnemyDeadState");
            var data = enemy.Data;
            enemy.EndChaseLock();
            enemy.CrossFadeState(data.deadStateName, data.deadCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            enemy.SetBreak(false);
        }

        protected override NodeStatus OnExecute()
        {
            if (!enemy.IsDead)
            {
                return NodeStatus.Failure;
            }

            enemy.SetMovement(0f);
            return NodeStatus.Running;
        }
    }

    /// <summary>出生：播放出生动画，结束后进入待机。</summary>
    private sealed class SpawnNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private float spawnCountdown;

        public SpawnNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (tree.hasSpawned)
            {
                return;
            }

            tree.SetBehavior("EnemySpawnState");
            var data = enemy.Data;
            enemy.CrossFadeState(data.spawnStateName, data.spawnCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            spawnCountdown = data.spawnDuration;
        }

        protected override NodeStatus OnExecute()
        {
            if (tree.hasSpawned)
            {
                return NodeStatus.Failure;
            }

            spawnCountdown -= Time.deltaTime;
            if (spawnCountdown > 0f)
            {
                return NodeStatus.Running;
            }

            tree.hasSpawned = true;
            return NodeStatus.Success;
        }
    }

    /// <summary>破防：播放 Hit 动画、提高承伤倍率，持续 breakDuration 后追击玩家（无玩家则回待机）。</summary>
    private sealed class BreakNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private float breakCountdown;

        public BreakNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (!tree.breakRequested)
            {
                return;
            }

            tree.SetBehavior(nameof(EnemyBreakState));
            tree.breakActive = true;

            var data = enemy.Data;
            enemy.CrossFadeState(data.breakStateName, data.breakCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            enemy.SetDamageTakenMultiplier(data.breakDamageTakenMultiplier);
            enemy.SetBreak(true);
            breakCountdown = data.breakDuration;
        }

        protected override NodeStatus OnExecute()
        {
            if (!tree.breakRequested)
            {
                return NodeStatus.Failure;
            }

            // 破防阶段保持静止。
            enemy.SetMovement(0f);

            breakCountdown -= Time.deltaTime;
            if (breakCountdown > 0f)
            {
                return NodeStatus.Running;
            }

            tree.breakRequested = false;
            var player = Object.FindObjectOfType<Player>();
            if (player != null)
            {
                enemy.BeginChaseLock(player.transform);
            }

            return NodeStatus.Success;
        }

        protected override void OnExit()
        {
            if (!tree.breakActive)
            {
                return;
            }

            enemy.SetBreak(false);
            enemy.SetDamageTakenMultiplier(1f);
            tree.breakActive = false;
        }
    }

    /// <summary>后撤：背对玩家退后，达到停止距离或持续时间后结束（有锁定则回到追击）。</summary>
    private sealed class RetreatNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;

        public RetreatNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (!tree.retreatRequested)
            {
                return;
            }

            tree.SetBehavior(nameof(EnemyRetreatState));

            var data = enemy.Data;
            enemy.CrossFadeState(data != null ? data.retreatStateName : "Retreat", data != null ? data.moveStartCrossFade : 0.1f);
            enemy.SetOffset(true);
            enemy.SetLock(data != null ? data.chaseLockValue : 1f);
            enemy.SetMovement(data != null ? data.retreatMovementValue : 1f);
        }

        protected override NodeStatus OnExecute()
        {
            if (!tree.retreatRequested)
            {
                return NodeStatus.Failure;
            }

            var data = enemy.Data;

            var stopDistance = data != null ? data.retreatStopDistanceToPlayer : 0f;
            if (tree.retreatPlayer != null && stopDistance > 0f)
            {
                var a = enemy.transform.position;
                var b = tree.retreatPlayer.position;
                a.y = 0f;
                b.y = 0f;
                if ((a - b).sqrMagnitude >= stopDistance * stopDistance)
                {
                    FinishRetreat();
                    return NodeStatus.Success;
                }
            }

            if (Time.time >= tree.retreatEndTime)
            {
                FinishRetreat();
                return NodeStatus.Success;
            }

            enemy.SetOffset(true);
            enemy.SetLock(data != null ? data.chaseLockValue : 1f);
            enemy.SetMovement(data != null ? data.retreatMovementValue : 1f);

            var back = -enemy.transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude > 0.0001f)
            {
                back.Normalize();
                var speed = data != null ? data.retreatSpeed : 2.5f;
                enemy.MoveInDirection(back, speed);
            }

            return NodeStatus.Running;
        }

        private void FinishRetreat()
        {
            tree.retreatPlayer = null;
            tree.retreatRequested = false;
        }
    }

    /// <summary>受击：播放受击动画并停顿 hitDuration（或被 Hit 动画退出事件提前结束），随后继续追击/警戒。</summary>
    private sealed class HitNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private float hitCountdown;

        public HitNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (!tree.hitRequested)
            {
                return;
            }

            tree.SetBehavior("EnemyHitState");
            var data = enemy.Data;
            enemy.CrossFadeState(data.hitStateName, data.hitCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            hitCountdown = data.hitDuration;
        }

        protected override NodeStatus OnExecute()
        {
            if (!tree.hitRequested)
            {
                return NodeStatus.Failure;
            }

            enemy.SetMovement(0f);

            hitCountdown -= Time.deltaTime;
            if (hitCountdown <= 0f)
            {
                tree.hitRequested = false;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }
    }

    /// <summary>咆哮：首次发现玩家进入战斗时播放，作为 Aggro 表现（返回巢穴后重置）。</summary>
    private sealed class RoarNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private bool sawAttackState;
        private float roarCountdown;

        public RoarNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (!tree.roarRequested)
            {
                return;
            }

            tree.SetBehavior("EnemyRoarState");
            var data = enemy.Data;
            enemy.CrossFadeState(data.roarStateName, data.roarCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            FaceTargetDuringRoar();
            roarCountdown = data.roarDuration + AttackAnimationGraceSeconds;
            sawAttackState = false;
        }

        protected override NodeStatus OnExecute()
        {
            if (!tree.roarRequested)
            {
                return NodeStatus.Failure;
            }

            enemy.SetMovement(0f);
            FaceTargetDuringRoar();

            // 咆哮动画播放中：保持状态直到动画自然播完。
            if (enemy.IsAttackAnimationPlaying)
            {
                if (!sawAttackState)
                {
                    sawAttackState = true;
                    var stateLength = enemy.GetAnimator().GetCurrentAnimatorStateInfo(0).length;
                    roarCountdown = Mathf.Max(enemy.Data.roarDuration, stateLength + AttackAnimationGraceSeconds);
                }
            }

            roarCountdown -= Time.deltaTime;

            if (sawAttackState && !enemy.IsAttackAnimationPlaying)
            {
                tree.roarRequested = false;
                return NodeStatus.Success;
            }

            if (roarCountdown <= 0f)
            {
                tree.roarRequested = false;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private void FaceTargetDuringRoar()
        {
            if (enemy.ChaseTarget == null)
            {
                return;
            }

            var flatDir = enemy.ChaseTarget.position - enemy.transform.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                enemy.SnapRotateTowards(flatDir);
            }
        }
    }

    /// <summary>攻击前定位：过近则后撤拉开、过远则前进逼近，调整到理想攻击距离后再出手。</summary>
    private sealed class RepositionNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private bool repositioning;
        private bool retreating;
        private float repositionTimer;

        public RepositionNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (tree.repositionCooldownTimer > 0f)
            {
                return;
            }

            if (!enemy.HasChaseLock || enemy.ChaseTarget == null)
            {
                return;
            }

            // 攻击动画播放中不做定位，避免打断。
            if (enemy.IsAttackAnimationPlaying)
            {
                return;
            }

            var data = enemy.Data;
            var distance = FlatDistanceToTarget();
            if (distance > data.repositionMaxEngageDistance)
            {
                return; // 太远，交给追击（跑/走）接近
            }

            if (distance >= data.repositionRetreatThreshold && distance <= data.repositionAdvanceThreshold)
            {
                return; // 已在理想距离，直接攻击
            }

            repositioning = true;
            retreating = distance < data.repositionRetreatThreshold;
            repositionTimer = data.repositionDuration;

            tree.SetBehavior("EnemyRepositionState");

            enemy.CrossFadeState(retreating ? data.retreatStateName : data.moveStartStateName, data.moveStartCrossFade);
            enemy.SetOffset(true);
            enemy.SetLock(data.lockValueMove);
            enemy.SetMovement(data.patrolMovementValue);
        }

        protected override NodeStatus OnExecute()
        {
            if (!repositioning)
            {
                return NodeStatus.Failure;
            }

            var data = enemy.Data;
            if (!enemy.HasChaseLock || enemy.ChaseTarget == null)
            {
                repositioning = false;
                return NodeStatus.Failure;
            }

            enemy.SetOffset(true);
            enemy.SetLock(data.lockValueMove);
            enemy.SetMovement(data.patrolMovementValue);

            if (retreating)
            {
                enemy.RetreatFrom(enemy.ChaseTarget.position, data.repositionSpeed, data.combatRotationTime);
            }
            else
            {
                enemy.FaceTargetThenMove(enemy.ChaseTarget.position, data.repositionSpeed, data.combatRotationTime);
            }

            repositionTimer -= Time.deltaTime;
            var distance = FlatDistanceToTarget();
            var reached = retreating
                ? distance >= data.repositionRetreatThreshold
                : distance <= data.repositionAdvanceThreshold;

            if (repositionTimer <= 0f || reached)
            {
                repositioning = false;
                tree.repositionCooldownTimer = data.repositionCooldown;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private float FlatDistanceToTarget()
        {
            if (enemy.ChaseTarget == null)
            {
                return float.MaxValue;
            }

            var flat = enemy.ChaseTarget.position - enemy.transform.position;
            flat.y = 0f;
            return flat.magnitude;
        }
    }

    /// <summary>撕咬：玩家进入撕咬范围且冷却完毕时攻击。</summary>
    private sealed class BiteNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private bool attacking;
        private bool sawAttackState;
        private float biteCountdown;

        public BiteNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            attacking = tree.CanUseAttack(enemy.Data.biteRange, tree.biteCooldownTimer);
            if (!attacking)
            {
                return;
            }

            tree.SetBehavior("EnemyBiteState");
            var data = enemy.Data;
            enemy.CrossFadeState(data.biteStateName, data.biteCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            FaceTargetDuringAttack();
            biteCountdown = data.biteDuration + AttackAnimationGraceSeconds;
            sawAttackState = false;
        }

        protected override NodeStatus OnExecute()
        {
            if (!attacking)
            {
                return NodeStatus.Failure;
            }

            enemy.SetMovement(0f);
            FaceTargetDuringAttack();

            // 攻击动画播放中：保持攻击状态直到动画自然播完，避免中途被下一个攻击打断。
            if (enemy.IsAttackAnimationPlaying)
            {
                if (!sawAttackState)
                {
                    sawAttackState = true;
                    var stateLength = enemy.GetAnimator().GetCurrentAnimatorStateInfo(0).length;
                    biteCountdown = Mathf.Max(enemy.Data.biteDuration, stateLength + AttackAnimationGraceSeconds);
                }
            }

            biteCountdown -= Time.deltaTime;

            if (sawAttackState && !enemy.IsAttackAnimationPlaying)
            {
                // 动画已播完并离开攻击状态。
                tree.biteCooldownTimer = enemy.Data.biteCooldown;
                attacking = false;
                return NodeStatus.Success;
            }

            if (biteCountdown <= 0f)
            {
                // 兜底：动画状态异常时防止卡死。
                tree.biteCooldownTimer = enemy.Data.biteCooldown;
                attacking = false;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private void FaceTargetDuringAttack()
        {
            if (enemy.ChaseTarget == null)
            {
                return;
            }

            var flatDir = enemy.ChaseTarget.position - enemy.transform.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                enemy.SnapRotateTowards(flatDir);
            }
        }
    }

    /// <summary>爪击：玩家进入爪击范围且冷却完毕时攻击。</summary>
    private sealed class ClawNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private bool attacking;
        private bool sawAttackState;
        private float clawCountdown;

        public ClawNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            attacking = tree.CanUseAttack(enemy.Data.clawRange, tree.clawCooldownTimer);
            if (!attacking)
            {
                return;
            }

            tree.SetBehavior("EnemyClawState");
            var data = enemy.Data;
            enemy.CrossFadeState(data.clawStateName, data.clawCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            FaceTargetDuringAttack();
            clawCountdown = data.clawDuration + AttackAnimationGraceSeconds;
            sawAttackState = false;
        }

        protected override NodeStatus OnExecute()
        {
            if (!attacking)
            {
                return NodeStatus.Failure;
            }

            enemy.SetMovement(0f);
            FaceTargetDuringAttack();

            // 攻击动画播放中：保持攻击状态直到动画自然播完，避免中途被下一个攻击打断。
            if (enemy.IsAttackAnimationPlaying)
            {
                if (!sawAttackState)
                {
                    sawAttackState = true;
                    var stateLength = enemy.GetAnimator().GetCurrentAnimatorStateInfo(0).length;
                    clawCountdown = Mathf.Max(enemy.Data.clawDuration, stateLength + AttackAnimationGraceSeconds);
                }
            }

            clawCountdown -= Time.deltaTime;

            if (sawAttackState && !enemy.IsAttackAnimationPlaying)
            {
                // 动画已播完并离开攻击状态。
                tree.clawCooldownTimer = enemy.Data.clawCooldown;
                attacking = false;
                return NodeStatus.Success;
            }

            if (clawCountdown <= 0f)
            {
                // 兜底：动画状态异常时防止卡死。
                tree.clawCooldownTimer = enemy.Data.clawCooldown;
                attacking = false;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        private void FaceTargetDuringAttack()
        {
            if (enemy.ChaseTarget == null)
            {
                return;
            }

            var flatDir = enemy.ChaseTarget.position - enemy.transform.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                enemy.SnapRotateTowards(flatDir);
            }
        }
    }

    /// <summary>追击：锁定目标持续靠近；超出追击距离或失去锁定时放弃并返回巢穴。</summary>
    private sealed class ChaseNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private bool usingRunState;

        public ChaseNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            // 无锁定不激活追击：避免每帧重复播放 WalkStart 动画。
            if (!enemy.HasChaseLock || enemy.ChaseTarget == null)
            {
                return;
            }

            tree.SetBehavior(nameof(EnemyChaseState));

            enemy.SetMovement(0f);
            var data = enemy.Data;
            var flatDelta = enemy.ChaseTarget.position - enemy.transform.position;
            flatDelta.y = 0f;
            usingRunState = flatDelta.sqrMagnitude > data.chaseRunDistance * data.chaseRunDistance;
            enemy.CrossFadeState(usingRunState ? data.runStateName : data.moveStartStateName, data.moveStartCrossFade);
            enemy.SetOffset(true);
            enemy.SetLock(data.chaseLockValue);
            enemy.SetMovement(0f);
        }

        protected override NodeStatus OnExecute()
        {
            if (!enemy.HasChaseLock || enemy.ChaseTarget == null)
            {
                return NodeStatus.Failure;
            }

            var data = enemy.Data;

            // 超出追击距离（leash）：放弃锁定，返回巢穴。
            var flatDelta = enemy.ChaseTarget.position - enemy.transform.position;
            flatDelta.y = 0f;
            if (data.chaseGiveUpDistance > 0f && flatDelta.sqrMagnitude > data.chaseGiveUpDistance * data.chaseGiveUpDistance)
            {
                enemy.EndChaseLock();
                tree.returnToLairRequested = true;
                return NodeStatus.Failure;
            }

            var distance = Mathf.Sqrt(flatDelta.sqrMagnitude);
            var shouldRun = distance > data.chaseRunDistance;

            // 跑过去 / 走过去：远距离跑、近距离走。
            if (shouldRun && !usingRunState)
            {
                usingRunState = true;
                enemy.CrossFadeState(data.runStateName, data.moveStartCrossFade);
            }
            else if (!shouldRun && usingRunState)
            {
                usingRunState = false;
                enemy.CrossFadeState(data.moveStartStateName, data.moveStartCrossFade);
            }

            var speed = shouldRun ? data.chaseSpeed : data.chaseWalkSpeed;
            enemy.SetOffset(true);
            enemy.SetLock(data.chaseLockValue);
            enemy.FaceTargetThenMove(enemy.ChaseTarget.position, speed, data.combatRotationTime);
            enemy.SetMovement(0f);
            return NodeStatus.Running;
        }
    }

    /// <summary>警戒：面向目标累计可见时间，达到 alertToChaseDelay 后锁定（首次先咆哮）并追击；丢失目标则前往最后目击点调查。</summary>
    private sealed class AlertNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private bool active;
        private float visibleTime;
        private float lostTime;

        public AlertNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            visibleTime = 0f;
            lostTime = 0f;

            // 目标不可见时警戒不激活：立刻失败，避免每帧重复播放 Idle 动画。
            active = enemy.HasTargetInSight;
            if (!active)
            {
                return;
            }

            tree.SetBehavior(nameof(EnemyAlertState));

            var data = enemy.Data;
            enemy.CrossFadeState(data.idleStateName, data.idleCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
        }

        protected override NodeStatus OnExecute()
        {
            if (!active)
            {
                return NodeStatus.Failure;
            }

            if (enemy.HasTargetInSight)
            {
                // 目标可见：累计可见时间，并持续面向目标。
                lostTime = 0f;
                visibleTime += Time.deltaTime;

                if (enemy.Target != null)
                {
                    var lookDir = enemy.Target.position - enemy.transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        enemy.RotateTowards(lookDir);
                    }

                    tree.lastSeenPlayerPos = enemy.Target.position;
                }

                if (visibleTime >= enemy.Data.alertToChaseDelay)
                {
                    // 首次进入战斗先咆哮一次（返回巢穴后重置）。
                    if (!tree.hasRoaredInCombat)
                    {
                        tree.roarRequested = true;
                        tree.hasRoaredInCombat = true;
                    }

                    enemy.BeginChaseLock();
                    return NodeStatus.Success;
                }

                return NodeStatus.Running;
            }

            // 目标丢失：前往最后一次看到玩家的位置调查。
            visibleTime = 0f;
            lostTime += Time.deltaTime;
            if (lostTime >= enemy.Data.alertDuration)
            {
                tree.StartInvestigate(tree.lastSeenPlayerPos);
                return NodeStatus.Failure;
            }

            return NodeStatus.Running;
        }
    }

    /// <summary>调查：前往最后目击点，原地环顾 investigateDuration；无发现则返回巢穴。</summary>
    private sealed class InvestigateNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private bool movingToPoint;
        private float lookTimer;

        public InvestigateNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (!tree.investigateRequested)
            {
                return;
            }

            tree.SetBehavior("EnemyInvestigateState");
            var data = enemy.Data;
            enemy.CrossFadeState(data.moveStartStateName, data.moveStartCrossFade);
            enemy.SetOffset(true);
            enemy.SetLock(data.lockValueMove);
            enemy.SetMovement(data.patrolMovementValue);
            movingToPoint = true;
            lookTimer = 0f;
        }

        protected override NodeStatus OnExecute()
        {
            if (!tree.investigateRequested)
            {
                return NodeStatus.Failure;
            }

            var data = enemy.Data;

            if (movingToPoint)
            {
                enemy.SetOffset(true);
                enemy.SetLock(data.lockValueMove);
                enemy.SetMovement(data.patrolMovementValue);
                enemy.FaceTargetThenMove(tree.investigatePoint, data.investigateSpeed);

                if (enemy.HasReached(tree.investigatePoint, data.investigateArriveDistance))
                {
                    movingToPoint = false;
                    lookTimer = 0f;
                    enemy.CrossFadeState(data.idleStateName, data.idleCrossFade);
                    enemy.SetOffset(false);
                    enemy.SetLock(data.lockValueIdle);
                    enemy.SetMovement(0f);
                }

                return NodeStatus.Running;
            }

            // 环顾四周：原地查看一段时间，仍无发现则返回巢穴。
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);
            enemy.RotateTowards(tree.investigatePoint - enemy.transform.position);

            lookTimer += Time.deltaTime;
            if (lookTimer >= data.investigateDuration)
            {
                tree.investigateRequested = false;
                tree.returnToLairRequested = true;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }
    }

    /// <summary>返回巢穴：走回出生点（PatrolAreaCenter），到达后回到待机-巡逻循环。</summary>
    private sealed class ReturnToLairNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;

        public ReturnToLairNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            if (!tree.returnToLairRequested)
            {
                return;
            }

            tree.SetBehavior("EnemyReturnToLairState");
            tree.hasRoaredInCombat = false;

            enemy.EndChaseLock();
            var data = enemy.Data;
            enemy.CrossFadeState(data.moveStartStateName, data.moveStartCrossFade);
            enemy.SetOffset(true);
            enemy.SetLock(data.lockValueMove);
            enemy.SetMovement(data.patrolMovementValue);
        }

        protected override NodeStatus OnExecute()
        {
            if (!tree.returnToLairRequested)
            {
                return NodeStatus.Failure;
            }

            var data = enemy.Data;
            enemy.SetOffset(true);
            enemy.SetLock(data.lockValueMove);
            enemy.SetMovement(data.patrolMovementValue);
            enemy.FaceTargetThenMove(enemy.PatrolAreaCenter, data.lairReturnSpeed);

            if (enemy.HasReached(enemy.PatrolAreaCenter, data.lairArriveDistance))
            {
                tree.returnToLairRequested = false;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }
    }

    /// <summary>待机：播放 Idle 动画并等待 patrolWaitTime。</summary>
    private sealed class IdleNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private float waitCountdown;

        public IdleNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            tree.SetBehavior(nameof(EnemyIdleState));

            var data = enemy.Data;
            enemy.CrossFadeState(data.idleStateName, data.idleCrossFade);
            enemy.SetOffset(false);
            enemy.SetLock(data.lockValueIdle);
            enemy.SetMovement(0f);

            // 防止上一次巡逻遗留的动画事件污染本次待机等待。
            tree.idleAnimationExited = false;
            waitCountdown = data.patrolWaitTime;
        }

        protected override NodeStatus OnExecute()
        {
            waitCountdown -= Time.deltaTime;
            return waitCountdown > 0f ? NodeStatus.Running : NodeStatus.Success;
        }
    }

    /// <summary>巡逻：走向随机巡逻点；到达后等待 Idle 动画收尾再进入待机。</summary>
    private sealed class PatrolNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;
        private readonly Enemy enemy;
        private Vector3 patrolTarget;
        private bool waitingForIdleTranslate;
        private float idleReturnTimer;

        public PatrolNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
            enemy = tree.enemy;
        }

        protected override void OnEnter()
        {
            tree.SetBehavior(nameof(EnemyPatrolState));

            enemy.EndChaseLock();
            patrolTarget = enemy.GetRandomPatrolPoint();
            waitingForIdleTranslate = false;
            idleReturnTimer = 0f;
            tree.idleAnimationExited = false;

            var data = enemy.Data;
            enemy.CrossFadeState(data.moveStartStateName, data.moveStartCrossFade);
            enemy.SetOffset(true);
            enemy.SetLock(data.lockValueMove);
            enemy.SetMovement(data.patrolMovementValue);
        }

        protected override NodeStatus OnExecute()
        {
            if (waitingForIdleTranslate)
            {
                // 等待 Idle 动画收尾期间保持静止，避免继续朝巡逻点纠偏导致原地打转。
                enemy.SetOffset(false);
                enemy.SetLock(enemy.Data.lockValueIdle);
                enemy.SetMovement(0f);

                idleReturnTimer += Time.deltaTime;
                if (tree.idleAnimationExited || idleReturnTimer >= PatrolIdleReturnTimeout)
                {
                    return NodeStatus.Success;
                }

                return NodeStatus.Running;
            }

            enemy.SetOffset(true);
            enemy.SetLock(enemy.Data.lockValueMove);
            enemy.SetMovement(enemy.Data.patrolMovementValue);
            enemy.MoveRootMotionTowards(patrolTarget, enemy.Data.patrolSpeed);

            if (enemy.HasReached(patrolTarget))
            {
                waitingForIdleTranslate = true;
                idleReturnTimer = 0f;
            }

            return NodeStatus.Running;
        }
    }

    /// <summary>None（兜底/未初始化）：什么都不做，保持 Running，避免行为树空转。</summary>
    private sealed class NoneNode : BTNode
    {
        private readonly EnemyBehaviorTree tree;

        public NoneNode(EnemyBehaviorTree tree)
        {
            this.tree = tree;
        }

        protected override void OnEnter()
        {
            tree.SetBehavior("EnemyNoneState");
        }

        protected override NodeStatus OnExecute()
        {
            return NodeStatus.Running;
        }
    }
}
