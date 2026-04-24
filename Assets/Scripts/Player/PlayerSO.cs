using UnityEngine;
namespace PlayerState
{
    [CreateAssetMenu(fileName ="Player",menuName ="Create/Character/Player")]
    
    public class PlayerSO:ScriptableObject
    {
        [field: SerializeField] public PlayerMovementData movementData{ get; private set; }
        [field: SerializeField] public PlayerComboData ComboData { get; private set; }
    }
}