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

        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null || !session.State.IsPlayerControlAllowed || session.ActiveRoom == null
                || dialogue == null || dialogue.IsPlaying) return;
            string entry = null, flag = null;
            switch (session.ActiveRoom.RoomId)
            {
                case "L00": entry = "intro_kid"; flag = StoryFlags.IntroPlayed; break;
                case "L01B": entry = "tree_marks"; flag = StoryFlags.HeightMarksSeen; break;
                case "L04": entry = "attic_tree"; flag = "AtticTreeSeen"; break;
                case "Epilogue": entry = "epilogue_arrival"; flag = "EpilogueArrived"; break;
            }
            if (entry != null && !session.StoryFlags.HasFlag(flag) && library != null && library.Find(entry) != null)
                PlayStory(entry, DialoguePriority.Story);
        }

        /// <summary>
        /// 播放一段剧情。返回 false 表示已看过或前置不满足（未播放）。
        /// </summary>
        public bool PlayStory(string entryId, DialoguePriority priority, System.Action onCompleted = null)
        {
            if (library == null || dialogue == null || GameSession.Instance == null)
            {
                return false;
            }
            DialogueEntry entry = library.Find(entryId);
            if (entry == null)
            {
                Debug.LogWarning($"[NarrativeDirector] 对白库中找不到 {entryId}。");
                return false;
            }

            StoryFlagStore flags = GameSession.Instance.StoryFlags;
            if (library.IsChainComplete(entry, flags))
            {
                return false; // 已读剧情标记保留，避免重复播放（策划 4.6）
            }
            dialogue.Play(entry, priority, onCompleted);
            return true;
        }

    }
}
