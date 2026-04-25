using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tools;

[CreateAssetMenu(fileName = "CharacterHealthData",menuName = "Create/Asset/CharacterHealthData")]
public class CharacterHealthData : ScriptableObject
{
    [field: SerializeField] public float maxHP { get; private set; }
    [field: SerializeField] public float maxStrength { get; private set; }

    [field: SerializeField] public float maxDefenseValue { get; private set; }
}

[CreateAssetMenu(fileName ="CharacterHealthInfo",menuName ="Create/Asset/CharacterHealthInfo")]
public class CharacterHealthInfo : ScriptableObject
{
    [SerializeField]private CharacterHealthData healthData;
  
    public GenericProperty<float> currentHP = new GenericProperty<float>();
    public GenericProperty<float> currentStrength = new GenericProperty<float>();
    public GenericProperty<float> currentDefenseValue = new GenericProperty<float>();
    public GenericProperty<bool> onDead = new GenericProperty<bool>();
    public GenericProperty<bool> hasStrength =new GenericProperty<bool>();
    public float maxHP;
  
    //属性封装不会显示在unity面板上

    public void InitHealthData()
    {
        maxHP= healthData.maxHP;
        currentHP.Value = healthData.maxHP;
        currentStrength.Value=healthData.maxStrength;
        currentDefenseValue.Value = healthData.maxDefenseValue;
        Debug.Log("敌人初始化的血量为" + currentHP.Value);
    }
    public void TakeDamage(float Damage)
    {
        if (!hasStrength.Value)
        {

            currentHP.Value = TakeHealthValue(currentHP.Value, Damage, healthData.maxHP, false);
        }
     
    }
    public void TakeStrength(float Damage)
    {
        if (hasStrength.Value)
        {
            currentStrength.Value = TakeHealthValue(currentStrength.Value, Damage, healthData.maxHP, false);
        }
    }
    public void TakeDefenseValue(float Damage)
    {
        currentDefenseValue.Value = TakeHealthValue(currentDefenseValue.Value, Damage, healthData.maxDefenseValue, false);
    }
    public void RePH(float Damage)
    {
        currentHP.Value = TakeHealthValue(currentHP.Value, Damage, healthData.maxHP, true);
    }
    public void ReStrength(float Damage)
    {
        currentStrength.Value = TakeHealthValue(currentStrength.Value, Damage, healthData.maxStrength, true);
    }

    /// 回满防御值
    public void ReDefenseValue()
    { 
    currentDefenseValue.Value = TakeHealthValue(currentDefenseValue.Value, healthData.maxDefenseValue, healthData.maxDefenseValue, true);
    }
    
    private float TakeHealthValue(float currentValue,float offsetValue,float maxValue,bool canAdd)
    {
        return Mathf.Clamp(canAdd?currentValue+ offsetValue:currentValue-offsetValue,0, maxValue);
    
    }
}
