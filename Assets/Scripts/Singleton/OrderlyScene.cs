using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tools;

public class OrderlyScene : LazySingleton<OrderlyScene>
{
    private GameTimer _oneStrokeTimer;
    private bool _oneStrokeActive;

    public event Action OneStrokeTimedOut;
    
    protected override void Awake()
    {
        base.Awake();
    }

    private void OnDisable()
    {
        CancelOneStroke();
    }
        
    public void BeginOneStrokeWithTimeLimit(float seconds)
    {
        CancelOneStroke();
        _oneStrokeActive = true;
        _oneStrokeTimer = TimerManager.MainInstance.GetTimer(seconds, OnOneStrokeTimeout);

    }
    
    public void CancelOneStroke()
    {
        _oneStrokeActive = false;

        if (_oneStrokeTimer != null)
        {
            if (TimerManager.TryGetExistingInstance(out var timerManager))
                timerManager.UnregisterTimer(_oneStrokeTimer);
            _oneStrokeTimer = null;
        }
    }

    private void OnOneStrokeTimeout()
    {
        _oneStrokeTimer = null;
        if (!_oneStrokeActive)
            return;

        _oneStrokeActive = false;
        
        OneStrokeTimedOut?.Invoke();
        
    }
    
}
