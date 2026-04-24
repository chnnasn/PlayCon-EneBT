using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enemy
{
    [DefaultExecutionOrder(200)]
    public class AttackComponent : MonoBehaviour
    {
        public enum AttackType
        {
            Damage,
            Stun,
            Taunt
        }

        [Serializable]
        public class AttackEffects
        {
            public float attackValue;
            public AttackType attackType;
        }

        [Header("Attack attributes")]
        [Tooltip("This attack won't be usable for that many seconds")]
        public float _attackCooldown = 3.0f;

        [Tooltip("No attacks will be usable for that many seconds, but controller will still be able to move")]
        public float _attackDuration = 1.0f;

        [Tooltip("No attacks nor movement will be usable for that many seconds")]
        public float _castTime = 0.0f;
        [Header("Target filtering")]
        [SerializeField] private LayerMask _targetLayers = ~0;

        public List<AttackEffects> _attackEffects;
        public Collider _attackCollider;

        private List<Collider> _targetList;
        private AttributesComponent _attributesComponent;
        private float _cooldown = 0;
        private bool _attacking = false;
        private AnimationComponent _animationComponent;

        private Animator _attackAnimator;
        private static readonly int ATTACK = Animator.StringToHash("attack");

        public void Awake()
        {
            _targetList = new List<Collider>();
            _attributesComponent = GetComponentInParent<AttributesComponent>();
            _animationComponent = GetComponentInParent<AnimationComponent>();

            _attackAnimator = GetComponent<Animator>();
        }
        
        public void OnEnable()
        {
            if (_attackCollider != null)
            {
                _attackCollider.gameObject.SetActive(false);
            }
        }

        public void Attack()
        {
            if (_attributesComponent == null || !_attributesComponent.CanAttack() || _cooldown > 0)
            {
                return;
            }

            if (_castTime > 0)
            {
                _attributesComponent.StartCastTime(_castTime);
            }

            _attacking = true;
            _cooldown = _attackCooldown;
            _attributesComponent.SetAttackCooldown(_attackDuration);
            if (_attackCollider != null)
            {
                _attackCollider.gameObject.SetActive(true);
            }
        
            if (_animationComponent != null && _attackEffects != null && _attackEffects.Count > 0)
            {
                _animationComponent.PlayAttackAnimation(_attackEffects[0].attackType);
            }

            if (_attackAnimator != null)
            {
                _attackAnimator.SetTrigger(ATTACK);
            }

            StartCoroutine(AttackCoroutine());
        }

        private IEnumerator AttackCoroutine()
        {
            yield return new WaitForSeconds(_attackDuration);
            _attacking = false;
            if (_attackCollider != null)
            {
                _attackCollider.gameObject.SetActive(false);
            }
            _targetList.Clear();
            yield return null;
        }

        public void AttackTriggerHit(Collider target)
        {
            if (!_attacking || target == null || _targetList.Contains(target))
            {
                return;
            }

            bool isInTargetLayers = (_targetLayers.value & (1 << target.gameObject.layer)) != 0;
            if (!isInTargetLayers)
            {
                return;
            }

            if (_attributesComponent == null)
            {
                return;
            }

            _targetList.Add(target);

            AttributesComponent targetAttributesComponent = target.GetComponentInParent<AttributesComponent>();
            if (targetAttributesComponent != null)
            {
                if (targetAttributesComponent._teamId != _attributesComponent._teamId && targetAttributesComponent.GetState() != AttributesComponent.CharacterState.Dead)
                {
                    DealAttackEffects(targetAttributesComponent);
                }
            }
        }

        public void DealAttackEffects(AttributesComponent targetAttributesComponent)
        {
            foreach (AttackEffects attackEffect in _attackEffects)
            {
                switch (attackEffect.attackType)
                {
                    case AttackType.Damage:
                    {
                        targetAttributesComponent.Hit(attackEffect.attackValue);
                        break;
                    }
                    case AttackType.Stun:
                    {
                        targetAttributesComponent.StartCoroutine(targetAttributesComponent.Stun(attackEffect.attackValue));
                        break;
                    }
                    case AttackType.Taunt:
                    {
                        if (targetAttributesComponent.GetState() != AttributesComponent.CharacterState.Stunned && targetAttributesComponent.TryGetComponent<EnemyController>(out EnemyController enemyComponent))
                        {
                            enemyComponent._playerAggro += attackEffect.attackValue;
                        }
                        
                        break;
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            _cooldown = Math.Max(0, _cooldown - Time.fixedDeltaTime);
        }
    }
}