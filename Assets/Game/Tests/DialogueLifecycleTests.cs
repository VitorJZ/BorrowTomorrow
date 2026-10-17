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
        }
        private static void Invoke(object o, string method) => o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
        private DialogueEntry Entry(string id)
        {
            var entry = ScriptableObject.CreateInstance<DialogueEntry>();
            entry.entryId = id;
            entry.setFlags = new[] { id };
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
        [Test] public void SkipChain_WritesAllFlagsAndCompletesOnce()
        {
            var first = Entry("first"); var second = Entry("second"); first.nextId = second.entryId;
            typeof(DialogueLibrary).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(library, entries.ToArray());
            int completed = 0;
            dialogue.Play(first, DialoguePriority.Story, () => completed++);
            dialogue.SkipCurrent();
            dialogue.SkipCurrent();
            Assert.IsTrue(session.StoryFlags.HasFlag("first"));
            Assert.IsTrue(session.StoryFlags.HasFlag("second"));
            Assert.AreEqual(1, completed);
            Assert.IsFalse(dialogue.IsPlaying);
        }
    }
}
