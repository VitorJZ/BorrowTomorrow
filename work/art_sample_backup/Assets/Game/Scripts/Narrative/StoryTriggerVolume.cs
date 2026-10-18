using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 区域剧情触发器（策划 12.1 触发条件：进入区域）：
    /// 玩家进入 Trigger 时播放一段环境回声（如 3.3 阿黄回头）。
    /// 关键剧情必须自动触发；低优先级片段在忙碌时被 DialoguePlayer 跳过。
    /// </summary>
    public sealed class StoryTriggerVolume : MonoBehaviour
    {
        [Tooltip("触发的一次性标记：Trigger_<triggerId>（策划 12.2 已触发剧情ID）")]
        [SerializeField] private string triggerId = "trigger_unknown";

        [Tooltip("播放的对白 entryId")]
        [SerializeField] private string dialogueEntryId;

        [Tooltip("对白优先级（环境回声用 Ambient）")]
        [SerializeField] private DialoguePriority priority = DialoguePriority.Ambient;

        [Tooltip("是否只触发一次（策划 4.6：已读剧情标记保留，避免重复播放）")]
        [SerializeField] private bool once = true;

        [SerializeField] private LayerMask playerMask = ~0;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
            {
                return;
            }
            GameSession session = GameSession.Instance;
            if (session == null)
            {
                return;
            }
            string triggerFlag = $"Trigger_{triggerId}";
            if (once && session.StoryFlags.HasFlag(triggerFlag))
            {
                return; // 已触发过，保留已读状态（策划 4.6）
            }
            if (ServiceRegistry.TryGet(out NarrativeDirector director))
            {
                director.PlayStory(dialogueEntryId, priority, () =>
                {
                    if (once && session != null) session.StoryFlags.SetFlag(triggerFlag);
                });
            }
        }
    }
}
