using System.Linq;
using BorrowTomorrow.Core.Data;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 对白库（策划 10.5：字幕与对白集中维护，禁止脚本内硬编码剧情文本）。
    /// 挂在系统根上，收集全部 DialogueEntry 资产供按 entryId 检索。
    /// </summary>
    public sealed class DialogueLibrary : MonoBehaviour
    {
        [Tooltip("全部对白条目（按 entryId 检索，nextId 串联）")]
        [SerializeField] private DialogueEntry[] entries = new DialogueEntry[0];
        public System.Collections.Generic.IReadOnlyList<DialogueEntry> Entries => entries;

        private void Awake()
        {
            Core.ServiceRegistry.Register(this);
        }

        private void OnDestroy()
        {
            Core.ServiceRegistry.Unregister(this);
        }

        public DialogueEntry Find(string entryId)
        {
            if (string.IsNullOrEmpty(entryId))
            {
                return null;
            }
            return entries.FirstOrDefault(e => e != null && e.entryId == entryId);
        }
    }
}
