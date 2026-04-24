using Tools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerState
{
    public class CharacterCombo : CharacterComboBase
    {

        public CharacterCombo(Animator animator, Transform playerTransform,Transform cameraTransform, PlayerComboReusableData reusableData, PlayerComboSOData playerComboSOData, Player player) : base(animator, playerTransform, cameraTransform, reusableData, playerComboSOData , player )
        {
           
        }

        #region 闪A处理
       

        public  void DodgeComboInput()
        {
            
        }

        #endregion
        

        #region 技能处理

        /// <summary>
        /// 主动技能
        /// </summary>
        /// <returns></returns>
        public bool CanFinishSkillInput()
        {
            if (animator.AnimationAtTag("Skill")) { return false; }
            if (animator.AnimationAtTag("Hit")) { return false; }
            if (animator.AnimationAtTag("Parry")) { return false; }
            if (animator.AnimationAtTag("ATK")) { return false; }
            if (comboData.finishSkillCombo == null) { return false; }
         
            return true;

        }
        public bool CanSkillInput()
        {
            if (animator.AnimationAtTag("Skill")) { return false; }
            if (animator.AnimationAtTag("Hit")) { return false; }
            if (animator.AnimationAtTag("Parry")) { return false; }
            if (animator.AnimationAtTag("ATK")) { return false; }
            if (comboData.skillCombo == null) { return false; }

            return true;

        }

        /// <summary>
        /// 终极大招
        /// </summary>
        public void FinishSkillInput()
        {
            if (comboData.finishSkillCombo == null) { return; }
            if (reusableData.currentCombo == null || reusableData.currentCombo != comboData.finishSkillCombo)
            {
                reusableData.currentSkill = comboData.finishSkillCombo;
            }
            ExecuteSkill();
        }
        /// <summary>
        /// 大招
        /// </summary>
        public void SkillInput()
        {
            if (comboData.skillCombo == null) { return; }
            if (reusableData.currentCombo == null || reusableData.currentCombo != comboData.skillCombo)
            {
                reusableData.currentSkill = comboData.skillCombo;
            }
            ExecuteSkill();
        }
       
        /// <summary>
        /// 执行大招
        /// </summary>
        private void ExecuteSkill()
        {
            ReSetATKIndex(0);
            //播放语音
            PlayCharacterVoice(reusableData.currentSkill);
            //播放武器音效
            PlayWeaponSound(reusableData.currentSkill);
            animator.CrossFadeInFixedTime(reusableData.currentSkill.comboName, 0.1f);
        }
        
        #endregion

        #region 敌人检测
        public void UpdateDetectionDir()
        {
         
            Vector3 camForwardDir = Vector3.zero;
            camForwardDir.Set(reusableData.cameraTransform.forward.x, 0, reusableData.cameraTransform.forward.z);
            camForwardDir.Normalize();
         
           reusableData.detectionDir = camForwardDir * PlayerInputSystem.MainInstance.PlayerMove.y + reusableData.cameraTransform.right * PlayerInputSystem.MainInstance.PlayerMove.x;
           reusableData.detectionDir.Normalize();
        }
        public void UpdateEnemy()
        {
            UpdateDetectionDir();

            reusableData.detectionOrigin = new Vector3(playerTransform.position.x, playerTransform.position.y + 0.7f, playerTransform.position.z);
           
            /*if (Physics.SphereCast(reusableData.detectionOrigin,enemyDetectionData.detectionRadius, reusableData.detectionDir, out var hit, enemyDetectionData.detectionLength, enemyDetectionData.WhatIsEnemy))
            {
                if (GameBlackboard.MainInstance.GetEnemy() != hit.collider.transform || GameBlackboard.MainInstance.GetEnemy() == null)
                {   
                    GameBlackboard.MainInstance.SetEnemy(hit.collider.transform);
                }
            }*/
        }
        
        public void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            //Gizmos.DrawWireSphere(reusableData. detectionOrigin + reusableData.detectionDir * enemyDetectionData.detectionLength, enemyDetectionData.detectionRadius);
        }

        #endregion


    }
}
