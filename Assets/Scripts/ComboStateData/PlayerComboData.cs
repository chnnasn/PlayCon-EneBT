using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerState
{
    [System.Serializable]
    public class EnemyDetectionData
    {
        [field: SerializeField, Header("检测距离")]
        public float detectionLength { get; private set; } = 6f;

        [field: SerializeField, Header("球体半径")]
        public float detectionRadius { get; private set; } = 0.45f;

        [field: SerializeField, Header("敌人层级掩码")]
        public LayerMask WhatIsEnemy { get; private set; }
    }

    [System.Serializable]

    public class PlayerComboData
    {
        [field: SerializeField, Header("招式配置")]
        public PlayerComboSOData comboData { get; private set; }

        [field: SerializeField, Header("敌人检测配置")]
        public EnemyDetectionData enemyDetectionData { get; private set; } = new EnemyDetectionData();

    }
}
