#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Player;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;

namespace BorrowTomorrow.Diagnostics
{
    // Opt-in physical walkthrough; ordinary game launches never create this object.
    public sealed class PlayableWalkthrough : MonoBehaviour
    {
        private readonly WalkInput input = new WalkInput();
        private readonly List<string> report = new List<string>();
        private PlayerMotor2D motor;
        private string output;
        private bool finished;
        private float nextDialogue;
        private GameSession Session => GameSession.Instance;
        private BoundaryController Boundary => ServiceRegistry.Get<IBoundaryContext>() as BoundaryController;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(), a => a == "--verify-playable")) return;
            if (FindObjectOfType<PlayableWalkthrough>() == null)
                new GameObject("OptInPlayableWalkthrough").AddComponent<PlayableWalkthrough>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            output = Environment.GetEnvironmentVariable("BORROWTOMORROW_WALKTHROUGH_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Path.Combine(Application.persistentDataPath, "Walkthrough");
            Directory.CreateDirectory(output);
            ServiceRegistry.Register<IInputSource>(input);
            Application.logMessageReceived += OnLog;
            Application.targetFrameRate = 60;
        }

        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string message, string trace, LogType type)
        {
            if (!finished && (type == LogType.Exception || type == LogType.Error)) Finish(false, message + "\n" + trace);
        }
        private void Update()
        {
            if (!finished && ServiceRegistry.TryGet<IDialogueService>(out var dialogue)
                && dialogue.IsPlaying && Time.unscaledTime >= nextDialogue)
            {
                input.interactFrame = Time.frameCount + 1;
                nextDialogue = Time.unscaledTime + 1.3f;
            }
        }
        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return Until(() => Session != null && Session.State.Phase == GamePhase.MainMenu, 20f, "Bootstrap reaches main menu");
            yield return null;
            UnityEngine.UI.Button startButton = null;
            foreach (var button in FindObjectsOfType<UnityEngine.UI.Button>())
            {
                var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
                if (label != null && label.text == "开始新游戏") startButton = button;
            }
            Check(startButton != null && startButton.interactable, "Main menu start button is available");
            startButton.onClick.Invoke();
            yield return Level("L00");
            Adjust(0f);
            yield return Move(23f);
            Shot("01-L00-gate");
            yield return PressE();
            yield return Level("L01");
            Adjust(Boundary.RangeMax);
            yield return Move(19.5f);
            Shot("02-L01A-canal");
            yield return PressE();
            yield return Level("L01B");
            yield return Settle();
            yield return Move(48f);
            yield return ClimbTo(2.06f);
            yield return Move(49.6f);
            yield return Settle();
            Check(motor.IsGrounded && !motor.IsClimbingLadder, "Leave ladder onto permanent ledge");
            Adjust(53f);
            yield return Move(50.2f);
            yield return JumpTo(51.8f);
            yield return Move(68.1f);
            Shot("03-L01B-tree");
            yield return PressE();

            yield return Level("L02");
            Adjust(Boundary.RangeMax);
            yield return Move(13f);
            yield return ClimbTo(3.06f);
            yield return Move(11.5f);
            yield return Settle();
            Check(motor.IsGrounded && !motor.IsClimbingLadder, "L02 stand on permanent brick platform");
            float previous = Boundary.CommittedBoundary;
            input.nudge = -1f;
            yield return new WaitForSeconds(0.5f);
            input.nudge = 0f;
            yield return null;
            yield return null;
            Check(Boundary.CommittedBoundary < previous - 0.5f && Free(), "Q/R hold moves continuously and release unlocks movement");
            Session.State.EnterPause();
            float pausedX = motor.transform.position.x;
            input.move = 1f;
            yield return new WaitForSecondsRealtime(0.15f);
            Check(Time.timeScale == 0f && Mathf.Abs(motor.transform.position.x - pausedX) < 0.02f,
                "Pause freezes physical movement");
            input.move = 0f;
            Session.State.ExitPause();
            yield return null;
            Adjust(0f);
            yield return Move(9.6f);
            yield return PressE();
            yield return Until(() => Session.StoryFlags.HasFlag(StoryFlags.UniformRead) && Free(), 20f, "Uniform dialogue unlocks exit");
            yield return Move(5f);
            Adjust(Boundary.RangeMax);
            Check(Boundary.CommittedBoundary < 6.2f, "Cannot remove support under player");
            Adjust(0f);
            Shot("04-L02-platform");
            yield return Move(1.6f);
            yield return PressE();

            yield return Level("L03");
            Adjust(11f);
            yield return Move(20f);
            Adjust(Boundary.RangeMax);
            yield return Move(28.5f);
            yield return PressE();
            yield return Until(() => Session.StoryFlags.HasFlag(StoryFlags.IdentityKnown) && Free(), 25f, "Photo chain confirms identity");
            Shot("05-L03-photo");
            yield return Move(29.8f);
            yield return PressE();

            yield return Level("L04");
            yield return PressE();
            yield return Until(Free, 12f, "Observe niche without blocking controls");
            // 竹梯（过去）上腰檐 → 铁梯（现在、树仍是幼苗）上墀头 → 墀头上让树长大，沿长枝到暗格（Docs/18）。
            Adjust(Boundary.RangeMax);
            yield return Move(25.2f);
            yield return ClimbTo(5.07f);
            yield return Move(18.8f);
            yield return Settle();
            Adjust(16.5f);
            yield return ClimbTo(7.65f);
            yield return Move(19.6f);
            yield return Settle();
            Adjust(0f);
            yield return Move(25.5f);
            Shot("06-L04-tree-to-niche");
            yield return PressE();
            yield return Level("Epilogue");
            yield return Move(7f);
            yield return PressE();
            yield return Until(() => Session.StoryFlags.HasFlag(StoryFlags.EpilogueSeen) && Free(), 20f, "Epilogue letter completes");
            Shot("07-Epilogue");
            yield return Move(13.5f);
            yield return PressE();
            yield return Until(() => Session.State.Phase == GamePhase.Credits, 8f, "Reach credits through real exits");
            Finish(true, "All levels completed through movement, climbing, boundary changes and interactions.");
        }

        private bool Free() => Session != null && Session.State.IsPlayerControlAllowed && !Session.State.IsPaused;
        private IEnumerator Level(string id)
        {
            yield return Until(() => Session.CurrentLevel != null && Session.CurrentLevel.levelId == id && Free(), 20f, "Enter " + id);
            motor = FindObjectOfType<PlayerMotor2D>();
            yield return Settle();
            Check(motor != null && motor.IsGrounded, id + " spawns grounded");
            foreach (TemporalObject obj in Boundary.GetComponent<TemporalObjectRegistry>().Objects)
            {
                Era expected = obj.AnchorX < Boundary.CommittedBoundary ? Era.Past : Era.Present;
                Check(obj.CurrentEra == expected, id + " initial era agrees with boundary for " + obj.ObjectId);
                Transform past = obj.transform.Find("Past");
                Transform present = obj.transform.Find("Present");
                Check((past == null || past.gameObject.activeSelf == (expected == Era.Past))
                    && (present == null || present.gameObject.activeSelf == (expected == Era.Present)),
                    id + " activates only one era root for " + obj.ObjectId);
            }
        }
        private IEnumerator Settle() { yield return new WaitForSeconds(0.35f); }
        private IEnumerator Move(float x)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Mathf.Abs(motor.transform.position.x - x) > 0.09f && Time.realtimeSinceStartup < deadline)
            {
                input.move = Mathf.Sign(x - motor.transform.position.x);
                yield return null;
            }
            input.move = 0f;
            Check(Mathf.Abs(motor.transform.position.x - x) <= 0.15f, "Walk to " + x + " at " + motor.transform.position);
        }
        private IEnumerator JumpTo(float x)
        {
            input.jumpFrame = Time.frameCount + 1;
            input.move = Mathf.Sign(x - motor.transform.position.x);
            yield return null;
            yield return Move(x);
            yield return Settle();
            Check(motor.IsGrounded, "Jump lands on route at " + x);
        }
        private IEnumerator ClimbTo(float y)
        {
            input.climb = 1f;
            yield return Until(() => motor.transform.position.y >= y, 8f, "Climb to landing " + y);
            Boundary.BeginMouseDrag();
            Check(!Boundary.IsAdjusting, "Boundary cannot begin while climbing");
            input.climb = 0f;
        }
        private IEnumerator PressE()
        {
            yield return new WaitForSeconds(0.25f);
            input.interactFrame = Time.frameCount + 1;
            nextDialogue = Time.unscaledTime + 1.3f;
            yield return null;
            yield return new WaitForSeconds(0.2f);
        }
        private void Adjust(float value)
        {
            Boundary.BeginMouseDrag();
            Check(Boundary.IsAdjusting, "Grounded boundary drag begins");
            Boundary.RequestAdjustment(value);
            Boundary.EndMouseDrag();
            Check(Free(), "Drag release returns to free movement");
        }
        private IEnumerator Until(Func<bool> condition, float timeout, string label)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Check(condition(), label);
        }
        private void Check(bool ok, string label)
        {
            report.Add((ok ? "PASS " : "FAIL ") + label);
            if (!ok) { Finish(false, label); throw new InvalidOperationException(label); }
        }
        private void Shot(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); }
        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(240f);
            if (!finished) Finish(false, "Walkthrough timed out");
        }
        private void Finish(bool ok, string message)
        {
            if (finished) return;
            finished = true;
            File.WriteAllText(Path.Combine(output, "walkthrough.txt"), (ok ? "PASS\n" : "FAIL\n") + string.Join("\n", report) + "\n" + message);
            Application.Quit(ok ? 0 : 1);
        }
        private sealed class WalkInput : IInputSource
        {
            public float move, climb, nudge;
            public int jumpFrame = -1, interactFrame = -1;
            private int consumed = -1;
            public float MoveAxis => move;
            public float ClimbAxis => climb;
            public float BoundaryNudgeDirection => nudge;
            public bool JumpPressedThisFrame => Time.frameCount == jumpFrame;
            public bool JumpHeld => JumpPressedThisFrame;
            public bool InteractPressedThisFrame => Time.frameCount == interactFrame;
            public bool ConsumeInteract()
            {
                if (!InteractPressedThisFrame || consumed == Time.frameCount) return false;
                consumed = Time.frameCount;
                return true;
            }
            public bool RespawnPressedThisFrame => false;
            public bool ObjectiveHeld => false;
            public bool PausePressedThisFrame => false;
        }
    }
}
#endif
