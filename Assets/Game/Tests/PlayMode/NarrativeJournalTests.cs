using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorrowTomorrow.Tests
{
    public sealed class NarrativeJournalTests
    {
        private GameObject sessionHost;
        private GameObject journalHost;
        private GameObject eventHost;
        private GameSession session;
        private NarrativeJournal journal;
        private DialogueLibrary library;
        private readonly List<DialogueEntry> entries = new List<DialogueEntry>();
        private readonly IdleDialogue dialogue = new IdleDialogue();

        private sealed class IdleDialogue : IDialogueService
        {
            public bool IsPlaying => false;
            public void Play(DialogueEntry entry, DialoguePriority priority, System.Action onCompleted = null) { }
            public void Advance() { }
            public void SkipCurrent() { }
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            sessionHost = new GameObject("JournalTestSession");
            session = sessionHost.AddComponent<GameSession>();
            session.State.SetPhase(GamePhase.MainMenu);
            session.State.SetPhase(GamePhase.Loading);
            session.State.SetPhase(GamePhase.Playing);
            library = sessionHost.AddComponent<DialogueLibrary>();
            ServiceRegistry.Register<IDialogueService>(dialogue);
            if (EventSystem.current == null) eventHost = new GameObject("JournalTestEvents", typeof(EventSystem));
            journalHost = new GameObject("JournalTest");
            journal = journalHost.AddComponent<NarrativeJournal>();
            journal.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(journalHost);
            Object.Destroy(sessionHost);
            if (eventHost != null) Object.Destroy(eventHost);
            ServiceRegistry.Unregister<IDialogueService>(dialogue);
            foreach (var entry in entries) Object.Destroy(entry);
            entries.Clear();
            yield return null;
            yield return null;
        }

        private T Field<T>(string name) => (T)typeof(NarrativeJournal)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(journal);

        private void AddRecords(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var entry = ScriptableObject.CreateInstance<DialogueEntry>();
                entry.entryId = "journal_" + i;
                entry.speakerLabel = "Speaker " + i;
                entry.subtitle = "Record " + i + ": text that must remain accessible when the journal extends below the viewport.";
                entries.Add(entry);
                session.RecordDialogue(entry.entryId);
            }
            var reversed = new List<DialogueEntry>(entries);
            reversed.Reverse();
            typeof(DialogueLibrary).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(library, reversed.ToArray());
        }

        [UnityTest]
        public IEnumerator ToggleRepeatedly_ClosesAndRestoresPlayerControl()
        {
            for (int i = 0; i < 3; i++)
            {
                journal.Toggle();
                Assert.IsTrue(journal.IsOpen);
                Assert.IsFalse(session.State.IsPlayerControlAllowed);
                yield return null;
                journal.Toggle();
                Assert.IsFalse(journal.IsOpen);
                Assert.IsTrue(session.State.IsPlayerControlAllowed);
            }
        }

        [UnityTest]
        public IEnumerator LostSession_DoesNotPreventClosingTheVisibleJournal()
        {
            journal.Toggle();
            Object.Destroy(sessionHost);
            yield return null;
            Assert.IsNull(GameSession.Instance);
            Assert.IsTrue(journal.IsOpen);
            journal.Toggle();
            Assert.IsFalse(journal.IsOpen, "A missing session must not trap the visible journal.");
        }

        [UnityTest]
        public IEnumerator CloseButton_ReceivesPointerAndRestoresPlayerControl()
        {
            journal.Toggle();
            yield return null;
            var button = Field<GameObject>("panel").GetComponentInChildren<Button>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreSame(button.gameObject, ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.IsFalse(journal.IsOpen);
            Assert.IsTrue(session.State.IsPlayerControlAllowed);
        }

        [UnityTest]
        public IEnumerator Records_UseReadingOrderAndCanScrollToTheEnd()
        {
            AddRecords(33);
            journal.Toggle();
            yield return null;
            var text = Field<Text>("text");
            int cursor = 0;
            foreach (var entry in entries)
            {
                int position = text.text.IndexOf(entry.subtitle, cursor, System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(position, cursor, entry.entryId);
                cursor = position + entry.subtitle.Length;
            }
            var scroll = Field<ScrollRect>("scroll");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.position),
                scrollDelta = new Vector2(0, -4)
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreSame(scroll.gameObject, ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject));
            Assert.AreSame(scroll.gameObject, ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.scrollHandler);
            Assert.Less(scroll.verticalNormalizedPosition, .99f);
            scroll.verticalScrollbar.value = 0;
            Assert.AreEqual(0f, scroll.verticalNormalizedPosition, .001f);
        }
    }
}
