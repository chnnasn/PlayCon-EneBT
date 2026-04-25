using Tools;
using UnityEngine;

namespace PlayerState
{
    public class CharacterComboBase
    {
        protected Animator animator{ get; }
        protected Transform playerTransform { get; }
        protected PlayerComboReusableData reusableData { get; }
        protected PlayerComboSOData comboData{ get; }
        protected EnemyDetectionData enemyDetectionData { get; }

        public Player player { get; }


        public CharacterComboBase(Animator animator, Transform playerTransform,Transform cameraTransform , PlayerComboReusableData reusableData, PlayerComboSOData playerComboSOData,Player player)
        {
            this.animator = animator;
            this.playerTransform = playerTransform;
            this.reusableData = reusableData;
            comboData = playerComboSOData;
            reusableData.cameraTransform = cameraTransform;
            this.player = player;
            enemyDetectionData = player.playerSO.ComboData.enemyDetectionData;

            if (comboData.heavyCombo != null)
            {
                comboData.heavyCombo.Init();
            }
            if (comboData.lightCombo != null)
            {
                comboData.lightCombo.Init();
            }
        }

        public void AddEventAction()
        {
            reusableData.currentIndex.OnValueChanged += ReSetATKIndex;
           
        }

        

        public void RemoveEventActon()
        {
            reusableData.currentIndex.OnValueChanged -= ReSetATKIndex;
        }

       

        public virtual bool CanBaseComboInput()
        {
            if (!reusableData.canInput) { return false; }
            if (animator.AnimationAtTag("Hit")) return false;
            if (animator.AnimationAtTag("Parry")) return false;
            if (animator.AnimationAtTag("Execute")) return false;
            if (animator.AnimationAtTag("Skill")) { return false; }
      
            return true;
        }
        protected virtual void UpdateComboInfo()
        {
            reusableData.comboIndex++;
            if (reusableData.comboIndex > reusableData.currentCombo.GetComboMaxCount() - 1)
            {
                reusableData.comboIndex = 0;
            }
        }
        #region ?????
        public virtual void LightComboInput()
        {
         
            if (comboData.lightCombo == null) { return; }
           
            if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo ==null)
            {
                reusableData.currentCombo = comboData.lightCombo;
                ReSetComboInfo();
            }

            reusableData.currentCombo.ResetComboDates();

