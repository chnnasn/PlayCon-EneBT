using UnityEngine;
using UnityEngine.AI;

namespace Enemy
{
    public class AnimationComponent : MonoBehaviour
    {
        private static readonly int HIT = Animator.StringToHash("hit");
        private static readonly int SPEED = Animator.StringToHash("speed");
        private static readonly int ATTACK = Animator.StringToHash("attack");
        private static readonly int ATTACK_TAUNT = Animator.StringToHash("attack_taunt");
        private static readonly int ATTACK_STUN = Animator.StringToHash("attack_stun");
        private static readonly int DEATH = Animator.StringToHash("dead");
        private static readonly int STUNNED = Animator.StringToHash("stunned");

        public Animator _animator;
        private NavMeshAgent _navMeshAgent;

        private Vector3 _prevPos;

        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _prevPos = transform.position;
        }

        private void FixedUpdate()
        {
            if (_animator == null)
            {
                return;
            }

            Vector3 velocity;
            if (_navMeshAgent == null)
            {
                velocity = (transform.position - _prevPos) / Time.fixedDeltaTime;
            }
            else
            {
                velocity = _navMeshAgent.velocity;
            }
            
            _animator.SetFloat(SPEED, velocity.magnitude);
            _prevPos = transform.position;
        }
        
        public void PlayHitAnimation()
        {
            _animator.SetTrigger(HIT);
        }

        public void PlayAttackAnimation(AttackComponent.AttackType attackEffectAttackType)
        {
            if (_animator == null)
            {
                return;
            }

            switch (attackEffectAttackType)
            {
                case AttackComponent.AttackType.Damage:
                    _animator.SetTrigger(ATTACK);
                    break;
                case AttackComponent.AttackType.Stun:
                    _animator.SetTrigger(ATTACK_STUN);
                    break;
                case AttackComponent.AttackType.Taunt:
                    _animator.SetTrigger(ATTACK_TAUNT);
                    break;
            }
        }

        public void PlayDeathAnimation()
        {
            if (_animator != null)
            {
                _animator.SetBool(DEATH, true);
            }
        }

        public void SetStunned(bool isStunned)
        {
            if (_animator != null)
            {
                _animator.SetBool(STUNNED, isStunned);
            }
        }
    }
}