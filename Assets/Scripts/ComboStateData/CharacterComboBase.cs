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

        public Player player { get; }


        public CharacterComboBase(Animator animator, Transform playerTransform,Transform cameraTransform , PlayerComboReusableData reusableData, PlayerComboSOData playerComboSOData,Player player)
        {
            //get访问器可以在构造函数里面赋值，而不能在其他地方赋值
            this.animator = animator;
            this.playerTransform = playerTransform;
            this.reusableData = reusableData;
            comboData = playerComboSOData;
            reusableData.cameraTransform = cameraTransform;
            this.player = player;

            if (comboData.heavyCombo != null)
            {
                comboData.heavyCombo.Init();
            }
            if (comboData.lightCombo != null)
            {
                //初始化其实就是缓存数据
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
        #region 一般攻击
        public virtual void LightComboInput()
        {
         
            if (comboData.lightCombo == null) { return; }
           
            if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo ==null)
            {
                reusableData.currentCombo = comboData.lightCombo;
                ReSetComboInfo();
            }
            //确保轻攻击是第一个轻攻击
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
            ReSetATKIndex(0);
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
            //播放语音
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
        

        #region 动画事件
        public void DisConnectCombo()//事件调用
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
            //SFX_PoolManager.MainInstance.TryGetSoundPool(reusableData.currentCombo.GetComboSoundStyle(reusableData.currentIndex.Value), playerTransform.position, Quaternion.identity);
        }

        #endregion


        //?????????????????
        public void ATK()
        {
            //AttackTrigger();

        }

        protected int UpdateExecuteIndex(ComboContainerData containerData)
        {
            return Random.Range(0, containerData.GetComboMaxCount());
        }
        
        #region 更新伤害点
        /// <summary>
        /// 重置伤害点的计数
        /// </summary>
        /// <param name="这个参数没意义"></param>
        public void ReSetATKIndex(int index)//???????????????????????????????????????????????????????A??????????????A???????????????0
        {
            reusableData.ATKIndex = 0;
        }
        public void UpdateATKIndex()
        {
            reusableData.ATKIndex++;
        }
        #endregion

        #region 传递伤害
        /*private void AttackTrigger()
        {
            if (animator.AnimationAtTag("ATK") )
            {
                UpdateATKIndex();
               // Debug.Log("???ATK????"+ reusableData.ATKIndex) ;
                 CameraHitFeel.MainInstance.CameraShake(reusableData.currentCombo.GetComboShakeForce(reusableData.currentIndex.Value,reusableData.ATKIndex));
                Debug.Log(reusableData.currentCombo);
                if (!AttackDetection(reusableData.currentCombo)) { return; }

                  GameEventsManager.MainInstance.CallEvent("???????",
                  reusableData.currentCombo.GetComboDamage(reusableData.currentIndex.Value),
                  reusableData.currentCombo.GetComboHitName(reusableData.currentIndex.Value),
                  reusableData.currentCombo.GetComboParryName(reusableData.currentIndex.Value),
                  playerTransform, GameBlackboard.MainInstance.GetEnemy(),
                  this);

                 CameraHitFeel.MainInstance.PF(reusableData.currentCombo.GetPauseFrameTime(reusableData.currentIndex.Value, reusableData.ATKIndex));
                //Debug.Log("QTE????????" + reusableData.canQTE);
                //Debug.Log("?????ATK???????"+reusableData.ATKIndex);
                //Debug.Log("????????ATK???????" + reusableData.currentCombo.GetComboATKCount(reusableData.currentIndex.Value));
                #region ????QTE
                if (reusableData.canQTE && reusableData.ATKIndex >= reusableData.currentCombo.GetComboATKCount(reusableData.currentIndex.Value))
                {
                    TriggerSwitchSkill();
                }
                #endregion

            }
            else if (animator.AnimationAtTag("Skill"))
            {
                UpdateATKIndex();

                if (!SkillDetection(reusableData.currentSkill)) { return; }
               
                GameEventsManager.MainInstance.CallEvent("???????", reusableData.currentSkill.comboDamage, reusableData.currentSkill.hitName, reusableData.currentSkill.parryName, playerTransform, GameBlackboard.MainInstance.GetEnemy(),this);

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

                #region ????QTE

                if (reusableData.canQTE && reusableData.ATKIndex >= reusableData.currentSkill.ATKCount)
                {
                    TriggerSwitchSkill();
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
            else//??????????????????????????????????
            {
                if (!AttackDetection(comboData.executeCombo)) { return; }
                GameEventsManager.MainInstance.CallEvent("???????", comboData.executeCombo.GetComboDamage(reusableData.executeIndex));
            }

        }*/
        #endregion

        public void UpdateAttackLookAtEnemy()
        {
            /*if (GameBlackboard.MainInstance.GetEnemy() == null) { return; }
            if ((animator.AnimationAtTag("ATK") && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.3f)|| animator.AnimationAtTag("Skill"))
            {
                if (DevelopmentToos.DistanceForTarget(playerTransform, GameBlackboard.MainInstance.GetEnemy()) > 6.5f) return;
                if (DevelopmentToos.DistanceForTarget(playerTransform, GameBlackboard.MainInstance.GetEnemy()) < 0.09f) return;
                playerTransform.LookAt(GameBlackboard.MainInstance.GetEnemy().position, 60);
            }*/

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

        #region 播放音效
        private void StartPlayWeapon()
        {
            PlayWeaponSound(reusableData.currentCombo.comboDates[reusableData.currentIndex.Value]);
        }
        protected void PlayCharacterVoice(ComboData comboData)
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.ComboVoice,comboData.comboName, playerTransform.position);
        }
        protected void PlayWeaponSound(ComboData comboData)
        {
            //SFX_PoolManager.MainInstance.TryGetSoundPool(SoundStyle.WeaponSound, comboData.comboName, playerTransform.position);
        }

        #endregion
    }
}
