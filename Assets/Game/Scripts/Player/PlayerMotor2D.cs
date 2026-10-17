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

        [Tooltip("攀爬中允许的横向微调速度（便于爬出梯顶落到平台）")]
        [SerializeField] private float climbHorizontalSpeed = 1.2f;

        [Header("容错（策划 6.2）")]
        [Tooltip("离地后仍可起跳的宽容时间（秒）")]
        [SerializeField] private float coyoteTime = 0.1f;

        [Tooltip("跳跃预输入窗口（秒）")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("检测")]
        [Tooltip("可站立表面层（Ground + Temporal）")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("脚下探测盒中心相对胶囊底部中心再向下的偏移")]
        [SerializeField] private float groundProbeDrop = 0.03f;

        [Tooltip("脚下探测盒尺寸")]
        [SerializeField] private Vector2 groundProbeSize = new Vector2(0.5f, 0.12f);

        [Header("音效（稳定 id，由 B 组在 SfxLibrary 登记，策划 09）")]
        [SerializeField] private string footstepSfxId = "player_footstep";
        [SerializeField] private float footstepInterval = 0.34f;
        [SerializeField] private string landSfxId = "player_land";
        [SerializeField] private string jumpSfxId = "player_jump";

        private Rigidbody2D body;
        private CapsuleCollider2D capsule;

        private IInputSource input;
        private ContactFilter2D groundFilter;
        private Collider2D[] groundProbeResults;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private float footstepTimer;
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
            // 地面探测不命中触发器：避免把梯子 Trigger 当成支撑（策划 11.3）。
            groundFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = groundMask,
            };
            groundProbeResults = new Collider2D[1];
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
                UpdateClimbing(controlAllowed, climb, move);
            }
            else
            {
                UpdateWalking(controlAllowed, move, climb);
            }

            UpdateFootsteps(controlAllowed);

            if (wasGrounded != IsGrounded)
            {
                if (IsGrounded)
                {
                    Landed?.Invoke();
                    PlaySfx(landSfxId);
                }
                wasGrounded = IsGrounded;
            }
        }

        private void UpdateWalking(bool controlAllowed, float move, float climb)
        {
            // 水平移动。
            body.velocity = new Vector2(controlAllowed ? move * moveSpeed : 0f, body.velocity.y);

            // 进入攀爬：在梯子区内按上下键（策划 11.3：梯子用 Trigger 与攀爬状态控制）。
            if (controlAllowed && !IsClimbingLadder && ladderZoneCount > 0 && Mathf.Abs(climb) > 0.1f)
            {
                IsClimbingLadder = true;
                body.gravityScale = 0f;
                body.velocity = Vector2.zero;
                return;
            }

            // 跳跃：需要落地或离地宽容（策划 4.5：腾空时不能开始拖动，但主动跳跃允许）。
            if (controlAllowed && jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                body.velocity = new Vector2(body.velocity.x, jumpVelocity);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                PlaySfx(jumpSfxId);
            }
        }

        private void UpdateClimbing(bool controlAllowed, float climb, float move)
        {
            // 攀爬中暂停重力（策划 11.3）；横向允许微调，方便爬出梯顶落到相邻平台。
            body.gravityScale = 0f;
            float vertical = controlAllowed ? climb * climbSpeed : 0f;
            float horizontal = controlAllowed ? move * climbHorizontalSpeed : 0f;
            body.velocity = new Vector2(horizontal, vertical);

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
            // 探测盒以胶囊底部为基准（origin 是脚底，不是胶囊中心）。
            Vector2 capsuleCenter = (Vector2)transform.position + capsule.offset;
            Vector2 origin = capsuleCenter + Vector2.down * (capsule.size.y * 0.5f + groundProbeDrop - groundProbeSize.y * 0.5f);
            // 单结果查询没有 ContactFilter 重载，用结果数组 + 不命中触发器的过滤器（策划 11.3：梯子 Trigger 不是支撑）。
            groundProbeResults[0] = null;
            int hitCount = Physics2D.OverlapBox(origin, groundProbeSize, 0f, groundFilter, groundProbeResults);
            Collider2D hit = hitCount > 0 ? groundProbeResults[0] : null;
            IsGrounded = hit != null;
            GroundCollider = hit;
        }

        private void UpdateFootsteps(bool controlAllowed)
        {
            if (!controlAllowed || !IsGrounded || Mathf.Abs(body.velocity.x) < 0.2f)
            {
                footstepTimer = 0.12f; // 起步后很快响第一声
                return;
            }
            footstepTimer -= Time.fixedDeltaTime;
            if (footstepTimer <= 0f)
            {
                footstepTimer = footstepInterval;
                PlaySfx(footstepSfxId);
            }
        }

        private static void PlaySfx(string sfxId)
        {
            if (string.IsNullOrEmpty(sfxId))
            {
                return;
            }
            if (ServiceRegistry.TryGet<IAudioService>(out var audio))
            {
                audio.PlaySfx(sfxId);
            }
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
            footstepTimer = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Vector2 capsuleCenter = (Vector2)transform.position + capsule.offset;
            Vector2 origin = capsuleCenter + Vector2.down * (capsule.size.y * 0.5f + groundProbeDrop - groundProbeSize.y * 0.5f);
            Gizmos.DrawWireCube(origin, groundProbeSize);
        }
    }
}
