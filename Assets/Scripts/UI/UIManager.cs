using Tools;
using System;
using System.Collections.Generic;
using UnityEngine;

public interface IUI { }

public class UIManager : LazySingleton<UIManager>
{
    [Tooltip("Template only. Each enemy receives its own runtime copy.")]
    [SerializeField] public StateBarUI EnemyBarUI;
    private readonly Dictionary<Type, IUI> uiDictionary = new Dictionary<Type, IUI>();
    private readonly Dictionary<CharacterHealthBase, StateBarUI> enemyHealthBars = new Dictionary<CharacterHealthBase, StateBarUI>();

    public void RegisterUI<T>(IUI ui) where T : IUI
    {
        uiDictionary[typeof(T)] = ui;
    }

    public T Get<T>() where T : class, IUI
    {
        return uiDictionary.TryGetValue(typeof(T), out var ui) ? ui as T : default;
    }

    protected override void Awake()
    {
        base.Awake();
        if (EnemyBarUI == null)
        {
            var template = transform.Find("UIRoot/State Bar");
            if (template != null)
                EnemyBarUI = template.GetComponent<StateBarUI>();
        }
        if (EnemyBarUI != null)
            EnemyBarUI.gameObject.SetActive(false);
    }

    public StateBarUI GetOrCreateEnemyHealthBar(CharacterHealthBase owner)
    {
        if (owner == null || EnemyBarUI == null)
            return null;
        if (enemyHealthBars.TryGetValue(owner, out var existing) && existing != null)
            return existing;

        var view = Instantiate(EnemyBarUI, EnemyBarUI.transform.parent);
        view.name = $"{owner.name} Health Bar";
        view.Init();
        view.UpdateBlood(owner.CurrentHP / owner.MaxHP, true);
        view.SetVisible(false);
        view.gameObject.SetActive(true);
        enemyHealthBars[owner] = view;
        return view;
    }

    public void ReleaseEnemyHealthBar(CharacterHealthBase owner)
    {
        if (ReferenceEquals(owner, null) || !enemyHealthBars.TryGetValue(owner, out var view))
            return;
        enemyHealthBars.Remove(owner);
        if (view != null)
            Destroy(view.gameObject);
    }

    protected override void OnDestroy()
    {
        foreach (var view in enemyHealthBars.Values)
        {
            if (view != null)
                Destroy(view.gameObject);
        }
        enemyHealthBars.Clear();
        base.OnDestroy();
    }
}
