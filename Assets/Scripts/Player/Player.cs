using UnityEngine;
using PlayerState;
using UnityEngine.InputSystem;
using FSM;
using Tools;

    [RequireComponent(typeof(Animator), typeof(CharacterController))]
    public class Player : ControllerBase
    {

        [SerializeField] public string currentMovementState;

        [SerializeField] public string currentComboState;

        [SerializeField] public Transform enemy;

        [SerializeField] public PlayerSO playerSO;

        [SerializeField] public PlayerCameraUtility playerCameraUtility;
        [Header("Puzzle Summon")]
        private PuzzleManager puzzleManager;

        public PlayerMovementStateMachine movementStateMachine { get;private set; }

        public PlayerComboStateMachine comboStateMachine { get;private set; }
        public new Transform camera { get; private set; }
        
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
            movementStateMachine.ChangeState(movementStateMachine.idlingState);
            comboStateMachine.ChangeState(comboStateMachine.NullState);
            
          
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

                case AnimationState.ATK:
                    comboStateMachine.OnAnimationTranslateEvent(comboStateMachine.ATKIngState);
                    movementStateMachine.OnAnimationTranslateEvent(movementStateMachine.playerMovementNullState);
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
            //VFX_PoolManager.MainInstance.TryGetVFX(characterName, name);
        }

        public void PlayFootSound()
        {
            //Debug.Log("播放脚步声");
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.FOOT,transform.position,Quaternion.identity);
        }
        public void PlayFootBackSound()
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.FOOTBACK,transform.position,Quaternion.identity);
        }

        public void PlayWeaponBackSound()
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.WeaponBack, characterName.ToString(), transform.position);
        }

        public void PlayWeaponEndSound()
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.WeaponEnd, characterName.ToString(), transform.position);
        }

        #endregion
        public void PlayDodgeSound()
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.DodgeSound, transform.position, Quaternion.identity);
        }
        public void PlaySwitchWindSound()
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.SwitchInWindSound, transform.position, Quaternion.identity);
        }
        public void PlaySwitchInVoice()
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.SwitchInVoice, characterName.ToString(), transform.position);
        }    
    }