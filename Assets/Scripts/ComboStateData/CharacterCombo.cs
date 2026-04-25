using Tools;
using UnityEngine;

namespace PlayerState
{
    public class CharacterCombo : CharacterComboBase
    {
        private const float AttackWindowGizmoDuration = 0.15f;
        private const int MaxOverlapHits = 12;
        private float attackWindowGizmoExpireTime = -1f;
        private readonly Collider[] overlapHits = new Collider[MaxOverlapHits];

        public CharacterCombo(Animator animator, Transform playerTransform,Transform cameraTransform, PlayerComboReusableData reusableData, PlayerComboSOData playerComboSOData, Player player) : base(animator, playerTransform, cameraTransform, reusableData, playerComboSOData , player )
        {
           
        }

        #region ??A????
       

        public  void DodgeComboInput()
        {
            
        }

        #endregion
        

        #region ???????

        /// <summary>
        /// ????????
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
        /// ???????
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
        /// ????
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
        /// ???????
        /// </summary>
        private void ExecuteSkill()
        {
            ReSetATKIndex(0);
            //????????
            PlayCharacterVoice(reusableData.currentSkill);
            //????????????
            PlayWeaponSound(reusableData.currentSkill);
            animator.CrossFadeInFixedTime(reusableData.currentSkill.comboName, 0.1f);
        }
        
        #endregion

        #region ??????
        public void UpdateDetectionDir()
        {
            // ????????????????????????
            reusableData.detectionDir = playerTransform.forward;
        }
        public void UpdateEnemy()
        {
            // 仅在攻击相关动画期间检测目标，避免非战斗时持续扫描。
            if (!animator.AnimationAtTag("ATK") && !animator.AnimationAtTag("Skill"))
            {
                return;
            }

            // 已有目标时直接复用，保证朝向逻辑稳定。
            if (player.CurrentEnemy != null)
            {
                return;
            }

            DetectEnemyInAttackWindow();
        }
        
        public void DrawDetectionGizmos()
        {
            if (playerTransform == null || reusableData == null || enemyDetectionData == null)
            {
                return;
            }
            if (Time.time > attackWindowGizmoExpireTime)
            {
                return;
            }

            var detectionOrigin = new Vector3(playerTransform.position.x, playerTransform.position.y + 0.7f, playerTransform.position.z);
            var detectionDir = reusableData.detectionDir.sqrMagnitude < 0.0001f ? playerTransform.forward : reusableData.detectionDir;
            detectionDir.y = 0f;
            detectionDir.Normalize();

            var sphereCenter = detectionOrigin + detectionDir * enemyDetectionData.detectionLength;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(detectionOrigin, sphereCenter);
            Gizmos.DrawWireSphere(sphereCenter, enemyDetectionData.detectionRadius);

            Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.15f);
            Gizmos.DrawSphere(sphereCenter, enemyDetectionData.detectionRadius);
        }

        protected override void OnAttackWindowTriggered()
        {
            attackWindowGizmoExpireTime = Time.time + AttackWindowGizmoDuration;
            DetectEnemyInAttackWindow();
        }

        private void DetectEnemyInAttackWindow()
        {
            UpdateDetectionDir();
            reusableData.detectionOrigin = new Vector3(playerTransform.position.x, playerTransform.position.y + 0.7f, playerTransform.position.z);

            if (enemyDetectionData == null)
            {
                player.SetCurrentEnemy(null);
                return;
            }

            if (Physics.SphereCast(reusableData.detectionOrigin, enemyDetectionData.detectionRadius, reusableData.detectionDir, out var hit, enemyDetectionData.detectionLength, enemyDetectionData.WhatIsEnemy))
            {
                player.SetCurrentEnemy(hit.collider.GetComponentInParent<Enemy>());
                return;
            }

            var sphereCenter = reusableData.detectionOrigin + reusableData.detectionDir * enemyDetectionData.detectionLength;
            var overlapCount = Physics.OverlapSphereNonAlloc(
                sphereCenter,
                enemyDetectionData.detectionRadius,
                overlapHits,
                enemyDetectionData.WhatIsEnemy,
                QueryTriggerInteraction.Collide);

            Enemy nearestEnemy = null;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < overlapCount; i++)
            {
                var collider = overlapHits[i];
                if (collider == null)
                {
                    continue;
                }

                var enemyTarget = collider.GetComponentInParent<Enemy>();
                if (enemyTarget == null)
                {
                    continue;
                }

                var distance = (enemyTarget.transform.position - playerTransform.position).sqrMagnitude;
                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestEnemy = enemyTarget;
            }

            for (var i = 0; i < overlapCount; i++)
            {
                overlapHits[i] = null;
            }

            if (nearestEnemy != null)
            {
                player.SetCurrentEnemy(nearestEnemy);
                return;
            }

            player.SetCurrentEnemy(null);
        }

        #endregion


    }
}
