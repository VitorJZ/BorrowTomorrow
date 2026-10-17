using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 剧情导演（策划 10.4 NarrativeDirector）：
    /// 根据稳定事件 ID 与前置标记触发一次性剧情。
    /// 一次性判定：entry 的 setFlags 已全部存在则视为已看过（策划 12.1 幂等）。
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class NarrativeDirector : MonoBehaviour
    {
        [SerializeField] private DialogueLibrary library;

        private IDialogueService dialogue;

        private void Awake()
        {
            ServiceRegistry.Register(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister(this);
        }

        private void Start()
        {
            if (library == null)
            {
                library = GetComponent<DialogueLibrary>();
            }
            ServiceRegistry.TryGet<IDialogueService>(out dialogue);
        }

        /// <summary>
        /// 播放一段剧情。返回 false 表示已看过或前置不满足（未播放）。
        /// </summary>
        public bool PlayStory(string entryId, DialoguePriority priority, System.Action onCompleted = null)
        {
            if (library == null || dialogue == null || GameSession.Instance == null)
            {
                onCompleted?.Invoke();
                return false;
            }
            DialogueEntry entry = library.Find(entryId);
            if (entry == null)
            {
                Debug.LogWarning($"[NarrativeDirector] 对白库中找不到 {entryId}。");
                onCompleted?.Invoke();
                return false;
            }

            StoryFlagStore flags = GameSession.Instance.StoryFlags;
            if (DialoguePlayer.Completed(entry) || !DialoguePlayer.RequirementsMet(entry.requiredFlags) || dialogue.IsPlaying)
            {
                onCompleted?.Invoke();
                return false; // 已读剧情标记保留，避免重复播放（策划 4.6）
            }
            dialogue.Play(entry, priority, onCompleted);
            return true;
        }

        /// <summary>entry 拥有标记且全部已写入 → 视为已看过。</summary>
        private static bool IsAlreadySeen(DialogueEntry entry, StoryFlagStore flags)
        {
            if (entry.setFlags == null || entry.setFlags.Length == 0)
            {
                return false;
            }
            bool allPresent = true;
            foreach (string flag in entry.setFlags)
            {
                if (!flags.HasFlag(flag))
                {
                    allPresent = false;
                }
            }
            return allPresent;
        }
    }
}
