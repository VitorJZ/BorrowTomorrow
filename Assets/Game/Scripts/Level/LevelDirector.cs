using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 关卡导演（策划 10.4 LevelDirector）：本关目标、出口条件、提示阶段。
    /// 挂在活动房间根下，注册为全局 ILevelContext。
    /// 支持两种通关方式：出口门（ExitDoor）或剧情标记触发（completeLevelOnFlag，如 L04 暗格）。
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public sealed class LevelDirector : UnityEngine.MonoBehaviour, ILevelContext
    {
        [UnityEngine.Tooltip("写入此剧情标记时自动通关（如 L04 的 BoxLocated）；留空表示靠出口门通关")]
        [UnityEngine.SerializeField]
        private string completeLevelOnFlag;

        public LevelDefinition Definition { get; private set; }

        private bool completionConsumed;

        /// <summary>出口条件未满足时的自语文本（策划 7.7：不使用无解释空气墙）。</summary>
        public string ExitLockedHint => Definition != null ? Definition.exitLockedHint : string.Empty;

        public bool ExitUnlocked
        {
            get
            {
                if (Definition == null || Definition.exitRequiredFlags == null)
                {
                    return true;
                }
                StoryFlagStore flags = GameSession.Instance != null ? GameSession.Instance.StoryFlags : null;
                if (flags == null)
                {
                    return false;
                }
                return Definition.exitRequiredFlags.All(f => flags.HasFlag(f));
            }
        }

        private void Start()
        {
            LevelRoot room = GetComponentInParent<LevelRoot>();
            Definition = room != null ? room.Definition : null;
            if (room != null && room.IsActiveRoom) ActivateForRoom(Definition);
        }

        public void ActivateForRoom(LevelDefinition definition)
        {
            Definition = definition;
            ServiceRegistry.Register<ILevelContext>(this);
        }

        public void DeactivateForRoom()
        {
            ServiceRegistry.Unregister<ILevelContext>(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<ILevelContext>(this);
        }

        private void Update()
        {
            // 标记触发通关：只在玩家自由行动时执行，保证打断的是对白收尾而不是演出本身
            //（DialoguePlayer 在整链结束后才回到 Free 子状态）。
            if (completionConsumed || string.IsNullOrEmpty(completeLevelOnFlag))
            {
                return;
            }
            LevelRoot room = GetComponentInParent<LevelRoot>();
            if (room == null || !room.IsActiveRoom) return;
            GameSession session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing)
            {
                return;
            }
            if (session.State.SubState != PlayingSubState.Free)
            {
                return;
            }
            if (session.StoryFlags.HasFlag(completeLevelOnFlag))
            {
                completionConsumed = true;
                CompleteLevel();
            }
        }

        /// <summary>关卡完成（末关出口触发后调用）：保存并推进流程。</summary>
        public void CompleteLevel()
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.CompleteLevel();
            }
        }
    }
}
