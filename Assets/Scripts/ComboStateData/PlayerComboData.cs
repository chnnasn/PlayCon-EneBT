using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerState
{
    [System.Serializable]

    public class PlayerComboData
    {
        [field: SerializeField, Header("招式配置")]
        public PlayerComboSOData comboData { get; private set; }
        

    }
}
