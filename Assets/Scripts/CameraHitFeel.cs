using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Tools;

public class CameraHitFeel : LazySingleton<CameraHitFeel>
{
    [SerializeField] private Animator currentCharacterAnimator;
    [SerializeField] private Animator currentEnemyAnimator;
    [SerializeField] private float slowMotionResetSpeed;
    [SerializeField] private CinemachineImpulseSource cinemachineImpulseSource;
    
    private void Start()
    {
        Init();
    }

    private void Init()
    {


    }

    Coroutine PauseFrameCoroutine;
    Coroutine SlowMotionCoroutine;

    public void PF(float time)
    {
        if (time == 0) { Debug.Log("顿帧时间为0退出");return; }
        currentEnemyAnimator= GetEnemyAnimator();
        currentCharacterAnimator=GetCurrentCharacterAnimator();

        if (currentCharacterAnimator == null )
        {
            Debug.Log("currentCharacterAnimator is null!");
            return;
        }
        if (currentEnemyAnimator == null)
        {
            Debug.Log("currentEnemyAnimator is null!");
            return;
        }
   

        if (PauseFrameCoroutine != null)
        { 
        StopCoroutine(PauseFrameCoroutine);
        }
        PauseFrameCoroutine = StartCoroutine(PauseFrameOnAnimation(time));
    }

    /// 动画特效的慢速
    public void SlowMotion(float time,float speedMult)
    {
        currentEnemyAnimator = GetEnemyAnimator();
        currentCharacterAnimator = GetCurrentCharacterAnimator();
        if (currentCharacterAnimator == null || currentEnemyAnimator == null)
        {
            Debug.LogWarning("Animator is null!");
            return;
        }
        if (SlowMotionCoroutine != null)
        { StopCoroutine(SlowMotionCoroutine); }
        SlowMotionCoroutine = StartCoroutine(SlowMotionOnAnimation(time, speedMult));
    }
    public void StartSlowTime(float timeScale)
    { 
      
       Time.timeScale=timeScale;
    }
    public void EndSlowTime()
    {
        Time.timeScale = 1;
    }
    IEnumerator SlowMotionOnAnimation(float time,float speedMult)
    {
        float currentSpeed = speedMult;
        currentCharacterAnimator.speed = currentSpeed;
        currentEnemyAnimator.speed = currentSpeed;
        yield return new WaitForSeconds(time);
        float minValue = 0.001f;
        while (Mathf.Abs(currentSpeed-1)>minValue)
        {
            currentSpeed = Mathf.Lerp(currentSpeed,1,Time.deltaTime* slowMotionResetSpeed);
            currentCharacterAnimator.speed = currentSpeed;
            currentEnemyAnimator.speed = currentSpeed;

            yield return null;

        }
        currentSpeed = 1;
        currentCharacterAnimator.speed = currentSpeed;
        currentEnemyAnimator.speed = currentSpeed;

    }
    IEnumerator PauseFrameOnAnimation(float time)
    {
        Debug.LogWarning("进入顿帧协程"+ time);
        currentCharacterAnimator.speed = 0f;
        currentEnemyAnimator.speed = 0f;
        yield return new WaitForSeconds(time);
        currentCharacterAnimator.speed = 1f;
        currentEnemyAnimator.speed = 1f;
    }
    

    private Animator GetEnemyAnimator()
    {
        Enemy enemy =  FindAnyObjectByType<Enemy>();
        return enemy.GetAnimator();
    }
    private Animator GetCurrentCharacterAnimator()
    {
        Player player =  FindAnyObjectByType<Player>();
        return player.GetAnimator();

    }
    
    #region 震屏
    public void CameraShake(float shakeForce)
    {
        if (shakeForce == 0) { return; }
        cinemachineImpulseSource.GenerateImpulseWithForce(shakeForce);
    }


    #endregion
    
}
