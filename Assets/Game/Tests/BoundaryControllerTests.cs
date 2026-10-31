using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;
using UnityEngine;

namespace BorrowTomorrow.Tests
{
    public sealed class BoundaryControllerTests
    {
        private GameObject sessionObject;
        private GameObject roomObject;
        private GameSession session;
        private BoundaryController boundary;
        private readonly TestInput input = new TestInput();
        private readonly TestPlayer player = new TestPlayer();
        private TestAudio audio;

        [SetUp]
        public void SetUp()
        {
            sessionObject = new GameObject("TestSession");
            session = sessionObject.AddComponent<GameSession>();
            Invoke(session, "Awake");
            session.State.SetPhase(GamePhase.MainMenu);
            session.State.SetPhase(GamePhase.Loading);
            session.State.SetPhase(GamePhase.Playing);
            ServiceRegistry.Register<IPlayerContext>(player);
            ServiceRegistry.Register<IInputSource>(input);
            audio = new TestAudio();
            ServiceRegistry.Register<IAudioService>(audio);
            roomObject = new GameObject("TestRoom");
            roomObject.AddComponent<TemporalObjectRegistry>();
            boundary = roomObject.AddComponent<BoundaryController>();
            Invoke(boundary, "Awake");
            Invoke(boundary, "Start");
            boundary.RegisterAsActive();
            boundary.ConfigureRoom(0f, 10f, 5f, "test_room");
            input.Direction = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            ServiceRegistry.Unregister<IInputSource>(input);
            ServiceRegistry.Unregister<IPlayerContext>(player);
            ServiceRegistry.Unregister<IAudioService>(audio);
            Object.DestroyImmediate(roomObject);
            Object.DestroyImmediate(sessionObject);
        }

        [Test]
        public void ConfigureRoom_SynchronizesInitialEraMix()
        {
            boundary.ConfigureRoom(0f, 20f, 0f, "present_room");
            Assert.AreEqual(0f, audio.PastMix, "进入全 2024 房间应切换到现在配乐");
            boundary.ConfigureRoom(0f, 20f, 20f, "past_room");
            Assert.AreEqual(1f, audio.PastMix);
        }

        [Test]
        public void RestoreBoundary_SynchronizesSavedMixWithoutDragging()
        {
            boundary.RestoreBoundary(2.5f);
            Assert.AreEqual(.25f, audio.PastMix);
        }

        [Test]
        public void InactiveRoomRestore_DoesNotChangeMusic_UntilActivated()
        {
            var otherObject = new GameObject("InactiveRoom");
            try
            {
                otherObject.AddComponent<TemporalObjectRegistry>();
                var other = otherObject.AddComponent<BoundaryController>();
                Invoke(other, "Awake");
                other.ConfigureRoom(0f, 10f, 10f, "other");
                other.RestoreBoundary(0f);
                Assert.AreEqual(.5f, audio.PastMix, "非活动房间不能覆盖当前 BGM");
                other.RegisterAsActive();
                Assert.AreEqual(0f, audio.PastMix, "激活已有房间也要同步 BGM");
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void KeyboardRelease_AfterMultipleFrames_UnlocksPlayer()
        {
            input.Direction = 1f;
            Invoke(boundary, "Update");
            Assert.IsTrue(boundary.IsAdjusting);
            Invoke(boundary, "Update");
            input.Direction = 0f;
            Invoke(boundary, "Update");
            Assert.IsFalse(boundary.IsAdjusting);
            Assert.IsTrue(session.State.IsPlayerControlAllowed);
        }

        [Test]
        public void MouseDrag_WhilePaused_DoesNotCommitBoundary()
        {
            boundary.BeginMouseDrag();
            session.State.EnterPause();
            boundary.RequestAdjustment(8f);
            Assert.AreEqual(5f, boundary.CommittedBoundary);
            session.State.ExitPause();
            boundary.RequestAdjustment(8f);
            Assert.AreEqual(8f, boundary.CommittedBoundary);
            boundary.EndMouseDrag();
            Assert.IsTrue(session.State.IsPlayerControlAllowed);
        }

        [Test]
        public void KeyboardReleaseDuringPause_IsProcessedAfterResume()
        {
            input.Direction = 1f;
            Invoke(boundary, "Update");
            session.State.EnterPause();
            input.Direction = 0f;
            Invoke(boundary, "Update");
            Assert.IsTrue(session.State.IsPaused);
            session.State.ExitPause();
            Invoke(boundary, "Update");
            Assert.IsTrue(session.State.IsPlayerControlAllowed);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }

        private sealed class TestAudio : IAudioService
        {
            public float PastMix = -1f;
            public void SetEraMix(float pastAreaRatio) { PastMix = pastAreaRatio; }
            public void PlaySfx(string sfxId, Vector2? atWorldPosition = null) { }
            public void SetDialogueDucking(bool active) { }
            public void ApplyVolumeSettings(SaveSettings settings) { }
        }

        private sealed class TestPlayer : IPlayerContext
        {
            public Vector2 Position => Vector2.zero;
            public Bounds BodyBounds => new Bounds(Vector3.zero, Vector3.one);
            public bool IsGrounded => true;
            public bool IsClimbingLadder => false;
            public Collider2D GroundCollider => null;
            public bool IsPresent => true;
            public void TeleportTo(ISpawnPoint spawn) { }
            public void TeleportToPosition(Vector2 position) { }
        }

        private sealed class TestInput : IInputSource
        {
            public float Direction;
            public float BoundaryNudgeDirection => Direction;
            public float MoveAxis => 0f;
            public float ClimbAxis => 0f;
            public bool JumpPressedThisFrame => false;
            public bool InteractPressedThisFrame => false;
            public bool ConsumeInteract() => false;
            public bool RespawnPressedThisFrame => false;
            public bool ObjectiveHeld => false;
            public bool PausePressedThisFrame => false;
            public bool JumpHeld => false;
        }
    }
}
