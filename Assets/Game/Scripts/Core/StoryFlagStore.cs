using System;
using System.Collections.Generic;

namespace BorrowTomorrow.Core
{
    /// <summary>
    /// 剧情标记存储。SetFlag 幂等：重复写入同一标记不重复触发事件，
    /// 满足策划方案 15.5 对“故事标记幂等性”的自动测试要求。
    /// </summary>
    public sealed class StoryFlagStore
    {
        private readonly HashSet<string> flags = new HashSet<string>();

        /// <summary>仅在新标记首次写入时触发。</summary>
        public event Action<string> FlagAdded;

        public IReadOnlyCollection<string> AllFlags => flags;

        public bool HasFlag(string flagId)
        {
            return !string.IsNullOrEmpty(flagId) && flags.Contains(flagId);
        }

        /// <summary>写入标记；已存在时返回 false 且不触发事件（幂等）。</summary>
        public bool SetFlag(string flagId)
        {
            if (string.IsNullOrEmpty(flagId) || flags.Contains(flagId))
            {
                return false;
            }
            flags.Add(flagId);
            FlagAdded?.Invoke(flagId);
            return true;
        }

        public bool SetFlags(IEnumerable<string> flagIds)
        {
            bool anyAdded = false;
            if (flagIds == null)
            {
                return false;
            }
            foreach (string id in flagIds)
            {
                anyAdded |= SetFlag(id);
            }
            return anyAdded;
        }

        public void Clear()
        {
            flags.Clear();
        }

        public void RestoreFrom(IEnumerable<string> flagIds)
        {
            Clear();
            if (flagIds == null)
            {
                return;
            }
            foreach (string id in flagIds)
            {
                flags.Add(id);
            }
        }
    }
}