            ExecuteBaseCombo();

        }
        public virtual void HeavyComboInput()
        {
            if (comboData.heavyCombo == null) { return; }
            if (reusableData.currentCombo != comboData.heavyCombo || reusableData.currentCombo == null)
            {
                reusableData.currentCombo = comboData.heavyCombo;

                ReSetComboInfo();
            }  

            ExecuteBaseCombo();

        }
        public virtual void NormalDodgeCombo()
        {
            if (comboData.lightCombo == null) { return; }
            if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo == null)
            {
                reusableData.currentCombo = comboData.lightCombo;

            }
            reusableData.currentCombo.SwitchDodgeATK();
            ReSetComboInfo();
            ReSetATKIndex();
            ExecuteBaseCombo();
        }

        protected virtual void ExecuteBaseCombo()
        {
            if (reusableData.currentCombo == null) { return; }
            reusableData.hasATKCommand = true;
            reusableData.canInput = false;

        }
        public virtual void UpdateComboAnimation()
        {
            if (!reusableData.canATK) { return; }
            if (!reusableData.hasATKCommand) { return; }

            reusableData.currentIndex.Value = reusableData.comboIndex;
            string comboName = reusableData.currentCombo.GetComboName(reusableData.currentIndex.Value);
            animator.CrossFade(comboName, 0.111f, 0);
            float transitionDuration = reusableData.currentIndex.Value == 0 ? 0.03f : 0.111f;
            animator.CrossFadeInFixedTime(comboName, transitionDuration, 0);
    
            PlayCharacterVoice(reusableData.currentCombo.comboDates[reusableData.currentIndex.Value]);
            StartPlayWeapon();

            UpdateComboInfo();
          

            reusableData.hasATKCommand = false;
            reusableData.canATK = false;
        }


        public virtual void ReSetComboInfo()
        {
            reusableData.comboIndex = 0;
            reusableData.canInput = true;
            reusableData.canLink = true;
            reusableData.canMoveInterrupt = false;
            reusableData.canATK = true;
        }
        #endregion
        

        #region ???????
        public void DisConnectCombo()
        {
            reusableData.canLink = false;
        }
        public void CanMoveInterrupt()
        {
            reusableData.canMoveInterrupt = true;
        }

        public void CanInput()
        {
            reusableData.canInput = true;
        }
        public void CanATK()
        {
            reusableData.canATK = true;
        }
        public void PlayComboFX()
        {
           
        }

        #endregion

        
        public void ATK()
        {
            OnAttackWindowTriggered();
            AttackTrigger();

        }
        protected virtual void OnAttackWindowTriggered()
        {
        }
        
        #region ??????
        protected bool AttackDetection(ComboContainerData comboContainerData)
        {

            if (player.CurrentEnemy == null) { return false; }
            if (MathToos.DistanceForTarget(player.CurrentEnemy.transform, playerTransform) > comboContainerData.
                    GetComboDistance(reusableData.currentIndex.Value)) { return false; }
            if (MathToos.GetAngleForTargetDirection(player.CurrentEnemy.transform, playerTransform) < 80) { return false; }
            return true;
        }
        
        #endregion 
        

        protected int UpdateExecuteIndex(ComboContainerData containerData)
        {
            return Random.Range(0, containerData.GetComboMaxCount());
        }
        
        #region ?????????
        
        //????api
        public void ReSetATKIndex(int index= 0)
        {
            reusableData.ATKIndex = 0;
        }
        public void UpdateATKIndex()
        {
            reusableData.ATKIndex++;
        }
        #endregion

        #region ´«µÝÉËº¦
        private void AttackTrigger()
        {
            if (animator.AnimationAtTag("ATK") )
            {
                UpdateATKIndex();

                 CameraHitFeel.MainInstance.CameraShake(reusableData.currentCombo.GetComboShakeForce(reusableData.currentIndex.Value,reusableData.ATKIndex));
                if (!AttackDetection(reusableData.currentCombo)) { return; }

                var hitEnemy = player.CurrentEnemy;
                GameEventsManager.MainInstance.CallEvent(HealthState.TriggerDamage.ToString(),
                reusableData.currentCombo.GetComboDamage(reusableData.currentIndex.Value),
                reusableData.currentCombo.GetComboHitName(reusableData.currentIndex.Value),
                reusableData.currentCombo.GetComboParryName(reusableData.currentIndex.Value),
                playerTransform, hitEnemy.transform,
                this);

                 CameraHitFeel.MainInstance.PF(reusableData.currentCombo.GetPauseFrameTime(reusableData.currentIndex.Value, reusableData.ATKIndex));


            }
            else if (animator.AnimationAtTag("Skill"))
            {
                UpdateATKIndex();
                
               
                if (player.CurrentEnemy == null) { return; }
                var skillEnemy = player.CurrentEnemy;
                GameEventsManager.MainInstance.CallEvent(HealthState.TriggerDamage.ToString(), reusableData.currentSkill.comboDamage, reusableData.currentSkill.hitName, reusableData.currentSkill.parryName, playerTransform, skillEnemy.transform,this);

                #region ???
                if (reusableData.currentSkill.pauseFrameTimeList!=null && reusableData.currentSkill.pauseFrameTimeList.Length > 0&& reusableData.ATKIndex <= reusableData.currentSkill.pauseFrameTimeList.Length)
                {       
                   CameraHitFeel.MainInstance.PF(reusableData.currentSkill.pauseFrameTimeList[reusableData.ATKIndex - 1]);
                }
                else
                {
                    CameraHitFeel.MainInstance.PF(reusableData.currentSkill.pauseFrameTime);
                }

                #endregion
                

                #region ????
                if (reusableData.currentSkill.shakeForce==null||reusableData.ATKIndex > reusableData.currentSkill.shakeForce.Length)//???????????????ATK??force???????????ATKindex????????force?????
                {
                    return;
                }
                CameraHitFeel.MainInstance.CameraShake(reusableData.currentSkill.shakeForce[reusableData.ATKIndex-1]);
                #endregion
            }
            else
            {
                if (!AttackDetection(comboData.executeCombo)) { return; }
                if (player.CurrentEnemy == null) { return; }
                var executeEnemy = player.CurrentEnemy;
                GameEventsManager.MainInstance.CallEvent(
                    HealthState.TriggerDamage.ToString(),
                    comboData.executeCombo.GetComboDamage(reusableData.executeIndex),
                    comboData.executeCombo.GetComboHitName(reusableData.executeIndex),
                    comboData.executeCombo.GetComboParryName(reusableData.executeIndex),
                    playerTransform,
                    executeEnemy.transform,
                    this);
            }

        }
        #endregion


        public void UpdateAttackLookAtEnemy()
        {
            if (player.CurrentEnemy == null) { return; }
            if ((animator.AnimationAtTag("ATK") && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.3f) || animator.AnimationAtTag("Skill"))
            {
                if (MathToos.DistanceForTarget(playerTransform, player.CurrentEnemy.transform) > 6.5f) return;
                if (MathToos.DistanceForTarget(playerTransform, player.CurrentEnemy.transform) < 0.09f) return;
                playerTransform.LookAt(player.CurrentEnemy.transform.position, 60);
            }

        }

        public void CheckMoveInterrupt()
        {
            if (reusableData.canMoveInterrupt == false) { return; }
            if (PlayerInputSystem.MainInstance.PlayerMove.sqrMagnitude != 0)
            {
                animator.CrossFadeInFixedTime("Locomotion", 0.155f, 0);
                reusableData.canMoveInterrupt = false;
            }
        }
        public void CheckCanLinkCombo()
        {
            if (!reusableData.canLink || PlayerInputSystem.MainInstance.Run)
            {
                ReSetComboInfo();
            }
        }

        #region ????????
        private void StartPlayWeapon()
        {
            PlayWeaponSound(reusableData.currentCombo.comboDates[reusableData.currentIndex.Value]);
        }
        protected void PlayCharacterVoice(ComboData comboData)
        {
            
        }
        protected void PlayWeaponSound(ComboData comboData)
        {
            
        }

        #endregion
    }
}
