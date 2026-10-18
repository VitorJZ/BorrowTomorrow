using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 关卡出口（策划 7.7）：明确显示门或楼梯，E 确认转场，不因接近边缘误切关。
    /// 支持两种模式：同场景房间转场（如 L01 水渠房 → 树房）或推进下一关。
    /// 出口条件未满足时播放主角自语提示，不使用无解释空气墙。
    /// </summary>
    public sealed class ExitDoor : MonoBehaviour, IInteractable
    {
        public enum TransitionMode
        {
            SameSceneRoom,
            NextLevel,
        }

        [Header("模式")]
        [SerializeField] private TransitionMode mode = TransitionMode.NextLevel;

        [Header("同场景目标")]
        [SerializeField] private LevelRoot targetRoom;
        [SerializeField] private string targetSpawnId;

        [Header("通用")]
        [Tooltip("交互提示（如“进院”“上楼”）")]
        [SerializeField] private string prompt = "开门";

        [Tooltip("本出口独立条件；为空则使用关卡定义的出口条件")]
        [SerializeField] private string[] requiredFlagsOverride;

        [Tooltip("条件未满足时的自语文本；为空则使用关卡定义")]
        [SerializeField] private string lockedHintOverride;

        [Tooltip("条件未满足时的对白 id；为空则直接用自语文本")]
        [SerializeField] private DialogueEntry lockedDialogue;
        [SerializeField] private bool ignoreLevelConditions;

        public string InteractId => $"exit_{gameObject.scene.name}_{transform.root.name}_{GetInstanceID()}";

        public string InteractPrompt => AreConditionsMet ? prompt : $"{prompt}（还差一点）";

        public bool CanInteract => true; // 出口始终可调查，锁定时反馈提示（策划 7.7）

        public bool AreConditionsMet
        {
            get
            {
                if (ignoreLevelConditions) return true;
                if (requiredFlagsOverride != null && requiredFlagsOverride.Length > 0)
                {
                    StoryFlagStore flags = GameSession.Instance != null ? GameSession.Instance.StoryFlags : null;
                    if (flags == null)
                    {
                        return false;
                    }
                    foreach (string f in requiredFlagsOverride)
                    {
                        if (!flags.HasFlag(f))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return ServiceRegistry.TryGet<ILevelContext>(out var level) && level.ExitUnlocked;
            }
        }

        public bool Interact()
        {
            if (!AreConditionsMet)
            {
                PlayLockedHint();
                return true; // 消耗输入但不开门
            }

            // 开门音效（稳定 id，由 B 组在 SfxLibrary 登记，策划 09 声音清单“门轴”）。
            if (ServiceRegistry.TryGet<IAudioService>(out var audio))
            {
                audio.PlaySfx("door_open", transform.position);
            }

            switch (mode)
            {
                case TransitionMode.SameSceneRoom:
                    GameSession.Instance.TransitionToRoom(targetRoom, targetSpawnId);
                    break;
                case TransitionMode.NextLevel:
                    if (GameSession.Instance != null)
                    {
                        GameSession.Instance.CompleteLevel();
                    }
                    break;
            }
            return true;
        }

        private void PlayLockedHint()
        {
            if (ServiceRegistry.TryGet<IDialogueService>(out var dialogue))
            {
                if (lockedDialogue != null)
                {
                    dialogue.Play(lockedDialogue, DialoguePriority.Objective);
                    return;
                }
                string hint = !string.IsNullOrEmpty(lockedHintOverride)
                    ? lockedHintOverride
                    : (ServiceRegistry.TryGet<ILevelContext>(out var level) ? level.ExitLockedHint : null);
                if (!string.IsNullOrEmpty(hint) && ServiceRegistry.TryGet<IAudioService>(out var audio))
                {
                    // 直接以 HUD 字幕呈现一句自语，不强制对白资产（P0 快速路径）。
                    if (ServiceRegistry.TryGet<IHudService>(out var hud))
                    {
                        hud.ShowSubtitle("主角", hint);
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = AreConditionsMetStatic() ? new Color(0.2f, 0.9f, 0.3f, 0.7f) : new Color(0.9f, 0.5f, 0.2f, 0.7f);
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2f, 0.1f));
        }

        private bool AreConditionsMetStatic() => requiredFlagsOverride == null || requiredFlagsOverride.Length == 0;
    }
}
