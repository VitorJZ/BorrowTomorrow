using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.TimeBoundary
{
    /// <summary>
    /// 时间边界控制器（策划 10.4 BoundaryController + 11.4 提交算法）：
    /// 持有已提交 B 与候选 B，只向安全系统提交转换请求。
    /// 每个房间一个实例；激活房间的实例注册为全局 IBoundaryContext。
    /// 渲染、音频和 HUD 只读取已提交 B，不读取未被批准的鼠标目标值（策划 11.4）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoundaryController : MonoBehaviour, IBoundaryContext
    {
        [Tooltip("键盘 Q/R 连续调整速度（世界单位/秒）")]
        [SerializeField] private float keyboardAdjustSpeed = 4f;

        private TemporalObjectRegistry registry;
        private IPlayerContext player;
        private IInputSource input;

        private float committedB;
        private float rangeMin;
        private float rangeMax;
        private string roomId = "room";

        private bool mouseDragging;
        private bool keyboardHeld;
        private float pendingTarget;

        /// <summary>统一受阻提示（策划 4.5）。</summary>
        private const string BlockedMessage = "先到稳固的地方。";

        public float CommittedBoundary => committedB;

        public float RangeMin => rangeMin;

        public float RangeMax => rangeMax;

        public string RoomId => roomId;

        public event System.Action<float> OnBoundaryCommitted;

        public event System.Action<string> OnAdjustBlocked;

        /// <summary>调整开始（进入 AdjustingBoundary，角色站定）。</summary>
        public event System.Action OnAdjustStarted;

        /// <summary>调整结束（恢复自由移动）。</summary>
        public event System.Action OnAdjustEnded;

        public bool IsAdjusting => mouseDragging || keyboardHeld;

        private void Awake()
        {
            registry = GetComponentInParent<TemporalObjectRegistry>();
            if (registry == null)
            {
                registry = GetComponent<TemporalObjectRegistry>();
            }
            if (registry == null)
            {
                Debug.LogError($"[BoundaryController] {name} 缺少 TemporalObjectRegistry。");
            }
        }

        private void Start()
        {
            ServiceRegistry.TryGet<IInputSource>(out input);
        }

        private void Update()
        {
            if (GameSession.Instance == null || GameSession.Instance.State.Phase != GamePhase.Playing)
            {
                ForceEndAdjust();
                return;
            }

            // 键盘 Q/R 调整（策划 5.1）。
            if (input != null && GameSession.Instance.State.IsBoundaryAdjustAllowed)
            {
                float nudge = input.BoundaryNudgeDirection;
                if (Mathf.Abs(nudge) > 0.01f)
                {
                    if (TryBeginAdjust(ref keyboardHeld))
                    {
                        pendingTarget = committedB + nudge * keyboardAdjustSpeed * Time.deltaTime;
                        ProcessAdjustment(pendingTarget);
                    }
                }
                else if (keyboardHeld)
                {
                    keyboardHeld = false;
                    MaybeEndAdjust();
                }
            }
        }

        /// <summary>鼠标把手按下时调用（策划 5.3）。</summary>
        public void BeginMouseDrag()
        {
            if (TryBeginAdjust(ref mouseDragging))
            {
                pendingTarget = committedB;
            }
        }

        /// <summary>鼠标把手释放时调用。</summary>
        public void EndMouseDrag()
        {
            mouseDragging = false;
            MaybeEndAdjust();
        }

        private bool TryBeginAdjust(ref bool sourceFlag)
        {
            if (sourceFlag)
            {
                return true;
            }
            EnsurePlayerResolved();
            GameSession session = GameSession.Instance;
            if (session == null || !session.State.IsBoundaryAdjustAllowed)
            {
                return false;
            }
            // 拖动只能在落地或站稳的情况下开始，爬梯和腾空时不能开始（策划 4.5）。
            if (player == null || !player.CanBeginBoundaryAdjustSafe())
            {
                OnAdjustBlocked?.Invoke(BlockedMessage);
                NotifyBlockedHud(BlockedMessage);
                return false;
            }
            sourceFlag = true;
            session.State.SetSubState(PlayingSubState.AdjustingBoundary);
            OnAdjustStarted?.Invoke();
            return true;
        }

        private void MaybeEndAdjust()
        {
            if (!IsAdjusting)
            {
                GameSession session = GameSession.Instance;
                if (session != null && session.State.SubState == PlayingSubState.AdjustingBoundary)
                {
                    session.State.SetSubState(PlayingSubState.Free);
                }
                OnAdjustEnded?.Invoke();
            }
        }

        private void ForceEndAdjust()
        {
            mouseDragging = false;
            keyboardHeld = false;
            GameSession session = GameSession.Instance;
            if (session != null && session.State.SubState == PlayingSubState.AdjustingBoundary)
            {
                session.State.SetSubState(PlayingSubState.Free);
            }
            OnAdjustEnded?.Invoke();
        }

        /// <summary>
        /// IBoundaryContext.RequestAdjustment：来自鼠标把手的目标位置。
        /// 内部逐阈值安全校验后才提交（策划 11.4）。
        /// </summary>
        public void RequestAdjustment(float targetBoundaryX)
        {
            if (!mouseDragging)
            {
                return; // 只受理拖动中的把手输入，防止误触瞬移（策划 5.3）。
            }
            pendingTarget = targetBoundaryX;
            ProcessAdjustment(targetBoundaryX);
        }

        /// <summary>键盘单步调整保留接口（策划 5.1 Q/R），由 Update 内部驱动。</summary>
        public void Nudge(float directionSign)
        {
            // 保留给手柄或 UI 复用；键盘路径已在 Update 内联处理。
            if (Mathf.Abs(directionSign) < 0.01f)
            {
                return;
            }
            if (TryBeginAdjust(ref keyboardHeld))
            {
                pendingTarget = committedB + directionSign * keyboardAdjustSpeed * Time.deltaTime;
                ProcessAdjustment(pendingTarget);
            }
        }

        /// <summary>
        /// 提交算法主体（策划 11.4）：
        /// 1) 生成候选；2) 跨越阈值排序；3) 同阈值成组；
        /// 4) 检查覆盖与支撑撤除；5) 不安全则停在阈值前并反馈；6) 提交并广播。
        /// </summary>
        private void ProcessAdjustment(float targetX)
        {
            if (registry == null)
            {
                return;
            }
            float clamped = Mathf.Clamp(targetX, rangeMin, rangeMax);
            if (Mathf.Abs(clamped - committedB) < BoundaryChangePlan.Epsilon * 0.5f)
            {
                return;
            }
            EnsurePlayerResolved();

            BoundaryChangePlan plan = registry.BuildPlan(committedB, clamped);
            foreach (BoundaryChangePlan.ThresholdStep step in plan.Steps)
            {
                var switching = step.GetPayloads<TemporalObject>();
                SafetyBlockReason reason = TemporalSafetyResolver.Evaluate(switching, step.TargetEra, player);
                if (reason != SafetyBlockReason.None)
                {
                    // 边界停在切换阈值之前，标记反馈（策划 4.5）。
                    CommitBoundary(step.SafeBoundaryBefore);
                    string message = TemporalSafetyResolver.ToDisplayText(reason);
                    OnAdjustBlocked?.Invoke(message);
                    NotifyBlockedHud(message);
                    return;
                }

                // 单次提交中处理完全部物体，不让画面处于半套新碰撞状态（策划 11.3）。
                foreach (TemporalObject obj in switching)
                {
                    obj.ApplyEra(step.TargetEra);
                }
                CommitBoundary(step.BoundaryAfter);
            }
            CommitBoundary(clamped);
        }

        private void CommitBoundary(float value)
        {
            committedB = value;
            OnBoundaryCommitted?.Invoke(committedB);
        }

        private void NotifyBlockedHud(string message)
        {
            if (ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                hud.ShowBoundaryBlocked(message);
            }
        }

        private void EnsurePlayerResolved()
        {
            if (player == null || !ServiceRegistry.TryGet<IPlayerContext>(out player))
            {
                player = null;
            }
        }

        /// <summary>LevelRoot 激活房间时调用（策划 4.2：跨房间不共享数值）。</summary>
        public void ConfigureRoom(float newRangeMin, float newRangeMax, float initialBoundary, string newRoomId)
        {
            rangeMin = newRangeMin;
            rangeMax = newRangeMax;
            roomId = newRoomId;
            committedB = Mathf.Clamp(initialBoundary, rangeMin, rangeMax);
            ForceEndAdjust();
            if (registry != null)
            {
                registry.ResetToBoundary(committedB, immediate: true);
            }
            OnBoundaryCommitted?.Invoke(committedB);
        }

        /// <summary>激活本房间：注册为全局边界服务。</summary>
        public void RegisterAsActive()
        {
            ServiceRegistry.Register<IBoundaryContext>(this);
        }

        /// <summary>存档恢复用：直接设置已提交边界并立即应用年代状态（跳过安全校验，策划 12.2）。</summary>
        public void RestoreBoundary(float boundaryX)
        {
            committedB = Mathf.Clamp(boundaryX, rangeMin, rangeMax);
            if (registry != null)
            {
                registry.ResetToBoundary(committedB, immediate: true);
            }
            OnBoundaryCommitted?.Invoke(committedB);
        }

        public Era EraAtAnchor(float anchorX)
        {
            return anchorX < committedB ? Era.Past : Era.Present;
        }
    }

    /// <summary>
    /// IPlayerContext 的边界安全扩展：暴露“能否开始拖动”的完整条件。
    /// 放在本模块避免 Core 依赖 Player（依赖倒置补充面）。
    /// </summary>
    internal static class PlayerContextSafetyExtension
    {
        public static bool CanBeginBoundaryAdjustSafe(this IPlayerContext player)
        {
            return player != null && player.IsPresent && player.IsGrounded && !player.IsClimbingLadder;
        }
    }
}
