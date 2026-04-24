using System.Collections.Generic;
using UnityEngine;

namespace Enemy
{
    public class AttackTrigger : MonoBehaviour
    {
        public List<AttackComponent> _attackComponents;
    
        private void OnTriggerEnter(Collider other)
        {
            if (_attackComponents == null)
            {
                return;
            }

            foreach (AttackComponent component in _attackComponents)
            {
                if (component != null)
                {
                    component.AttackTriggerHit(other);
                }
            }
        }
    }
}
