using UnityEngine;
using System.Collections;
using PlayerState;
using UnityEngine.InputSystem;
using FSM;
using Tools;

    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class Player : ControllerBase
    {
        private const int MaxLockDetectionHits = 32;

        [SerializeField] public string currentMovementState;

        [SerializeField] public string currentComboState;

        [SerializeField] private Enemy currentEnemy;

        [SerializeField] public PlayerSO playerSO;
        [Header("Lock Detection")]
        [SerializeField] private float lockDetectionDistance = 8f;
        [SerializeField] private LayerMask lockEnemyMask;

        [SerializeField] public PlayerCameraUtility playerCameraUtility;
        [SerializeField] private float startupCameraInputBlockSeconds = 0.15f;
        [Header("Puzzle Summon")]
        private PuzzleManager puzzleManager;

        public PlayerMovementStateMachine movementStateMachine { get;private set; }

        public PlayerComboStateMachine comboStateMachine { get;private set; }
        public new Transform camera { get; private set; }
        public Enemy CurrentEnemy => IsValidEnemy(currentEnemy) ? currentEnemy : null;
        public bool IsManualEnemyLockActive => manualEnemyLock && CurrentEnemy != null;
        public bool IsDead { get; private set; }
        public bool CanAcceptInput => hasStarted && isActiveAndEnabled && !IsDead;
        protected override bool CanMove => CanAcceptInput;
        private readonly Collider[] lockDetectionHits = new Collider[MaxLockDetectionHits];
        private readonly RaycastHit[] visibilityHits = new RaycastHit[32];
        private bool hasStarted;
        private bool manualEnemyLock;
        private float automaticEnemyLockExpireTime;
        private bool puzzleCallbacksRegistered;
        public int AnimationSession { get; private set; }

        public readonly struct AnimationStateBinding
        {
            public readonly int Session;
            public readonly int MovementRevision;
            public readonly int ComboRevision;
            public AnimationStateBinding(int session, int movementRevision, int comboRevision)
            {
                Session = session;
                MovementRevision = movementRevision;
                ComboRevision = comboRevision;
            }
        }

        public AnimationStateBinding CaptureAnimationBinding()
        {
            return new AnimationStateBinding(AnimationSession, movementStateMachine.Revision, comboStateMachine.Revision);
        }
        
        protected override void Awake()
        {
            base.Awake();

            if (playerSO == null || playerSO.movementData == null || playerSO.ComboData?.comboData == null)
            {
                Debug.LogError("Player requires movement and combo data in PlayerSO.", this);
                enabled = false;
                return;
            }

            camera = Camera.main != null ? Camera.main.transform : null;
            movementStateMachine =new PlayerMovementStateMachine(this);
            comboStateMachine = new PlayerComboStateMachine(this);
            playerCameraUtility?.Init();
            if (puzzleManager == null)
                puzzleManager = FindObjectOfType<PuzzleManager>();
        }

       

        protected override void Start()
        {
            base.Start();
            hasStarted = true;
            if (!IsDead) StartPlayerControl();
        }
       protected override void Update()
        {
            if (!CanAcceptInput) return;
            base.Update();
            if ((currentEnemy != null || manualEnemyLock) && (!IsValidEnemy(currentEnemy) ||
                (!manualEnemyLock && Time.time >= automaticEnemyLockExpireTime)))
            {
                CancelManualEnemyLock();
            }

            movementStateMachine.HandInput();
            movementStateMachine.Update();
            comboStateMachine.Update();

        }
       

        #region 相关动画进入或退出触发的方法

        public void OnAnimationEnterEvent(AnimationState playerState)
        {
            if (!CanAcceptInput) return;
            switch (playerState)
            {
                case AnimationState.TurnBack:
                    EnterAnimationState(movementStateMachine, movementStateMachine.returnRunState);
                    break;
                case AnimationState.Dash:
                    EnterAnimationState(movementStateMachine, movementStateMachine.dashingState);
                    EnterAnimationState(comboStateMachine, comboStateMachine.NullState);
                    break;
                case AnimationState.ATK:
                    EnterAnimationState(comboStateMachine, comboStateMachine.ATKIngState);
                    EnterAnimationState(movementStateMachine, movementStateMachine.playerMovementNullState);
                    break;
                case AnimationState.DashBack:
                    EnterAnimationState(movementStateMachine, movementStateMachine.dashBackingState);
                    EnterAnimationState(comboStateMachine, comboStateMachine.NullState);
                    break;
            }
            
        }
        private static void EnterAnimationState(StateMachine machine, IState state)
        {
            if (!machine.IsRunning) return;
            if (ReferenceEquals(machine.currentState.Value, state)) machine.Start(state);
            else machine.OnAnimationTranslateEvent(state);
        }

        public void OnAnimationExitEvent(AnimationStateBinding source)
        {
            if (!CanAcceptInput || source.Session != AnimationSession) return;
            if (source.MovementRevision == movementStateMachine.Revision)
                movementStateMachine.OnAnimationExitEvent();
            if (source.ComboRevision == comboStateMachine.Revision)
                comboStateMachine.OnAnimationExitEvent();
        }
        #endregion

        #region 状态变更事件
        public void OnEnable()
        {
            if (movementStateMachine == null || comboStateMachine == null) return;
            //注册movement状态机中的状态变更事件
            movementStateMachine.currentState.OnValueChanged += MovementStateChanged;
            comboStateMachine.currentState.OnValueChanged += ComboStateChanged;
            if (hasStarted && !IsDead) StartPlayerControl();
        }
       

        public void OnDisable()
        {
            StopPlayerControl();
            if (movementStateMachine != null) movementStateMachine.currentState.OnValueChanged -= MovementStateChanged;
            if (comboStateMachine != null) comboStateMachine.currentState.OnValueChanged -= ComboStateChanged;
        }

        public void OnDeath()
        {
            if (IsDead) return;
            IsDead = true;
            StopPlayerControl();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnRevived()
        {
            IsDead = false;
            if (hasStarted && isActiveAndEnabled) StartPlayerControl();
        }

        private void StartPlayerControl()
        {
            if (!CanAcceptInput) return;
            StopPlayerControl();
            verticalSpeed = 0f;
            fallOutdeltaTimer = fallOutTimer;
            movementStateMachine.reusableDate.shouldWalk = false;
            movementStateMachine.reusableDate.dashCooldownEndTime = 0f;
            comboStateMachine.ReusableData.hasATKCommand = false;
            characterAnimator.Rebind();
            characterAnimator.Update(0f);
            movementStateMachine.Start(movementStateMachine.idlingState);
            comboStateMachine.Start(comboStateMachine.NullState);
            MovementStateChanged(movementStateMachine.currentState.Value);
            ComboStateChanged(comboStateMachine.currentState.Value);
            RegisterPuzzleInputCallbacks();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            StartCoroutine(BlockCameraInputOnStartup());
        }

        private void StopPlayerControl()
        {
            AnimationSession++;
            StopAllCoroutines();
            movementStateMachine?.Stop();
            comboStateMachine?.Stop();
            UnregisterPuzzleInputCallbacks();
            CancelManualEnemyLock();
            playerCameraUtility?.DisableRecentering();
            playerCameraUtility?.SetInputEnabled(false);
            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(AnimID.MovementID, 0f);
                characterAnimator.SetBool(AnimID.HasInputID, false);
            }
            currentMovementState = IsDead ? "Dead" : "Disabled";
            currentComboState = IsDead ? "Dead" : "Disabled";
        }
    

        public void MovementStateChanged(IState currentState)
        {
            currentMovementState= currentState.GetType().Name;
        }
        private void ComboStateChanged(IState state)
        {
           currentComboState= state.GetType().Name;
        }
        #endregion

        private void RegisterPuzzleInputCallbacks()
        {
            if (puzzleCallbacksRegistered) return;
            if (PlayerInputSystem.MainInstance?.inputActions?.Player == null)
                return;

            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.started += OnPuzzleStarted;
            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.canceled += OnPuzzleCanceled;
            PlayerInputSystem.MainInstance.inputActions.Player.MousePo.performed += OnPuzzlePointerMoved;
            puzzleCallbacksRegistered = true;
            RegisterPuzzleManagerCallbacks();
        }

        private void UnregisterPuzzleInputCallbacks()
        {
            if (puzzleCallbacksRegistered && PlayerInputSystem.TryGetExistingInstance(out var inputSystem) && inputSystem.inputActions != null)
            {
                inputSystem.inputActions.Player.Puzzle.started -= OnPuzzleStarted;
                inputSystem.inputActions.Player.Puzzle.canceled -= OnPuzzleCanceled;
                inputSystem.inputActions.Player.MousePo.performed -= OnPuzzlePointerMoved;
            }
            puzzleCallbacksRegistered = false;
            UnregisterPuzzleManagerCallbacks();
        }

        private void RegisterPuzzleManagerCallbacks()
        {
            if (puzzleManager == null)
                puzzleManager = FindObjectOfType<PuzzleManager>();
            if (puzzleManager == null)
                return;

            puzzleManager.OneDrawingChanged -= OnOneStrokeDrawingStateChanged;
            puzzleManager.OneStrokeTimedOut -= OnOneStrokeTimedOut;
            puzzleManager.OneDrawingChanged += OnOneStrokeDrawingStateChanged;
            puzzleManager.OneStrokeTimedOut += OnOneStrokeTimedOut;
        }

        private void UnregisterPuzzleManagerCallbacks()
        {
            if (puzzleManager == null)
                return;

            puzzleManager.OneDrawingChanged -= OnOneStrokeDrawingStateChanged;
            puzzleManager.OneStrokeTimedOut -= OnOneStrokeTimedOut;
        }

        private void OnOneStrokeDrawingStateChanged(bool isDrawing)
        {
            playerCameraUtility?.SetInputEnabled(CanAcceptInput && !isDrawing);
        }

        private void OnOneStrokeTimedOut()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            playerCameraUtility?.SetInputEnabled(CanAcceptInput);
        }

        private bool TryDetectEnemyFromForward(out Enemy enemyTarget)
        {
            enemyTarget = null;
            var enemyMask = GetLockEnemyMask();
            var effectiveMask = enemyMask.value == 0 ? Physics.AllLayers : enemyMask.value;

            var sourceCamera = camera != null ? camera : Camera.main != null ? Camera.main.transform : null;
            if (sourceCamera == null)
            {
                return false;
            }
            var runtimeCamera = sourceCamera.GetComponent<Camera>() ?? Camera.main;
            if (runtimeCamera == null)
            {
                return false;
            }
            if (playerSO == null || playerSO.ComboData?.enemyDetectionData == null)
            {
                return false;
            }
            var detectionData = playerSO.ComboData.enemyDetectionData;

            var detectionOrigin = sourceCamera.position;
            var detectionDir = sourceCamera.forward;
            if (detectionDir.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            detectionDir.Normalize();
            var detectionDistance = Mathf.Max(0.01f, lockDetectionDistance);
            var detectionRadius = Mathf.Max(0.01f, detectionData.detectionRadius);
            var overlapCount = Physics.OverlapSphereNonAlloc(
                detectionOrigin + detectionDir * detectionDistance * 0.5f,
                detectionDistance * 0.5f + detectionRadius,
                lockDetectionHits,
                effectiveMask,
                QueryTriggerInteraction.Ignore);

            Enemy bestEnemy = null;
            var bestDepth = float.MaxValue;
            for (var i = 0; i < overlapCount; i++)
            {
                var hitCollider = lockDetectionHits[i];
                if (hitCollider == null)
                {
                    continue;
                }

                var candidateEnemy = hitCollider.GetComponentInParent<Enemy>();
                if (!IsValidEnemy(candidateEnemy) || !HasClearPathToEnemy(candidateEnemy, sourceCamera.position))
                {
                    continue;
                }

                var candidatePos = candidateEnemy.transform.position + Vector3.up * 1f;
                var viewport = runtimeCamera.WorldToViewportPoint(candidatePos);

                if (viewport.z <= 0f)
                {
                    continue;
                }

                if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                {
                    continue;
                }

                if (viewport.z >= bestDepth)
                {
                    continue;
                }

                bestDepth = viewport.z;
                bestEnemy = candidateEnemy;
            }

            for (var i = 0; i < overlapCount; i++)
            {
                lockDetectionHits[i] = null;
            }

            enemyTarget = bestEnemy;
            return enemyTarget != null;
        }

        private void OnPuzzleStarted(InputAction.CallbackContext context)
        {
            if (!CanAcceptInput) return;
            if (puzzleManager == null)
                return;
            if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
                return;

            if (!TryDetectEnemyFromForward(out var enemyTarget))
            {
                SetCurrentEnemy(null);
                puzzleManager.SetPuzzleVisible(false);
                return;
            }

            SetCurrentEnemy(enemyTarget);
            NotifyCurrentLockedEnemyOneStrokeCommand();
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            puzzleManager.EnsurePuzzleSession();

            puzzleManager.SetPuzzleVisible(true);

            if (PlayerInputSystem.MainInstance.TryGetPointerPosition(out var beginPointerPosition))
                puzzleManager.BeginDraw(beginPointerPosition);
        }

        private bool CanShowPuzzleByFrontArea()
        {
            return currentEnemy != null;
        }

        private void NotifyCurrentLockedEnemyOneStrokeCommand()
        {
            if (!CanShowPuzzleByFrontArea())
            {
                return;
            }

            if (GameEventsManager.MainInstance == null)
            {
                return;
            }

            GameEventsManager.MainInstance.CallEvent(Enemy.PlayerOneStrokeCommandEvent, currentEnemy.transform);
        }

        private void OnPuzzlePointerMoved(InputAction.CallbackContext context)
        {
            if (!CanAcceptInput) return;
            if (puzzleManager == null || !puzzleManager.IsPuzzleSession)
                return;
            if (!PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.IsPressed())
                return;

            puzzleManager.ContinueDraw(context.ReadValue<Vector2>());
        }

        private void OnPuzzleCanceled(InputAction.CallbackContext context)
        {
            if (!CanAcceptInput || puzzleManager == null) return;
            
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
            var solved = puzzleManager.EndDraw();
            if (solved && currentEnemy != null)
            {
                GameEventsManager.MainInstance.CallEvent(HealthState.BrokenDefense.ToString(), currentEnemy.transform);
            }

            puzzleManager.SetPuzzleVisible(false);
        }

        #region 连招动画事件
        public void EnablePreInput()
        {
            if (!CanAcceptInput) return;
            comboStateMachine.ATKIngState.EnablePreInput();
        }
        public void CancelAttackColdTime()
        { 
            if (!CanAcceptInput) return;
            comboStateMachine.ATKIngState.CancelAttackColdTime();
        }
      
        public void DisableLinkCombo()
        { 
            if (!CanAcceptInput) return;
            comboStateMachine.ATKIngState.DisableLinkCombo();
        }
        public void EnableMoveInterrupt()
        {
            if (!CanAcceptInput) return;
            comboStateMachine.ATKIngState.EnableMoveInterrupt();
        }
     
        public void ATK(AnimationEvent animationEvent = null)
        {
            if (!CanAcceptInput || !comboStateMachine.IsRunning) return;
            var state = comboStateMachine.currentState.Value;
            if (ReferenceEquals(state, comboStateMachine.ATKIngState))
            {
                var data = comboStateMachine.ReusableData;
                if (data.currentCombo == null) return;
                if (animationEvent != null && animationEvent.isFiredByAnimator &&
                    animationEvent.animatorStateInfo.shortNameHash != Animator.StringToHash(data.currentCombo.GetComboName(data.currentIndex.Value))) return;
            }
            else if (ReferenceEquals(state, comboStateMachine.SkillState))
            {
                if (animationEvent != null && animationEvent.isFiredByAnimator && !animationEvent.animatorStateInfo.IsTag("Skill")) return;
            }
            else return;
            comboStateMachine.ATKIngState.ATK();
        }

        #endregion

        #region 动画事件音效

        public void PlayVFX(string name)
        {
            
        }

        public void PlayFootSound()
        {
           
        }
        public void PlayFootBackSound()
        {
           
        }

        public void PlayWeaponBackSound()
        {
            
        }

        public void PlayWeaponEndSound()
        {
            
        }

        #endregion
        public void PlayDodgeSound()
        {
            
        }

        public void SetCurrentEnemy(Enemy enemyTarget, bool manualLock = true)
        {
            currentEnemy = IsValidEnemy(enemyTarget) ? enemyTarget : null;
            manualEnemyLock = currentEnemy != null && manualLock;
            automaticEnemyLockExpireTime = currentEnemy != null && !manualEnemyLock ? Time.time + 0.35f : 0f;
        }

        public static bool IsValidEnemy(Enemy enemyTarget)
        {
            return enemyTarget != null && enemyTarget.isActiveAndEnabled && !enemyTarget.IsDead;
        }

        public bool HasClearPathToEnemy(Enemy enemyTarget)
        {
            return HasClearPathToEnemy(enemyTarget, transform.position + Vector3.up * 0.7f);
        }

        private bool HasClearPathToEnemy(Enemy enemyTarget, Vector3 origin)
        {
            if (!IsValidEnemy(enemyTarget)) return false;
            var direction = enemyTarget.transform.position + Vector3.up * 0.7f - origin;
            var distance = direction.magnitude;
            if (distance <= 0.001f) return true;
            var hitCount = Physics.RaycastNonAlloc(origin, direction / distance, visibilityHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hitCount == visibilityHits.Length) return false;
            for (var i = 0; i < hitCount; i++)
            {
                var hitTransform = visibilityHits[i].transform;
                if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(enemyTarget.transform)) continue;
                return false;
            }
            return true;
        }

        private void CancelManualEnemyLock()
        {
            SetCurrentEnemy(null);

            if (puzzleManager != null)
            {
                puzzleManager.CancelPuzzleSession(hideUi: true);
            }

            Cursor.visible = IsDead;
            Cursor.lockState = IsDead ? CursorLockMode.None : CursorLockMode.Locked;
            playerCameraUtility?.SetInputEnabled(CanAcceptInput);
        }

        private IEnumerator BlockCameraInputOnStartup()
        {
            playerCameraUtility?.SetInputEnabled(false);
            if (startupCameraInputBlockSeconds > 0f)
            {
                yield return new WaitForSeconds(startupCameraInputBlockSeconds);
            }
            else
            {
                yield return null;
            }

            playerCameraUtility?.SetInputEnabled(CanAcceptInput && (puzzleManager == null || !puzzleManager.IsPuzzleSession));
        }

        private void OnDrawGizmosSelected()
        {
            DrawLockDetectionGizmos();

            if (comboStateMachine == null || comboStateMachine.ATKIngState == null)
            {
                return;
            }

            comboStateMachine.ATKIngState.DrawEnemyDetectionGizmos();
        }

        private void DrawLockDetectionGizmos()
        {
            var sourceCamera = camera != null ? camera : Camera.main != null ? Camera.main.transform : null;
            if (sourceCamera == null)
            {
                return;
            }

            var runtimeCamera = sourceCamera.GetComponent<Camera>() ?? Camera.main;
            if (runtimeCamera == null)
            {
                return;
            }

            if (playerSO == null || playerSO.ComboData?.enemyDetectionData == null)
            {
                return;
            }

            var detectionData = playerSO.ComboData.enemyDetectionData;
            var direction = sourceCamera.forward;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction.Normalize();
            var distance = Mathf.Max(0.01f, lockDetectionDistance);
            var radius = Mathf.Max(0.01f, detectionData.detectionRadius);
            var origin = sourceCamera.position;
            var endPoint = origin + direction * distance;
            var overlapCenter = origin + direction * (distance * 0.5f);
            var overlapRadius = distance * 0.5f + radius;

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawLine(origin, endPoint);
            Gizmos.DrawWireSphere(endPoint, radius);

            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.55f);
            var cachedMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(origin, sourceCamera.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, runtimeCamera.fieldOfView, distance, 0.1f, runtimeCamera.aspect);
            Gizmos.matrix = cachedMatrix;

            Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.45f);
            Gizmos.DrawWireSphere(overlapCenter, overlapRadius);
        }

        private LayerMask GetLockEnemyMask()
        {
            if (lockEnemyMask.value != 0)
            {
                return lockEnemyMask;
            }

            if (playerSO != null && playerSO.ComboData?.enemyDetectionData != null)
            {
                return playerSO.ComboData.enemyDetectionData.WhatIsEnemy;
            }

            return default;
        }
    }
