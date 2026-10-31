using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Save;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    public sealed class RopePointerInteractionTests
    {
        private sealed class PointerControls : IInputSource, IPointerInputSource
        {
            public Vector2 position;
            public bool down, held;
            public Vector2 PointerPosition => position;
            public bool PrimaryPressedThisFrame => down;
            public bool PrimaryHeld => held;
            public bool SecondaryPressedThisFrame => false;
            public bool HintPressedThisFrame => false;
            public float MoveAxis => 0;
            public float ClimbAxis => 0;
            public float BoundaryNudgeDirection => 0;
            public bool JumpPressedThisFrame => false;
            public bool JumpHeld => false;
            public bool InteractPressedThisFrame => false;
            public bool ConsumeInteract() => false;
            public bool RespawnPressedThisFrame => false;
            public bool ObjectiveHeld => false;
            public bool PausePressedThisFrame => false;
        }

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private GameSession session;
        private RopeDryingRack mechanism;
        private PointerControls controls;

        private object Call(string name, params object[] args)
        {
            var method = typeof(RopeDryingRack).GetMethod(name, Private);
            Assert.IsNotNull(method, name);
            return method.Invoke(mechanism, args);
        }
        private T Read<T>(string name) => (T)typeof(RopeDryingRack).GetField(name, Private).GetValue(mechanism);
        private void Write(string name, object value) => typeof(RopeDryingRack).GetField(name, Private).SetValue(mechanism, value);
        private Vector2 ScreenPoint(Vector3 world) => Camera.main.WorldToScreenPoint(world);
        private Vector3 WorldPoint(Vector2 screen)
        {
            var p = Camera.main.ScreenToWorldPoint(screen);
            p.z = 0;
            return p;
        }
        private void Tick() => Call("Update");

        private IEnumerator Until(Func<bool> ready, string message)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!ready() && Time.realtimeSinceStartup < deadline)
            {
                if (ServiceRegistry.TryGet<IDialogueService>(out var dialogue) && dialogue.IsPlaying)
                    dialogue.SkipCurrent();
                yield return null;
            }
            Assert.IsTrue(ready(), message);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return Until(() => GameSession.Instance != null
                && GameSession.Instance.State.Phase == GamePhase.MainMenu, "Bootstrap ready");
            session = GameSession.Instance;
            var saves = Object.FindObjectOfType<SaveService>();
            Assert.IsNotNull(saves);
            // These tests never write the user's campaign save.
            string directory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowRopePointer_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            typeof(SaveService).GetField("saveDirectory", Private).SetValue(saves, directory);
            controls = new PointerControls();
            ServiceRegistry.Register<IInputSource>(controls);
            session.LoadLevel(session.FindLevel("L01B"));
            yield return Until(() => session.State.Phase == GamePhase.Playing
                && session.ActiveRoom != null && session.ActiveRoom.RoomId == "L01B"
                && ServiceRegistry.TryGet<IPlayerContext>(out var player) && player.IsGrounded
                && !player.IsClimbingLadder, "Real tree-room player grounded");
            if (ServiceRegistry.TryGet<IDialogueService>(out var d) && d.IsPlaying) d.SkipCurrent();
            session.State.SetSubState(PlayingSubState.Free);
            mechanism = Object.FindObjectOfType<RopeDryingRack>();
            Assert.IsNotNull(mechanism);
            // Drive the real Update deterministically, without a second automatic Update in the same frame.
            mechanism.enabled = false;
            Write("input", controls);
            Assert.IsNotNull(Camera.main);
        }

        private void Configure(bool routed, bool grown = true)
        {
            session.StoryFlags.Clear();
            session.StoryFlags.SetFlag(RopeDryingRack.ReleasedFlag);
            if (routed) session.StoryFlags.SetFlag(RopeDryingRack.RoutedFlag);
            mechanism.rack.ApplyEra(Era.Past, true);
            mechanism.tree.ApplyEra(grown ? Era.Present : Era.Past, true);
            Call("EraChanged", mechanism.tree, mechanism.tree.CurrentEra);
            Write("unlockTime", 0f);
            Write("routeSlideRemaining", 0f);
            Write("needNewPress", false);
            Write("tailLabelUntil", Time.time + 10f);
            session.State.SetSubState(PlayingSubState.Free);
            controls.down = controls.held = false;
            Call("Paint");
        }

        private Vector3 GrabAtPadding(Vector3 axis)
        {
            Vector3 end = mechanism.tailImage.transform.position;
            Vector2 centre = ScreenPoint(end);
            float radius = Mathf.Max(22f, Vector2.Distance(centre, ScreenPoint(end + Vector3.right * .65f)));
            Vector2 direction = (ScreenPoint(end + axis.normalized) - centre).normalized;
            controls.position = centre + direction * radius * .9f;
            Assert.IsTrue((bool)Call("OverPoint", controls.position, end));
            Vector3 offset = end - WorldPoint(controls.position);
            controls.down = controls.held = true;
            Tick();
            controls.down = false;
            Assert.IsTrue(Read<bool>("dragging"), "Padded rope handle starts dragging");
            Assert.Less(Vector3.Distance(end, mechanism.tailImage.transform.position), .001f,
                "Grabbing off-centre must not teleport the visible rope end");
            return offset;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mechanism != null) Call("StopDrag");
            if (controls != null) ServiceRegistry.Unregister<IInputSource>(controls);
            if (session != null) Object.Destroy(session.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LabelStartsDragAndPreservesGrabOffset()
        {
            Configure(false);
            Vector3 before = mechanism.tailImage.transform.position;
            Rect area = (Rect)Call("TailLabelRect");
            controls.position = new Vector2(area.center.x, Screen.height - area.center.y);
            Assert.IsFalse((bool)Call("OverPoint", controls.position, before), "Test must click the words, not rope padding");
            controls.down = controls.held = true;
            Tick();
            Assert.IsTrue(Read<bool>("dragging"));
            Assert.Less(Vector3.Distance(before, mechanism.tailImage.transform.position), .001f);
            controls.down = false;
            Vector2 delta = new Vector2(16, 8);
            Vector3 expected = before + WorldPoint(controls.position + delta) - WorldPoint(controls.position);
            controls.position += delta;
            Tick();
            Assert.Less(Vector3.Distance(expected, mechanism.tailImage.transform.position), .001f);
            yield break;
        }

        [UnityTest]
        public IEnumerator VisibleEndOnPoleRoutesWhileCursorIsOffPole()
        {
            Configure(false);
            Vector3 offset = GrabAtPadding(Vector3.up);
            controls.position = ScreenPoint(mechanism.fork.position - offset);
            object[] cursorArgs = { controls.position, Vector3.zero };
            Assert.IsFalse((bool)Call("TryPoleContact", cursorArgs), "Cursor is genuinely outside the pole target");
            Tick();
            Assert.IsTrue(mechanism.Routed, "Physical rope end on pole must route even with offset cursor");
            yield break;
        }

        [UnityTest]
        public IEnumerator CursorOnPoleDoesNotRouteWhenVisibleEndIsOffPole()
        {
            Configure(false);
            GrabAtPadding(Vector3.up);
            controls.position = ScreenPoint(mechanism.fork.position);
            Tick();
            Assert.IsFalse(mechanism.Routed, "Cursor alone is not the dragged object");
            yield break;
        }

        [UnityTest]
        public IEnumerator VisibleEndAtHookSecuresWhileCursorIsOutsideOldRadius()
        {
            Configure(true);
            Vector3 offset = GrabAtPadding(Vector3.up);
            controls.position = ScreenPoint(mechanism.hook.position - offset);
            Assert.Greater(Vector2.Distance(WorldPoint(controls.position), mechanism.hook.position), .5f);
            Tick();
            Assert.IsTrue(mechanism.Secured);
            Assert.IsTrue(mechanism.SupportValid);
            Assert.IsTrue(mechanism.deck.enabled);
            yield break;
        }

        [UnityTest]
        public IEnumerator CursorAtHookDoesNotSecureWhenVisibleEndIsAway()
        {
            Configure(true);
            Vector3 direction = mechanism.hook.position - mechanism.fork.position;
            GrabAtPadding(new Vector3(-direction.y, direction.x, 0));
            controls.position = ScreenPoint(mechanism.hook.position);
            Tick();
            Assert.Greater(Vector2.Distance(Read<Vector3>("tail"), mechanism.hook.position), .5f);
            Assert.IsFalse(mechanism.Secured);
            Assert.IsFalse(mechanism.deck.enabled);
            yield break;
        }

        [UnityTest]
        public IEnumerator WrongTreeEraCannotSecure()
        {
            Configure(true, false);
            Vector3 offset = GrabAtPadding(Vector3.up);
            controls.position = ScreenPoint(mechanism.hook.position - offset);
            Tick();
            Assert.IsFalse(mechanism.Combination);
            Assert.IsFalse(mechanism.Secured);
            Assert.IsFalse(mechanism.deck.enabled);
            yield break;
        }

        [UnityTest]
        public IEnumerator ReleasingDuringPauseRequiresANewGrab()
        {
            Configure(false);
            GrabAtPadding(Vector3.up);
            session.State.EnterPause();
            controls.held = false;
            Tick();
            session.State.ExitPause();
            Tick();
            Assert.IsFalse(Read<bool>("dragging"));
            Assert.AreEqual(PlayingSubState.Free, session.State.SubState);
            Assert.IsFalse(mechanism.Routed);
            yield break;
        }
    }
}
