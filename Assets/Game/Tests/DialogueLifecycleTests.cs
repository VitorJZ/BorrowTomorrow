using System.Collections.Generic;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace BorrowTomorrow.Tests
{
    public sealed class DialogueLifecycleTests
    {
        private GameObject host;
        private GameSession session;
        private DialoguePlayer dialogue;
        private DialogueLibrary library;
        private readonly List<DialogueEntry> entries = new List<DialogueEntry>();
        private readonly List<NarrativeAction> created = new List<NarrativeAction>();
        [SetUp] public void Setup()
        {
            host = new GameObject("DialogueTest");
            session = host.AddComponent<GameSession>();
            Invoke(session, "Awake");
            session.State.SetPhase(GamePhase.MainMenu);
            session.State.SetPhase(GamePhase.Loading);
            session.State.SetPhase(GamePhase.Playing);
            dialogue = host.AddComponent<DialoguePlayer>();
            Invoke(dialogue, "Awake");
            library = host.AddComponent<DialogueLibrary>();
            ServiceRegistry.Register(library);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(host);
            foreach (var entry in entries) Object.DestroyImmediate(entry);
            entries.Clear();
            foreach (var action in created) Object.DestroyImmediate(action);
            created.Clear();
        }
        private static void Invoke(object o, string method) => o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
        private DialogueEntry Entry(string id)
        {
            var entry = ScriptableObject.CreateInstance<DialogueEntry>();
            entry.entryId = id;
            entry.completionFlags = new[] { id };
            entries.Add(entry);
            return entry;
        }
        [Test] public void RejectedDialogue_DoesNotReportCompletion()
        {
            var active = Entry("active"); var rejected = Entry("rejected");
            bool completed = false;
            dialogue.Play(active, DialoguePriority.Reveal);
            dialogue.Play(rejected, DialoguePriority.Story, () => completed = true);
            Assert.IsFalse(completed);
            Assert.IsFalse(session.StoryFlags.HasFlag("rejected"));
            Assert.IsTrue(dialogue.IsPlaying);
        }
        [Test] public void InterruptedDialogue_DoesNotSetUnreadFlagOrComplete()
        {
            bool completed = false;
            dialogue.Play(Entry("interrupted"), DialoguePriority.Ambient, () => completed = true);
            dialogue.Play(Entry("reveal"), DialoguePriority.Reveal);
            Assert.IsFalse(completed);
            Assert.IsFalse(session.StoryFlags.HasFlag("interrupted"));
        }
        [Test] public void MissingPrerequisite_DoesNotReportCompletion()
        {
            var entry = Entry("blocked"); entry.requiredFlags = new[] { "missing" };
            bool completed = false;
            dialogue.Play(entry, DialoguePriority.Story, () => completed = true);
            Assert.IsFalse(completed);
            Assert.IsFalse(dialogue.IsPlaying);
        }
        [Test] public void HiddenAmbientSource_CancelsWithoutCompletingAndCanPlayAgain()
        {
            var entry = Entry("dog_echo");
            bool completed = false;
            dialogue.Play(entry, DialoguePriority.Ambient, () => completed = true);
            dialogue.CancelAmbient(entry);
            Assert.IsFalse(dialogue.IsPlaying);
            Assert.IsFalse(completed);
            Assert.IsFalse(session.StoryFlags.HasFlag(entry.entryId));
            dialogue.Play(entry, DialoguePriority.Ambient);
            Assert.AreSame(entry, dialogue.Current);
        }
        [TestCase(DialoguePriority.Story)]
        [TestCase(DialoguePriority.Reveal)]
        public void AmbientCancellation_DoesNotInterruptBlockingDialogue(DialoguePriority priority)
        {
            var entry = Entry("story");
            dialogue.Play(entry, priority);
            dialogue.CancelAmbient(entry);
            Assert.IsTrue(dialogue.IsPlaying);
            Assert.AreSame(entry, dialogue.Current);
        }
        [Test] public void AmbientCancellation_DoesNotInterruptAnotherEntryOrHistoryReplay()
        {
            var entry = Entry("dog_echo");
            dialogue.Play(entry, DialoguePriority.Ambient);
            dialogue.CancelAmbient(Entry("other"));
            Assert.IsTrue(dialogue.IsPlaying);
            dialogue.CancelAmbient(entry);
            dialogue.PlayReadOnly(entry);
            dialogue.CancelAmbient(entry);
            Assert.IsTrue(dialogue.IsPlaying);
            Assert.AreSame(entry, dialogue.Current);
        }
        private void Register(params NarrativeAction[] actions)
        {
            typeof(DialogueLibrary).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(library, entries.ToArray());
            typeof(DialogueLibrary).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(library, actions);
        }
        private NarrativeAction Action(string beforeEntryId, string confirmBefore = "", string confirmAfter = "")
        {
            var action = ScriptableObject.CreateInstance<NarrativeAction>();
            action.beforeEntryId = beforeEntryId; action.confirmBefore = confirmBefore; action.confirmAfter = confirmAfter;
            created.Add(action);
            return action;
        }
        [Test] public void SkipChain_CommitsEventCompletionOnceWithoutWalkingLines()
        {
            var first = Entry("first"); var second = Entry("second"); first.nextId = second.entryId;
            Register();
            int completed = 0;
            dialogue.Play(first, DialoguePriority.Story, () => completed++);
            dialogue.SkipCurrent();
            dialogue.SkipCurrent();
            Assert.IsTrue(session.StoryFlags.HasFlag("first"));
            Assert.IsFalse(session.StoryFlags.HasFlag("second"), "后续条目的配置不应被跳过逻辑盲写");
            Assert.AreEqual(1, completed);
            Assert.IsFalse(dialogue.IsPlaying);
        }
        [Test] public void ActionStage_HoldsGatedLineUntilActionCompletes()
        {
            var first = Entry("cuff");
            Register(Action("cuff"));
            dialogue.Play(first, DialoguePriority.Story);
            Assert.IsTrue(dialogue.IsPlaying);
            Assert.IsNull(dialogue.Current, "动作结束前不显示被门控的第一句");
            Assert.IsFalse(session.StoryFlags.HasFlag("cuff"));
        }
        [Test] public void ActionWait_IgnoresConfirmOnSameFrameAsStageStart()
        {
            var first = Entry("smooth");
            Register(Action("smooth", confirmBefore: "抚平布角"));
            dialogue.Play(first, DialoguePriority.Story);
            Assert.IsTrue(dialogue.IsWaitingForAction);
            dialogue.Advance();
            Assert.IsTrue(dialogue.IsWaitingForAction, "启动事件的同一次输入不能穿透为动作确认");
            Assert.IsNull(dialogue.Current);
        }
        [Test] public void SkipDuringActionWait_CommitsEventAndClearsStage()
        {
            var first = Entry("niche");
            Register(Action("niche", confirmAfter: "告诉晓晓位置"));
            int completed = 0;
            dialogue.Play(first, DialoguePriority.Story, () => completed++);
            dialogue.SkipCurrent();
            Assert.IsTrue(session.StoryFlags.HasFlag("niche"));
            Assert.IsFalse(dialogue.IsWaitingForAction);
            Assert.IsFalse(dialogue.IsPlaying);
            Assert.AreEqual(1, completed);
        }
        [Test] public void ReadOnlyReplay_SkipsActionStage()
        {
            var first = Entry("cuff_replay");
            Register(Action("cuff_replay"));
            dialogue.PlayReadOnly(first);
            Assert.AreSame(first, dialogue.Current);
        }
    }
}
