using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace Enemy
{
    public class EnemyController : MonoBehaviour
    {
#if UNITY_EDITOR
        private static readonly bool DEBUG_ATTACK_CONE = true;
#endif

        public float _visionAngle = 40.0f;
        public float _aggroDecreaseRate = 1.0f;

        public float _attackDistance = 1.85f;
    
        public List<AttackComponent> _attackComponents;

        private NavMeshAgent _navMeshAgent;
        private CharacterController _charControl;

        public event Action<EnemyController> OnEnemyDeath;
    
        private AttributesComponent _attributesComponent;
        private AttributesComponent _helpless;
        private AttributesComponent _player;

        private bool _isDeathProcessed = false;

        public float _playerAggro = 0.0f;

        private void Start()
        {
            _playerAggro = 0.0f;
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _charControl = GetComponent<CharacterController>();
        
            // supposes enemies and player and helpless have same collision radius
            Debug.Assert(_attackDistance >= 2 * _charControl.radius, "T-REX CAN'T REACH BECAUSE TOO BIG!!");
            Debug.Assert(_navMeshAgent.stoppingDistance >= 2 * _charControl.radius, "T-REX CAN'T REACH BECAUSE TOO BIG!!");
        
            _attributesComponent = GetComponent<AttributesComponent>();
            Player player = GameObject.FindObjectOfType<Player>();
            if (player != null)
            {
                _player = player.GetComponent<AttributesComponent>();
            }
        }

        private void Update()
        {
            if (_attributesComponent.CanMove())
            {
                AttributesComponent target = GetTarget();
            
                if (target != null)
                {
                    _navMeshAgent.SetDestination(target.transform.position);
                    // TargetIfInRange(closestCharacter);
                    //ExtraRotation();
                    Attack(target);
                }
            }
            else if (_attributesComponent.GetState() == AttributesComponent.CharacterState.Stunned)
            {
                StopAgentMovement();
            }
            else if (_attributesComponent.GetState() == AttributesComponent.CharacterState.Dead)
            {
                Die();
            }

            if (_playerAggro > 0.0f)
            {
                _playerAggro -= _aggroDecreaseRate * Time.deltaTime;
            }
        }

        public void FixedUpdate()
        {
            if (_attributesComponent.CanMove())
            {
                Vector3 desVelocity = _navMeshAgent.desiredVelocity;
                desVelocity.y = -800 * Time.fixedDeltaTime;
                _charControl.Move(desVelocity * Time.fixedDeltaTime);
                _navMeshAgent.velocity = _charControl.velocity;
            }
        }

        private void StopAgentMovement()
        {
            _navMeshAgent.ResetPath();
            _navMeshAgent.velocity = Vector3.zero;
        }
    
        private AttributesComponent GetTarget()
        {
            if ((_playerAggro > 0) && (_player != null) && _player.GetState() != AttributesComponent.CharacterState.Dead)
            {
                return _player;
            }

            if (_helpless != null && _helpless.GetState() != AttributesComponent.CharacterState.Dead)
            {
                return _helpless;
            }

            if (_player != null && _player.GetState() != AttributesComponent.CharacterState.Dead)
            {
                return _player;
            }

            return null;
        }
    
        private static Vector3 RotateVector2D(Vector3 v, float rads, float y = 0)
        {
            Vector3 result;
            result.x = v.x * Mathf.Cos(rads) - v.z * Mathf.Sin(rads);
            result.y = y;
            result.z = v.x * Mathf.Sin(rads) + v.z * Mathf.Cos(rads);
            return result;
        }
    
        private bool IsCharacterReachable(AttributesComponent target)
        {
            Vector3 targetVector = target.transform.position - transform.position;
            float angle = Vector3.Angle(targetVector, transform.forward);

#if UNITY_EDITOR
            if (DEBUG_ATTACK_CONE)
            {
                float halfAngle = _visionAngle * Mathf.Deg2Rad / 2;
                Vector3 coneStart = RotateVector2D(transform.forward, -halfAngle) * _attackDistance;
                Vector3 coneEnd = RotateVector2D(transform.forward, halfAngle) * _attackDistance;
                coneStart.y = 0;
                coneEnd.y = 0;

                Vector3 coneOrigin = transform.position;
                Vector3 targetOrigin = target.transform.position;
                coneOrigin.y = 1;
                targetOrigin.y = 1;
                Debug.DrawLine(coneOrigin, targetOrigin, Color.red);
                Debug.DrawLine(coneOrigin, coneOrigin + coneStart, Color.blue);
                Debug.DrawLine(coneOrigin, coneOrigin + coneEnd, Color.blue);

                float radByStep = 2 * halfAngle / 16;
                Vector3 end = coneStart;
                for (int i = 0; i < 16; ++i)
                {
                    Vector3 start = end;

                    end = RotateVector2D(end, radByStep);
                
                    Debug.DrawLine(coneOrigin + start, coneOrigin + end, Color.blue);
                }
            }
#endif

            if (angle <= _visionAngle && targetVector.magnitude <= _attackDistance)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private void Attack(AttributesComponent target)
        {
            if (_attributesComponent.CanAttack() && IsCharacterReachable(target) && _attackComponents.Count > 0)
            {
                _attackComponents[Random.Range(0, _attackComponents.Count)].Attack();
            }
        }

        public void Die()
        {
            if (!_isDeathProcessed)
            {
                OnEnemyDeath?.Invoke(this);
                _isDeathProcessed = true;
                StopAgentMovement();
                _navMeshAgent.enabled = false;

                List<Collider> colliders = new List<Collider>();
                GetComponents(colliders);
                GetComponentsInChildren(colliders);
                foreach (Collider c in colliders)
                {
                    c.enabled = false;
                }
            }
        }
    }
}
