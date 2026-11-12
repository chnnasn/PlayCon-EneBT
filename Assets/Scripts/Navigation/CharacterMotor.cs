using UnityEngine;

/// <summary>One collision-aware horizontal displacement path; gravity remains with ControllerBase.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
public sealed class CharacterMotor : MonoBehaviour
{
    private CharacterController controller;
    public Vector3 ActualVelocity { get; private set; }
    public CharacterController Controller => controller != null ? controller : (controller = GetComponent<CharacterController>());

    public void Configure(NavigationProfile profile)
    {
        if (profile == null) return;
        var scale = transform.lossyScale;
        var horizontalScale = Mathf.Max(0.001f, Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        var verticalScale = Mathf.Max(0.001f, Mathf.Abs(scale.y));
        Controller.radius = profile.radius / horizontalScale;
        Controller.height = Mathf.Max(profile.height / verticalScale, Controller.radius * 2f);
        Controller.center = Vector3.up * Controller.height * 0.5f;
        Controller.stepOffset = Mathf.Min(profile.stepHeight / verticalScale, Controller.height - 0.01f);
        Controller.slopeLimit = profile.maxSlope;
        Controller.minMoveDistance = 0f;
        Controller.skinWidth = Mathf.Min(0.03f, Controller.radius * 0.1f);
    }

    public Vector3 Move(Vector3 velocity, float deltaTime)
    {
        velocity.y = 0f;
        if (!isActiveAndEnabled || !Controller.enabled || deltaTime <= 0f)
        {
            ActualVelocity = Vector3.zero;
            return Vector3.zero;
        }
        var before = transform.position;
        Controller.Move(velocity * deltaTime);
        var displacement = transform.position - before;
        ActualVelocity = displacement / deltaTime;
        return displacement;
    }

    public void Stop() => ActualVelocity = Vector3.zero;

    public void Face(Vector3 direction, float degreesPerSecond, float deltaTime)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), degreesPerSecond * deltaTime);
    }
}
