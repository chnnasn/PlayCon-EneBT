using Tools;
using UnityEngine;

public class ControllerBase : MonoBehaviour
{
    //重力
    //地面检测
    //坡面检测
    //移动接口
    public Animator characterAnimator { get; private set; }
    protected CharacterController characterController;
    [SerializeField, Header("重力")] private float characterGravity=-9;
    protected float fallOutdeltaTimer;
    protected float fallOutTimer = 0.2f;
    [SerializeField] protected float maxVerticalSpeed=20;
    [SerializeField] protected float minVerticalSpeed=-3;
    [SerializeField]protected float verticalSpeed;
    protected Vector3 verticalVelocity;
    
    [SerializeField, Header("地面检测")] private float GroundDetectionRadius;
    [SerializeField] private float GroundDetectionOffset;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] protected bool isOnGround;
    private Vector3 groundDetectionOrigin;

    [SerializeField, Header("坡面检测")] private float SlopDetectionLenth=1;
    private ColliderHit groundHit;

    [Range(0.2f, 100), SerializeField, Header("移动位移倍率")] private float moveMult = 1f;
    protected virtual bool CanMove => isActiveAndEnabled;
    protected virtual void Awake()
    {
        characterAnimator = GetComponent<Animator>();
        characterController= GetComponent<CharacterController>();
    }
    protected virtual void Start()
    {
        fallOutdeltaTimer = fallOutTimer;
    }
    protected virtual void Update()
    {
        if (!CanMove || characterController == null || !characterController.enabled) return;
        GroundDetecion();
        UpdateChracterGravity();
        UpDateVerticalVelocity(); 
    }
    /// <summary>
    /// 更新动画根位移
    /// </summary>
    protected virtual void OnAnimatorMove()
    {       
        if (!CanMove || characterController == null || !characterController.enabled) return;
        // deltaPosition is already a displacement. Route every animation through collision handling.
        UpdateCharacterVelocity(characterAnimator.deltaPosition);
        transform.rotation *= characterAnimator.deltaRotation;
    }
   
    /// <summary>
    /// 地面检测
    /// </summary>
    protected void GroundDetecion()
    {
        groundDetectionOrigin = new Vector3(transform.position.x, transform.position.y-GroundDetectionOffset, transform.position.z);
        isOnGround = Physics.CheckSphere(groundDetectionOrigin, GroundDetectionRadius, whatIsGround, QueryTriggerInteraction.Ignore);
    }
    /// <summary>
    /// 重力
    /// </summary>
    protected void UpdateChracterGravity()
    {
        if (isOnGround)
        {
            fallOutdeltaTimer = fallOutTimer;
           
            if (verticalSpeed <= 0)
            {
                verticalSpeed = -2;
            }
        }
        else
        {
            if (fallOutdeltaTimer >= 0)
            {
                fallOutdeltaTimer -= Time.deltaTime;
            }
            else
            {
                verticalSpeed = Mathf.Clamp(verticalSpeed + characterGravity * Time.deltaTime,
                    minVerticalSpeed, maxVerticalSpeed);
            }

        }

    }
    /// <summary>
    /// 更新垂直速度
    /// </summary>
    protected void UpDateVerticalVelocity()
    {
        verticalVelocity.Set(0, verticalSpeed, 0);
        var collisions = characterController.Move(verticalVelocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
    
    }
    /// <summary>
    /// 坡面检测
    /// </summary>
    /// <param name="characterVelosity"></param>
    protected Vector3 ResetVelocityOnSlop(Vector3 characterVelosity)
    {
      
        if (isOnGround && Physics.Raycast(transform.position, Vector3.down, out var groundHit,
            SlopDetectionLenth, whatIsGround, QueryTriggerInteraction.Ignore))
        {
            float newAngle = Vector3.Dot(Vector3.up, groundHit.normal);
            //不在天花板和角色在坡面上时
            if (newAngle > 0f && Vector3.Angle(Vector3.up, groundHit.normal) <= characterController.slopeLimit && verticalSpeed <= 0)
            {
                return Vector3.ProjectOnPlane(characterVelosity,groundHit.normal);
            }
        }
        return characterVelosity;

    }
    /// <summary>
    ///更新角色速度
    /// </summary>
   
    protected virtual void UpdateCharacterVelocity(Vector3 movement)
    {
        Vector3 dir = ResetVelocityOnSlop(movement);
        characterController.Move(dir * Mathf.Max(0f, moveMult));
    }

    public Animator GetAnimator()
    {   
        return characterAnimator;
    }

    /// <summary>
    /// 绘制地面检测
    /// </summary>
    private void OnDrawGizmos()
    {
        if (isOnGround)
        {
            Gizmos.color = Color.green;      
        }
        else
        { 
            Gizmos.color= Color.red;
        }
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y - GroundDetectionOffset, transform.position.z), GroundDetectionRadius);
    }

}
