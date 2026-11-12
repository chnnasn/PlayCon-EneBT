using UnityEngine;
using Tools;

[CreateAssetMenu(fileName = "CharacterHealthData", menuName = "Create/Asset/CharacterHealthData")]
public class CharacterHealthData : ScriptableObject
{
    [field: SerializeField] public float maxHP { get; private set; }
}

[CreateAssetMenu(fileName = "CharacterHealthInfo", menuName = "Create/Asset/CharacterHealthInfo")]
public class CharacterHealthInfo : ScriptableObject
{
    [SerializeField] private CharacterHealthData healthData;
    public GenericProperty<float> currentHP = new GenericProperty<float>();
    public GenericProperty<bool> onDead = new GenericProperty<bool>();
    public float maxHP;

    public void InitHealthData(float fallbackMaxHP = 100f)
    {
        var configuredMax = healthData != null ? healthData.maxHP : maxHP;
        maxHP = IsPositiveFinite(configuredMax) ? configuredMax : IsPositiveFinite(fallbackMaxHP) ? fallbackMaxHP : 100f;
        // The asset is configuration; every runtime clone owns fresh mutable properties.
        currentHP = new GenericProperty<float>();
        onDead = new GenericProperty<bool>();
        currentHP.Value = maxHP;
    }

    public void TakeDamage(float damage)
    {
        if (IsPositiveFinite(damage))
            currentHP.Value = Mathf.Clamp(currentHP.Value - damage, 0f, maxHP);
    }

    public void RePH(float amount)
    {
        if (IsPositiveFinite(amount))
            currentHP.Value = Mathf.Clamp(currentHP.Value + amount, 0f, maxHP);
    }

    public void RestoreFullHealth()
    {
        currentHP.Value = maxHP;
    }

    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
