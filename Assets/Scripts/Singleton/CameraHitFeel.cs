using System.Collections;
using Cinemachine;
using Tools;
using UnityEngine;

public class CameraHitFeel : LazySingleton<CameraHitFeel>
{
    [SerializeField] private Animator currentCharacterAnimator;
    [SerializeField] private Animator currentEnemyAnimator;
    [SerializeField] private float slowMotionResetSpeed = 5f;
    [SerializeField] private CinemachineImpulseSource cinemachineImpulseSource;

    private Coroutine animationEffect;
    private float characterOriginalSpeed = 1f;
    private float enemyOriginalSpeed = 1f;
    private bool ownsTimeScale;
    private float originalTimeScale = 1f;

    public void PF(float time)
    {
        if (time <= 0f || !BeginAnimationEffect()) return;
        animationEffect = StartCoroutine(PauseAnimations(time));
    }

    public void SlowMotion(float time, float speedMult)
    {
        if (time <= 0f || !BeginAnimationEffect()) return;
        animationEffect = StartCoroutine(SlowAnimations(time, Mathf.Clamp01(speedMult)));
    }

    private bool BeginAnimationEffect()
    {
        EndAnimationEffect();
        var player = FindObjectOfType<Player>();
        if (player == null || player.IsDead || player.CurrentEnemy == null || player.CurrentEnemy.IsDead)
            return false;
        // Use the actual combat target, not an arbitrary enemy in the scene.
        currentCharacterAnimator = player.GetAnimator();
        currentEnemyAnimator = player.CurrentEnemy.GetAnimator();
        if (currentCharacterAnimator == null || currentEnemyAnimator == null) return false;
        characterOriginalSpeed = currentCharacterAnimator.speed;
        enemyOriginalSpeed = currentEnemyAnimator.speed;
        return true;
    }

    private IEnumerator PauseAnimations(float time)
    {
        SetAnimationSpeed(0f);
        yield return new WaitForSecondsRealtime(time);
        RestoreAnimationSpeeds();
        animationEffect = null;
    }

    private IEnumerator SlowAnimations(float time, float multiplier)
    {
        SetAnimationSpeed(multiplier);
        yield return new WaitForSecondsRealtime(time);
        while (multiplier < 0.999f)
        {
            multiplier = Mathf.MoveTowards(multiplier, 1f, Mathf.Max(0.1f, slowMotionResetSpeed) * Time.unscaledDeltaTime);
            SetAnimationSpeed(multiplier);
            yield return null;
        }
        RestoreAnimationSpeeds();
        animationEffect = null;
    }

    private void SetAnimationSpeed(float multiplier)
    {
        if (currentCharacterAnimator != null) currentCharacterAnimator.speed = characterOriginalSpeed * multiplier;
        if (currentEnemyAnimator != null) currentEnemyAnimator.speed = enemyOriginalSpeed * multiplier;
    }

    private void RestoreAnimationSpeeds()
    {
        if (currentCharacterAnimator != null) currentCharacterAnimator.speed = characterOriginalSpeed;
        if (currentEnemyAnimator != null) currentEnemyAnimator.speed = enemyOriginalSpeed;
        currentCharacterAnimator = null;
        currentEnemyAnimator = null;
    }

    private void EndAnimationEffect()
    {
        if (animationEffect != null) StopCoroutine(animationEffect);
        animationEffect = null;
        RestoreAnimationSpeeds();
    }

    public void StartSlowTime(float timeScale)
    {
        if (!ownsTimeScale) originalTimeScale = Time.timeScale;
        ownsTimeScale = true;
        Time.timeScale = Mathf.Clamp(timeScale, 0f, 1f);
    }

    public void EndSlowTime()
    {
        if (!ownsTimeScale) return;
        Time.timeScale = originalTimeScale;
        ownsTimeScale = false;
    }

    private void OnDisable()
    {
        EndAnimationEffect();
        EndSlowTime();
    }

    public void CameraShake(float shakeForce)
    {
        if (shakeForce > 0f && cinemachineImpulseSource != null)
            cinemachineImpulseSource.GenerateImpulseWithForce(shakeForce);
    }
}
