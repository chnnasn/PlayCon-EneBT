using Tools;
using System;
using System.Collections.Generic;
using UnityEngine;

public interface IUI { }

public class UIManager : LazySingleton<UIManager>
{
    [SerializeField] public StateBarUI EnemyBarUI;
    GameObject uIRoot;
    private Dictionary<Type, IUI> uiDictionary = new Dictionary<Type, IUI>();
    public void RegisterUI<T>(IUI uI) where T : IUI
    { 
        Type type = typeof(T);
        if (!uiDictionary.ContainsKey(type))
        {
            uiDictionary.Add(type, uI);
        }
        else
        {
            uiDictionary[type] = uI;
        }
    }
    public T Get<T>() where T : class,IUI
    { 
        Type t = typeof(T);
        if (uiDictionary.TryGetValue(t,out var uI))
        {
            return uI as T;
        }
        return default(T);
    }
    protected override void Awake()
    {
        base.Awake();
        EnemyBarUI =transform.Find("UIRoot/State Bar").GetComponent<StateBarUI>();
    }
  


}