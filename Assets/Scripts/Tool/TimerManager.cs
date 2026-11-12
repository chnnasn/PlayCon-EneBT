using System;
using System.Collections.Generic;
using UnityEngine;
using Tools;

public class TimerManager : LazySingleton<TimerManager>
{
    [SerializeField, Header("Initial timer capacity")] private int timerCount;
    private readonly List<GameTimer> isWorkingTimers = new List<GameTimer>();
    private readonly List<GameTimer> pendingTimers = new List<GameTimer>();
    private bool isUpdating;

    protected void Start()
    {
        isWorkingTimers.Capacity = Mathf.Max(isWorkingTimers.Count, timerCount);
    }

    private void Update()
    {
        UpdateTime();
    }

    public void GetOneTimer(float time, Action action)
    {
        GetTimer(time, action);
    }

    public GameTimer GetRealTimer(float time, Action action)
    {
        return CreateTimer(true, time, action);
    }

    public GameTimer GetTimer(float time, Action action)
    {
        return CreateTimer(false, time, action);
    }

    private GameTimer CreateTimer(bool realTime, float time, Action action)
    {
        // Handles remain unique: old states cannot cancel a reused timer owned by another state.
        var timer = new GameTimer();
        timer.StartTimer(realTime, Mathf.Max(0f, time), action);
        (isUpdating ? pendingTimers : isWorkingTimers).Add(timer);
        return timer;
    }

    public void UnregisterTimer(GameTimer timer)
    {
        if (timer == null) return;
        timer.InitTimer();
        pendingTimers.Remove(timer);
        if (!isUpdating) isWorkingTimers.Remove(timer);
    }

    private void UpdateTime()
    {
        isUpdating = true;
        try
        {
            for (var i = 0; i < isWorkingTimers.Count; i++)
            {
                var timer = isWorkingTimers[i];
                if (timer.TimerStation != TimerStation.DoWorking) continue;
                if (timer.IsRealTime) timer.UpdateRealTimer();
                else timer.UpdateTimer();
            }
        }
        finally
        {
            isUpdating = false;
            for (var i = isWorkingTimers.Count - 1; i >= 0; i--)
            {
                if (isWorkingTimers[i].TimerStation != TimerStation.DoWorking)
                    isWorkingTimers.RemoveAt(i);
            }
            isWorkingTimers.AddRange(pendingTimers);
            pendingTimers.Clear();
        }
    }
}
