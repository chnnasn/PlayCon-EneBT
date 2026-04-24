using System;
using System.Collections;
using UnityEngine;

namespace Enemy
{
    public class AttributesComponent : MonoBehaviour
    {
        public enum CharacterState
        {
            Normal,
            Hurt,
            Stunned,
            Dead,
        }
    
        public int _teamId = 1;
        public float _maxHealth = 100.0f;
        public float _health = 100.0f;
        [SerializeField] private float _hurtLimit = 40.0f;
        [SerializeField] private CharacterState _state = CharacterState.Normal;
        private bool _isStunned = false;
        public float _attackCooldown = 0.0f;
        public Transform _stunnedObject;

        private AnimationComponent _animationComponent;
        private float _remainingCastTime;
        private Coroutine _stunCoroutine;

        private void Awake()
        {
            _animationComponent = GetComponent<AnimationComponent>();
        
            if (_stunnedObject != null)
            {
                _stunnedObject.gameObject.SetActive(false);
            }
        }
    
        private void FixedUpdate()
        {
            _attackCooldown = Math.Max(0, _attackCooldown - Time.fixedDeltaTime);
        }

        public void Update()
        {
            UpdateCharacterState();

            if (_state != CharacterState.Dead)
            {
                if (_remainingCastTime > 0)
                {
                    _remainingCastTime -= Time.deltaTime;
                }
            }
        }

        public void SetAttackCooldown(float value)
        {
            _attackCooldown = value;
        }

        public bool CanAttack()
        {
            return _attackCooldown <= 0 && _remainingCastTime <= 0;
        }

        public bool CanMove()
        {
            return _remainingCastTime <= 0 && _state != CharacterState.Dead && _state != CharacterState.Stunned;
        }

        public CharacterState GetState()
        {
            return _state;
        }

        public void StartCastTime(float castTime)
        {
            _remainingCastTime = castTime;
        }

        public void Heal(float healPoints)
        {
            if (_state != CharacterState.Dead)
            {
                _health += healPoints;
            
                if (_health > _maxHealth)
                {
                    _health = _maxHealth;
                }
            }
        }

        public void Hit(float damagePoints)
        {
            Debug.Log("Damage taken by " + name + ": " + damagePoints);
            _health -= damagePoints;

            if (_animationComponent != null)
            {
                _animationComponent.PlayHitAnimation();
            }
        }

        public void Revive(float healthPoints = 20.0f)
        {
            if (_state == CharacterState.Dead)
            {
                _health = healthPoints;
            }
        }

        public IEnumerator Stun(float stunTime = 4.0f)
        {
            if (_stunCoroutine != null)
            {
                StopCoroutine(_stunCoroutine);
            }

            _stunCoroutine = StartCoroutine(StunRoutine(stunTime));
            yield return _stunCoroutine;
            _stunCoroutine = null;
        }

        private IEnumerator StunRoutine(float stunTime)
        {
            _isStunned = true;
            if (_animationComponent != null)
            {
                _animationComponent.SetStunned(true);
            }
        
            if (_stunnedObject != null)
            {
                _stunnedObject.gameObject.SetActive(true);
            }
        
            yield return new WaitForSeconds(stunTime);
            _isStunned = false;
        
            if (_stunnedObject != null)
            {
                _stunnedObject.gameObject.SetActive(false);
            }
            if (_animationComponent != null)
            {
                _animationComponent.SetStunned(false);
            }
        
            yield return null;
        }

        private void UpdateCharacterState()
        {
            if (_health <= 0.0f)
            {
                if (_state != CharacterState.Dead)
                {
                    StartCoroutine(Die());
                }
                _state = CharacterState.Dead;
                _health = 0.0f;
            
            }
            else if (_isStunned)
            {
                _state = CharacterState.Stunned;
            }
            else if (_health <= _hurtLimit)
            {
                _state = CharacterState.Hurt;
            }
            else
            {
                _state = CharacterState.Normal;
            }
        }

        private IEnumerator Die()
        {
            if (_animationComponent != null)
            {
                _animationComponent.PlayDeathAnimation();
            }
            yield return new WaitForSeconds(5.0f);
            float until = Time.time + 1;
            while (Time.time < until)
            {
                Vector3 newPos = transform.position;
                newPos.y -= 2f * Time.deltaTime;
                transform.position = newPos;
                yield return null;
            }
            GameObject.Destroy(gameObject);
        }
    }
}
