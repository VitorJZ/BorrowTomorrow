using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 叙事调查物（策划 3.1 叙事渠道 D / 10.4）：
    /// 玩家按 E 调查后播放对白（校服、照片、账本、暗格等）。
    /// 重要剧情必须自动触发；可选调查只增加细节（策划 3.1）。
    /// </summary>
    public sealed class NarrativeInteractable : MonoBehaviour, IInteractable
    {
        [Header("标识")]
        [SerializeField] private string interactId = "interact_unknown";

        [Header("内容")]
        [SerializeField] private string prompt = "查看";

        [Tooltip("调查触发的对白条目")]
        [SerializeField] private DialogueEntry entry;

        [Tooltip("对白优先级：关键剧情用 Story，环境细节用 Ambient")]
        [SerializeField] private DialoguePriority priority = DialoguePriority.Story;

        [Tooltip("关键剧情调查完成后是否可重复查看（可选项设 true）")]
        [SerializeField] private bool replayable = false;

        public string InteractId => interactId;

        public string InteractPrompt => prompt;

        public bool CanInteract => entry != null && !IsCompleted;

        private bool IsCompleted
        {
            get
            {
                var session = GameSession.Instance;
                return !replayable && entry != null && session != null
                    && ServiceRegistry.TryGet<DialogueLibrary>(out var library)
                    && library.IsChainComplete(entry, session.StoryFlags);
            }
        }

        public bool Interact()
        {
            if (entry == null || IsCompleted)
            {
                return false;
            }
            if (ServiceRegistry.TryGet<IDialogueService>(out var dialogue))
            {
                dialogue.Play(entry, priority);
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        /// <summary>编辑器脚手架专用：直接注入对白条目（直接字段赋值 + SetDirty；运行时不要调用）。</summary>
        public void EditorConfigure(DialogueEntry dialogueEntry, DialoguePriority dialoguePriority, bool replayableFlag)
        {
            entry = dialogueEntry;
            priority = dialoguePriority;
            replayable = replayableFlag;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.85f, 0.3f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, interactId);
#endif
        }
    }
}
