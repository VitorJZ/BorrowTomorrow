using BorrowTomorrow.Core;
using NUnit.Framework;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 故事标记幂等性测试（策划 15.5）。
    /// </summary>
    public sealed class StoryFlagStoreTests
    {
        [Test]
        public void SetFlag_SecondTimeDoesNotRaiseEvent()
        {
            var store = new StoryFlagStore();
            int raised = 0;
            store.FlagAdded += _ => raised++;

            bool first = store.SetFlag(StoryFlags.IntroPlayed);
            bool second = store.SetFlag(StoryFlags.IntroPlayed);

            Assert.IsTrue(first, "首次写入应成功");
            Assert.IsFalse(second, "重复写入应被幂等忽略");
            Assert.AreEqual(1, raised, "事件只应触发一次");
            Assert.IsTrue(store.HasFlag(StoryFlags.IntroPlayed));
        }

        [Test]
        public void SetFlags_BatchWrite()
        {
            var store = new StoryFlagStore();
            bool any = store.SetFlags(new[] { StoryFlags.UniformRead, StoryFlags.PhotoRevealed });
            Assert.IsTrue(any);
            Assert.IsTrue(store.HasFlag(StoryFlags.UniformRead));
            Assert.IsTrue(store.HasFlag(StoryFlags.PhotoRevealed));
        }

        [Test]
        public void RestoreFrom_ReplacesAllFlags()
        {
            var store = new StoryFlagStore();
            store.SetFlag(StoryFlags.IntroPlayed);
            store.RestoreFrom(new[] { StoryFlags.BoxLocated });
            Assert.IsFalse(store.HasFlag(StoryFlags.IntroPlayed), "恢复应整体替换而不是合并");
            Assert.IsTrue(store.HasFlag(StoryFlags.BoxLocated));
        }

        [Test]
        public void EmptyOrNullFlag_IsIgnored()
        {
            var store = new StoryFlagStore();
            Assert.IsFalse(store.SetFlag(null));
            Assert.IsFalse(store.SetFlag(string.Empty));
            Assert.IsFalse(store.HasFlag(null));
        }
    }
}
