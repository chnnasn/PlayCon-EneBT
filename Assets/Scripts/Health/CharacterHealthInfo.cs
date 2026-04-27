using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tools;

[CreateAssetMenu(fileName = "CharacterHealthData",menuName = "Create/Asset/CharacterHealthData")]
public class CharacterHealthData : ScriptableObject
{
    [field: SerializeField] public float maxHP { get; private set; }
}

[CreateAssetMenu(fileName ="CharacterHealthInfo",menuName ="Create/Asset/CharacterHealthInfo")]
public class CharacterHealthInfo : ScriptableObject
{
    [SerializeField]private CharacterHealthData healthData;
  
    public GenericProperty<float> currentHP = new GenericProperty<float>();
    public GenericProperty<bool> onDead = new GenericProperty<bool>();
    public float maxHP;
  
    //属性封装不会显示在unity面板上

    public void InitHealthData()
    {
        maxHP= healthData.maxHP;
        currentHP.Value = healthData.maxHP;
        Debug.Log("敌人初始化的血量为" + currentHP.Value);
    }
    public void TakeDamage(float Damage)
    {
        currentHP.Value = TakeHealthValue(currentHP.Value, Damage, healthData.maxHP, false);
    }
    public void RePH(float Damage)
    {
        currentHP.Value = TakeHealthValue(currentHP.Value, Damage, healthData.maxHP, true);
    }
    
    private float TakeHealthValue(float currentValue,float offsetValue,float maxValue,bool canAdd)
    {
        return Mathf.Clamp(canAdd?currentValue+ offsetValue:currentValue-offsetValue,0, maxValue);
    
    }
}
