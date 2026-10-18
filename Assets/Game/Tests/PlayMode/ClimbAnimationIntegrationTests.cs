using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Player;
using BorrowTomorrow.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    public sealed class ClimbAnimationIntegrationTests
    {
        private sealed class ClimbInput : IInputSource
        {
            public float move, climb;
            public int jumpFrame = -1;
            public float MoveAxis => move;
            public float ClimbAxis => climb;
            public bool JumpPressedThisFrame => Time.frameCount == jumpFrame;
            public bool InteractPressedThisFrame => false;
            public bool ConsumeInteract() => false;
            public float BoundaryNudgeDirection => 0;
            public bool RespawnPressedThisFrame => false;
            public bool HintPressedThisFrame => false;
            public bool ObjectiveHeld => false;
            public bool PausePressedThisFrame => false;
            public bool JumpHeld => JumpPressedThisFrame;
        }

        private ClimbInput input;
        private GameObject ladder, floor;

        private static IEnumerator Until(Func<bool> ready, float timeout, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ready(), message);
        }

        private static IEnumerator FlushPhysics()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (input != null) ServiceRegistry.Unregister<IInputSource>(input);
            if (ladder != null) Object.Destroy(ladder);
            if (floor != null) Object.Destroy(floor);
            if (GameSession.Instance != null) Object.Destroy(GameSession.Instance.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApprovedClimb_RealMovement_Stop_Reverse_Pause_Jump_AndReturnToWalk()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return Until(() => GameSession.Instance != null && GameSession.Instance.State.Phase == GamePhase.MainMenu,
                20f, "Bootstrap must reach the main menu.");
            var session = GameSession.Instance;
            var save = Object.FindObjectOfType<SaveService>();
            Assert.IsNotNull(save);
            string saveRoot = Environment.GetEnvironmentVariable("BORROWTOMORROW_CLIMB_TEST_OUTPUT");
            if (string.IsNullOrEmpty(saveRoot)) saveRoot = Path.Combine(Path.GetTempPath(), "BorrowTomorrowClimbTests");
            string saveDirectory = Path.Combine(saveRoot, "isolated-saves", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            typeof(SaveService).GetField("saveDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(save, saveDirectory);
            input = new ClimbInput();
            ServiceRegistry.Register<IInputSource>(input);
            session.StartNewGame();
            yield return Until(() => session.State.Phase == GamePhase.Playing, 25f, "New game must spawn the actual player prefab.");
            // Allow the existing introductory trigger to finish before movement.
            float freeDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < freeDeadline)
            {
                if (ServiceRegistry.TryGet<IDialogueService>(out var dialogue) && dialogue.IsPlaying) dialogue.SkipCurrent();
                if (session.State.IsPlayerControlAllowed) break;
                yield return null;
            }
            Assert.IsTrue(session.State.IsPlayerControlAllowed);
            var motor = Object.FindObjectOfType<PlayerMotor2D>();
            var art = motor.GetComponent<CharacterArtAnimator>();
            Assert.IsNotNull(art.visual);
            var climb = art.clips.Single(c => c.name == "Climb");
            Assert.AreEqual(15, climb.frames.Length);
            Assert.AreEqual(15, climb.frames.Distinct().Count());
            Assert.IsNotNull(climb.animation);
            Assert.AreEqual(1.5f, climb.animation.length, .001f);
            Assert.Greater(climb.distancePerCycle, .5f);
            Assert.Greater(climb.visualOffset.y, .5f);

            input.move = -1;
            yield return Until(() => art.visual.flipX, 2f, "Walking left must establish side-view facing.");
            input.move = 0;
            // A temporary trigger exercises the shipped motor/renderer through
            // real physics without altering any authored room or puzzle collider.
            Vector2 origin = new Vector2(200, 200);
            motor.TeleportTo(origin);
            ladder = new GameObject("TestOnlyLadder");
            ladder.transform.position = origin;
            int ladderLayer = LayerMask.NameToLayer("Ladder");
            if (ladderLayer >= 0) ladder.layer = ladderLayer;
            var trigger = ladder.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true; trigger.size = new Vector2(4, 30);
            ladder.AddComponent<LadderZone>();
            Physics2D.SyncTransforms();
            input.climb = 1;
            yield return Until(() => motor.IsClimbingLadder, 2f, "Up input must enter the real ladder state.");
            yield return FlushPhysics();
            Assert.Contains(art.visual.sprite, climb.frames);
            Assert.IsFalse(art.visual.flipX, "The rear-view animation must not mirror a previous left-facing walk.");
            float beforeY = motor.transform.position.y;
            Sprite beforeSprite = art.visual.sprite;
            yield return new WaitForSeconds(.14f);
            Assert.Greater(motor.transform.position.y, beforeY + .1f);
            Assert.AreNotSame(beforeSprite, art.visual.sprite, "Moving up must advance the approved sprite sequence.");

            input.climb = 0;
            yield return FlushPhysics();
            Sprite held = art.visual.sprite;
            Vector3 heldPosition = motor.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(motor.IsClimbingLadder);
            Assert.AreSame(held, art.visual.sprite, "Releasing climb input must hold the exact current frame.");
            Assert.Less(Vector3.Distance(heldPosition, motor.transform.position), .002f);
            art.PlayGesture("PickUp");
            yield return null;
            yield return null;
            Assert.AreSame(held, art.visual.sprite, "An unrelated gesture must not restart or replace the held ladder pose.");

            int beforeIndex = Array.IndexOf(climb.frames, held);
            input.climb = -1;
            yield return FlushPhysics();
            int afterIndex = Array.IndexOf(climb.frames, art.visual.sprite);
            int backwards = (beforeIndex - afterIndex + climb.frames.Length) % climb.frames.Length;
            Assert.Greater(backwards, 0, "Down movement must reverse the frame order.");
            Assert.Less(backwards, climb.frames.Length / 2);
            Assert.Less(motor.transform.position.y, heldPosition.y);

            input.climb = 0;
            yield return FlushPhysics();
            held = art.visual.sprite; heldPosition = motor.transform.position;
            session.State.EnterPause();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreSame(held, art.visual.sprite);
            Assert.Less(Vector3.Distance(heldPosition, motor.transform.position), .002f);
            session.State.ExitPause();
            input.climb = 1;
            yield return new WaitForSeconds(.14f);
            Assert.Greater(motor.transform.position.y, heldPosition.y + .1f);

            input.climb = 0;
            input.jumpFrame = Time.frameCount + 1;
            yield return Until(() => !motor.IsClimbingLadder, 2f, "Jump must release the ladder.");
            yield return null;
            yield return null;
            // The shipped 24-frame jump replaced JumpLand with physical phases.
            // Jumping off a ladder bypasses preparation and must show an ascent pose.
            var ascent = art.clips.Single(c => c.name == "JumpRise");
            Assert.Contains(art.visual.sprite, ascent.frames);
            var artType = typeof(CharacterArtAnimator);
            var visualOrigin = (Vector3)artType.GetField("baseVisualPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(art);
            var scale = (Vector3)artType.GetField("baseVisualScale", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(art);
            Assert.Less((art.visual.transform.localPosition-visualOrigin-(Vector3)ascent.visualOffset).sqrMagnitude, .0001f,
                "Leaving climb restores the approved jump registration, not the rear-view ladder offset.");
            Assert.Less((art.visual.transform.localScale-scale*ascent.visualScale).sqrMagnitude, .0001f);
            Assert.IsTrue(art.visual.flipX, "Leaving the rear view must restore horizontal facing.");

            floor = new GameObject("TestOnlyLanding");
            floor.transform.position = motor.transform.position + Vector3.down;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) floor.layer = groundLayer;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(8, .5f);
            Physics2D.SyncTransforms();
            yield return Until(() => motor.IsGrounded, 4f, "The existing motor must land normally after leaving the ladder.");
            var idleFrames = art.clips.Single(c => c.name == "Idle").frames;
            yield return Until(() => idleFrames.Contains(art.visual.sprite), 3f,
                "The 24-frame jump's landing recovery must finish before idle resumes.");
            Assert.Contains(art.visual.sprite, art.clips.Single(c => c.name == "Idle").frames);
            Debug.Log($"[ClimbLandingCheck] state={session.State.Phase}/{session.State.SubState}, grounded={motor.IsGrounded}, position={motor.transform.position}");
            // The game's delayed introductory dialogue can arrive during this
            // focused scenario; finish it before asking the motor to walk.
            float walkingDeadline = Time.realtimeSinceStartup + 5f;
            while (!session.State.IsPlayerControlAllowed && Time.realtimeSinceStartup < walkingDeadline)
            {
                if (ServiceRegistry.TryGet<IDialogueService>(out var dialogue) && dialogue.IsPlaying) dialogue.SkipCurrent();
                yield return null;
            }
            Assert.IsTrue(session.State.IsPlayerControlAllowed, "Walking is checked only after the existing dialogue releases input.");
            input.move = -1;
            yield return new WaitForSeconds(.14f);
            Debug.Log($"[ClimbWalkingCheck] state={session.State.Phase}/{session.State.SubState}, velocity={motor.GetComponent<Rigidbody2D>().velocity}, sprite={art.visual.sprite?.name}");
            Assert.Contains(art.visual.sprite, art.clips.Single(c => c.name == "Walk").frames);
            Assert.IsTrue(art.visual.flipX);
            TestContext.WriteLine("PASS: booted actual player prefab; up, stop, gesture priority, reverse, pause/resume, jump off, land, idle and walk.");
        }
    }
}
