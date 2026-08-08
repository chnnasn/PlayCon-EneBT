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
        public Enemy CurrentEnemy => currentEnemy;
        public bool IsManualEnemyLockActive => currentEnemy != null;
        private readonly Collider[] lockDetectionHits = new Collider[MaxLockDetectionHits];
        
        protected override void Awake()
        {
            base.Awake();

            camera=Camera.main.transform;
            movementStateMachine =new PlayerMovementStateMachine(this);
            comboStateMachine = new PlayerComboStateMachine(this);
            playerCameraUtility.Init();
            if (puzzleManager == null)
                puzzleManager = FindObjectOfType<PuzzleManager>();
        }

       

        protected override void Start()
        {
            base.Start();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            movementStateMachine.ChangeState(movementStateMachine.idlingState);
            comboStateMachine.ChangeState(comboStateMachine.NullState);
            StartCoroutine(BlockCameraInputOnStartup());
        }
       protected override void Update()
        {
            base.Update();

            movementStateMachine.HandInput();
            movementStateMachine.Update();
            comboStateMachine.Update();

        }
       

        #region 相关动画进入或退出触发的方法

        public void OnAnimationEnterEvent(AnimationState playerState)
        {
            switch (playerState)
            {
                case AnimationState.TurnBack:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.returnRunState);
                    break;
                case AnimationState.Dash:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.dashingState);
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.NullState);
                    break;
                case AnimationState.ATK:
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.ATKIngState);
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.playerMovementNullState);
                    break;
                case AnimationState.DashBack:
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.dashBackingState);
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.NullState);
                    break;
            }
            
        }
        public void OnAnimationExitEvent()
        {
            movementStateMachine.OnAnimationExitEvent();

            comboStateMachine.OnAnimationExitEvent();
        }
        #endregion

        #region 状态变更事件
        public void OnEnable()
        {
            //注册movement状态机中的状态变更事件
            movementStateMachine.currentState.OnValueChanged += MovementStateChanged;
            comboStateMachine.currentState.OnValueChanged += ComboStateChanged;
            RegisterPuzzleInputCallbacks();
        }
       

        public void OnDisable()
        {
            movementStateMachine.currentState.OnValueChanged -= MovementStateChanged;
            comboStateMachine.currentState.OnValueChanged -= ComboStateChanged;
            UnregisterPuzzleInputCallbacks();
            playerCameraUtility.SetInputEnabled(true);
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
            if (PlayerInputSystem.MainInstance?.inputActions?.Player == null)
                return;

            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.started += OnPuzzleStarted;
            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.canceled += OnPuzzleCanceled;
            PlayerInputSystem.MainInstance.inputActions.Player.MousePo.performed += OnPuzzlePointerMoved;
            RegisterPuzzleManagerCallbacks();
        }

        private void UnregisterPuzzleInputCallbacks()
        {
            if (PlayerInputSystem.MainInstance?.inputActions?.Player == null)
                return;

            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.started -= OnPuzzleStarted;
            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.canceled -= OnPuzzleCanceled;
            PlayerInputSystem.MainInstance.inputActions.Player.MousePo.performed -= OnPuzzlePointerMoved;
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
            playerCameraUtility?.SetInputEnabled(!isDrawing);
        }

        private void OnOneStrokeTimedOut()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            playerCameraUtility?.SetInputEnabled(true);
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
                if (candidateEnemy == null)
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
            if (puzzleManager == null || !puzzleManager.IsPuzzleSession)
                return;
            if (!PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.IsPressed())
                return;

            puzzleManager.ContinueDraw(context.ReadValue<Vector2>());
        }

        private void OnPuzzleCanceled(InputAction.CallbackContext context)
        {
            
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
            comboStateMachine.ATKIngState.EnablePreInput();
        }
        public void CancelAttackColdTime()
        { 
            comboStateMachine.ATKIngState.CancelAttackColdTime();
        }
      
        public void DisableLinkCombo()
        { 
            comboStateMachine.ATKIngState.DisableLinkCombo();
        }
        public void EnableMoveInterrupt()
        {
            comboStateMachine.ATKIngState.EnableMoveInterrupt();
        }
     
        public void ATK()
        {
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

        public void SetCurrentEnemy(Enemy enemyTarget)
        {
            currentEnemy = enemyTarget;
        }

        private void CancelManualEnemyLock()
        {
            SetCurrentEnemy(null);

            if (puzzleManager != null)
            {
                puzzleManager.CancelPuzzleSession(hideUi: true);
            }

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            playerCameraUtility?.SetInputEnabled(true);
        }

        private IEnumerator BlockCameraInputOnStartup()
        {
            playerCameraUtility.SetInputEnabled(false);
            if (startupCameraInputBlockSeconds > 0f)
            {
                yield return new WaitForSeconds(startupCameraInputBlockSeconds);
            }
            else
            {
                yield return null;
            }

            playerCameraUtility.SetInputEnabled(true);
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