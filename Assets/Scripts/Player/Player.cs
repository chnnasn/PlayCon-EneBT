using UnityEngine;
using System.Collections;
using PlayerState;
using UnityEngine.InputSystem;
using FSM;
using Tools;

    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class Player : ControllerBase
    {

        [SerializeField] public string currentMovementState;

        [SerializeField] public string currentComboState;

        [SerializeField] private Enemy currentEnemy;

        [SerializeField] public PlayerSO playerSO;

        [SerializeField] public PlayerCameraUtility playerCameraUtility;
        [SerializeField] private float startupCameraInputBlockSeconds = 0.15f;
        [Header("Puzzle Summon")]
        private PuzzleManager puzzleManager;

        public PlayerMovementStateMachine movementStateMachine { get;private set; }

        public PlayerComboStateMachine comboStateMachine { get;private set; }
        public new Transform camera { get; private set; }
        public Enemy CurrentEnemy => currentEnemy;
        
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
        }

        private void UnregisterPuzzleInputCallbacks()
        {
            if (PlayerInputSystem.MainInstance?.inputActions?.Player == null)
                return;

            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.started -= OnPuzzleStarted;
            PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.canceled -= OnPuzzleCanceled;
            PlayerInputSystem.MainInstance.inputActions.Player.MousePo.performed -= OnPuzzlePointerMoved;
        }

        private void OnPuzzleStarted(InputAction.CallbackContext context)
        {
            if (puzzleManager == null)
                return;
            
            puzzleManager.EnsurePuzzleSession();

            puzzleManager.SetPuzzleVisible(true);

            if (PlayerInputSystem.MainInstance.TryGetPointerPosition(out var beginPointerPosition))
                puzzleManager.BeginDraw(beginPointerPosition);
        }

        private void OnPuzzlePointerMoved(InputAction.CallbackContext context)
        {
            if (puzzleManager == null || !puzzleManager.IsPuzzleSessionActive)
                return;
            if (!PlayerInputSystem.MainInstance.inputActions.Player.Puzzle.IsPressed())
                return;

            puzzleManager.ContinueDraw(context.ReadValue<Vector2>());
        }

        private void OnPuzzleCanceled(InputAction.CallbackContext context)
        {

            puzzleManager.EndDraw();
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
            if (comboStateMachine == null || comboStateMachine.ATKIngState == null)
            {
                return;
            }

            comboStateMachine.ATKIngState.DrawEnemyDetectionGizmos();
        }
    }