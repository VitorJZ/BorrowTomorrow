using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Player
{
    /// <summary>
    /// 2D 角色运动（策划 10.4 PlayerMotor2D）：
    /// Rigidbody2D 运动、地面检测、跳跃、梯子状态。
    /// 运动参数为策划 6.2 的原型起点，实际值以控制器测试为准。
    /// 依据策划 11.3：冻结 Z 旋转；运动指令在 FixedUpdate 阶段执行；
    /// 不混用 Transform 位移与刚体位移。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [Header("移动参数（策划 6.2 原型值）")]
        [Tooltip("水平移动速度（单位/秒）")]
        [SerializeField] private float moveSpeed = 3.5f;

        [Tooltip("起跳初速度（单位/秒）")]
        [SerializeField] private float jumpVelocity = 5.8f;

        [Tooltip("等效下落加速度（单位/秒²），将换算为 gravityScale")]
        [SerializeField] private float fallAcceleration = 18f;

        [Tooltip("梯子攀爬速度")]
        [SerializeField] private float climbSpeed = 3f;

        [Header("容错（策划 6.2）")]
        [Tooltip("离地后仍可起跳的宽容时间（秒）")]
        [SerializeField] private float coyoteTime = 0.1f;

        [Tooltip("跳跃预输入窗口（秒）")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("检测")]
        [Tooltip("可站立表面层")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("脚下检测盒中心相对胶囊底部中心的偏移")]
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.05f);

        [Tooltip("脚下检测盒尺寸")]
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.15f);

        private Rigidbody2D body;
        private CapsuleCollider2D capsule;

        private IInputSource input;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private int ladderZoneCount;
        private bool wasGrounded;
        private float cachedGravityScale;

        /// <summary>是否落地（本物理步）。</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>当前脚下地面碰撞体；腾空为 null（供支撑撤除校验使用）。</summary>
        public Collider2D GroundCollider { get; private set; }

        /// <summary>是否处于梯子攀爬状态。</summary>
        public bool IsClimbingLadder { get; private set; }

        /// <summary>落地瞬间触发（音效/尘土等订阅）。</summary>
        public event System.Action Landed;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            body.freezeRotation = true; // 冻结 Z 旋转（策划 11.3）
            cachedGravityScale = fallAcceleration / Mathf.Abs(Physics2D.gravity.y);
            body.gravityScale = cachedGravityScale;
        }

        private BorrowTomorrow.Player.PlayerContext registeredContext;

        private void OnEnable()
        {
            // 缓存唯一实例，保证 OnDisable 能正确注销同一引用。
            registeredContext = new PlayerContext(this);
            ServiceRegistry.Register<IPlayerContext>(registeredContext);
        }

        private void OnDisable()
        {
            if (registeredContext != null)
            {
                ServiceRegistry.Unregister<IPlayerContext>(registeredContext);
                registeredContext = null;
            }
        }

        private void Start()
        {
            if (ServiceRegistry.TryGet<IInputSource>(out var source))
            {
                input = source;
            }
            else
            {
                Debug.LogError("[PlayerMotor2D] IInputSource 未注册，玩家无法行动。");
            }
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }
            // 跳跃预输入与离地宽容计时（策划 6.2）。
            if (input.JumpPressedThisFrame && IsControlAllowed)
            {
                jumpBufferTimer = jumpBufferTime;
            }
            coyoteTimer = IsGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (input == null)
            {
                return;
            }
            bool controlAllowed = IsControlAllowed;
            float move = controlAllowed ? input.MoveAxis : 0f;
            float climb = controlAllowed ? input.ClimbAxis : 0f;

            DetectGround();

            if (IsClimbingLadder)
            {
                UpdateClimbing(controlAllowed, climb);
            }
            else
            {
                UpdateWalking(controlAllowed, move);
            }

            if (wasGrounded != IsGrounded)
            {
                if (IsGrounded)
                {
                    Landed?.Invoke();
                }
                wasGrounded = IsGrounded;
            }
        }

        private void UpdateWalking(bool controlAllowed, float move)
        {
            // 水平移动。
            body.velocity = new Vector2(controlAllowed ? move * moveSpeed : 0f, body.velocity.y);

            // 跳跃：需要落地或离地宽容（策划 4.5：拖动/腾空时不能开始，但这里是主动跳跃）。
            if (controlAllowed && jumpBufferTimer > 0f && coyoteTimer > 0f && !IsClimbingLadder)
            {
                body.velocity = new Vector2(body.velocity.x, jumpVelocity);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
            }
        }

        private void UpdateClimbing(bool controlAllowed, float climb)
        {
            // 攀爬中暂停重力（策划 11.3），锁水平位移防止脱轨。
            body.gravityScale = 0f;
            float vertical = controlAllowed ? climb * climbSpeed : 0f;
            body.velocity = new Vector2(0f, vertical);

            // 跳离梯子或脱离梯区后恢复正常重力。
            bool jumpOff = controlAllowed && jumpBufferTimer > 0f;
            if (jumpOff || ladderZoneCount <= 0)
            {
                ExitLadder(jumpOff);
            }
        }

        private void ExitLadder(bool withJump)
        {
            IsClimbingLadder = false;
            body.gravityScale = cachedGravityScale;
            if (withJump)
            {
                body.velocity = new Vector2(body.velocity.x, jumpVelocity * 0.8f);
                jumpBufferTimer = 0f;
            }
        }

        private void DetectGround()
        {
            if (IsClimbingLadder)
            {
                IsGrounded = false;
                GroundCollider = null;
                return;
            }
            Vector2 origin = (Vector2)transform.TransformPoint(capsule.offset) + groundCheckOffset;
            Collider2D hit = Physics2D.OverlapBox(origin, groundCheckSize, 0f, groundMask);
            IsGrounded = hit != null;
            GroundCollider = hit;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<LadderZone>() != null)
            {
                ladderZoneCount++;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<LadderZone>() != null)
            {
                ladderZoneCount = Mathf.Max(0, ladderZoneCount - 1);
                if (IsClimbingLadder && ladderZoneCount <= 0)
                {
                    ExitLadder(false);
                }
            }
        }

        private bool IsControlAllowed
        {
            get
            {
                if (ServiceRegistry.TryGet(out IInputSource _) == false)
                {
                    return false;
                }
                return GameSession.Instance != null
                    && GameSession.Instance.State.IsPlayerControlAllowed
                    && !GameSession.Instance.State.IsPaused;
            }
        }

        /// <summary>
        /// 是否可以开始拖动时间边界：必须落地或站稳，
        /// 攀爬与腾空时不能开始（策划 4.5）。另由状态机保证子状态合法。
        /// </summary>
        public bool CanBeginBoundaryAdjust => IsGrounded && !IsClimbingLadder && IsControlAllowed;

        /// <summary>存档/检查点恢复用：直接放置（策划 12.2：先恢复环境再放人）。</summary>
        public void TeleportTo(Vector2 position)
        {
            body.position = position;
            body.velocity = Vector2.zero;
            IsClimbingLadder = false;
            body.gravityScale = cachedGravityScale;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Vector2 origin = transform.position + (Vector3)groundCheckOffset;
            Gizmos.DrawWireCube(origin, groundCheckSize);
        }
    }
}
