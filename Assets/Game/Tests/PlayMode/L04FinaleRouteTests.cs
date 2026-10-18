using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Player;
using BorrowTomorrow.Save;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// L04 终局关（竹梯→铁梯→桂花树，Docs/18）按真实移动、攀爬与边界提交走完整标准解，
    /// 并验证顺序约束：树长大会挡住铁梯；站在长枝下方时不允许让树长大。
    /// </summary>
    public sealed class L04FinaleRouteTests
    {
        private const float Sill = 2.11f, Cornice = 4.87f, Corbel = 7.45f, NicheLedge = 7.63f;
        private const float TreeX = 14.3f, MetalX = 18.8f, BambooX = 25.2f;

        private string saveDirectory;
        private RouteInput input;
        private PlayerMotor2D motor;
        private BoundaryController boundary;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (GameSession.Instance != null) Object.Destroy(GameSession.Instance.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
            yield return null;
            if (!string.IsNullOrEmpty(saveDirectory) && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator L04_ThreeGenerationRoute_EnforcesOrderAndReachesNiche()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            var session = GameSession.Instance;
            saveDirectory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowL04_" + Guid.NewGuid());
            Directory.CreateDirectory(saveDirectory);
            typeof(SaveService).GetField("saveDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Object.FindObjectOfType<SaveService>(), saveDirectory);
            foreach (string flag in new[] { "IntroPlayed", "DogEchoPlayed", "HeightMarksSeen", "UniformRead", "PhotoRevealed", "IdentityKnown" })
                session.StoryFlags.SetFlag(flag);
            // 玩家运动在 Start 时缓存输入源：必须在加载关卡（生成玩家）之前注册测试输入。
            input = new RouteInput();
            ServiceRegistry.Register<IInputSource>(input);
            session.LoadLevel(session.FindLevel("L04"));
            yield return Until(() => session.State.Phase == GamePhase.Playing, 20f, "进入 L04");
            var dialogue = (DialoguePlayer)ServiceRegistry.Get<IDialogueService>();
            yield return new WaitForSeconds(1.2f);
            if (dialogue.IsPlaying) dialogue.SkipCurrent();
            Assert.IsTrue(session.StoryFlags.HasFlag("CompartmentSeen"), "窗边初看木板应自动播放");
            yield return null;

            motor = Object.FindObjectOfType<PlayerMotor2D>();
            boundary = (BoundaryController)ServiceRegistry.Get<IBoundaryContext>();
            var registry = boundary.GetComponent<TemporalObjectRegistry>();
            TemporalObject Obj(string id) => registry.Objects.First(o => o.ObjectId == id);
            var tree = Obj("L04_tree"); var metal = Obj("L04_metal_ladder"); var bamboo = Obj("L04_bamboo_ladder");
            yield return Settle();
            Assert.IsTrue(motor.IsGrounded, "窗台出生站稳");
            Assert.AreEqual(Era.Past, tree.CurrentEra, "开局树是幼苗");
            Assert.AreEqual(Era.Present, metal.CurrentEra, "开局能看到够不着的铁梯");
            Assert.AreEqual(Era.Present, bamboo.CurrentEra, "开局竹梯不在");
            yield return Shot("1-start");

            // 窗台原地跳不上腰檐。
            input.jumpFrame = Time.frameCount + 1;
            float peak = motor.transform.position.y, jumpEnd = Time.realtimeSinceStartup + 2f;
            yield return null;
            yield return null;
            while (Time.realtimeSinceStartup < jumpEnd && !(motor.IsGrounded && peak > Sill + .3f))
            {
                peak = Mathf.Max(peak, motor.transform.position.y);
                yield return null;
            }
            Assert.Greater(peak, Sill + .3f, "测试输入确实触发了跳跃");
            Assert.Less(peak, Cornice - .5f, "窗台到腰檐的高差必须超过跳跃能力");
            yield return Settle();
            Assert.Less(Mathf.Abs(motor.transform.position.y - Sill), .15f, "跳完落回窗台");

            // ① 推过竹梯：竹梯回到 1994，爬上腰檐。
            yield return Adjust(boundary.RangeMax);
            Assert.AreEqual(Era.Past, bamboo.CurrentEra);
            Assert.AreEqual(Era.Past, metal.CurrentEra, "推过竹梯的同时铁梯消失");
            yield return Shot("2-bamboo");
            yield return Move(BambooX);
            yield return Climb(Cornice + .2f);
            yield return Move(23.4f);
            yield return Settle();
            Assert.IsTrue(motor.IsGrounded && Mathf.Abs(motor.transform.position.y - Cornice) < .15f, "站上腰檐");

            // 错误顺序：在腰檐右段让树长大 → 长枝横在铁梯前，走不过去。
            yield return Adjust(0f);
            Assert.AreEqual(Era.Present, tree.CurrentEra, "腰檐右段不在长枝下方，允许让树长大");
            yield return TryMove(MetalX);
            Assert.Greater(motor.transform.position.x, 21f, "成年长枝挡住通往铁梯的腰檐");
            yield return Shot("3-branch-blocks-ladder");
            yield return Adjust(16.5f);
            Assert.AreEqual(Era.Past, tree.CurrentEra);
            Assert.AreEqual(Era.Present, metal.CurrentEra);
            yield return Shot("4-metal-ladder");

            // ② 正确顺序：站到铁梯下（长枝的位置），拉过树干会被安全规则挡在树干右侧。
            yield return Move(MetalX + .6f);
            yield return Adjust(0f);
            Assert.Greater(boundary.CommittedBoundary, TreeX - .1f, "长枝会覆盖玩家，边界停在树干锚点（含滞回）前");
            Assert.AreEqual(Era.Past, tree.CurrentEra);
            yield return Move(MetalX);
            yield return Climb(Corbel + .2f);
            yield return Move(19.6f);
            yield return Settle();
            Assert.IsTrue(motor.IsGrounded && Mathf.Abs(motor.transform.position.y - Corbel) < .15f, "站上墀头");

            // ③ 墀头上让树长大，沿长枝走到暗格小台。
            yield return Adjust(0f);
            Assert.AreEqual(Era.Present, tree.CurrentEra, "墀头上可以让树长大");
            Assert.AreEqual(Era.Present, bamboo.CurrentEra);
            yield return Shot("5-tree-grown");
            yield return Move(25.5f);
            yield return Settle();
            Assert.IsTrue(motor.IsGrounded && Mathf.Abs(motor.transform.position.y - NicheLedge) < .15f,
                "沿长枝走到暗格小台，位置 " + motor.transform.position);

            yield return Shot("6-niche");
            input.interactFrame = Time.frameCount + 1;
            yield return Until(() => dialogue.IsPlaying || dialogue.IsWaitingForAction, 6f, "暗格可拨开");
            ServiceRegistry.Unregister<IInputSource>(input);
        }

        // ---------------------------------------------------------------- 驱动

        private IEnumerator Adjust(float value)
        {
            var state = GameSession.Instance.State;
            yield return Until(() => motor.IsGrounded && !motor.IsClimbingLadder && state.IsPlayerControlAllowed, 3f,
                "调整边界前站稳，位置 " + motor.transform.position + " 状态 " + state.SubState);
            boundary.BeginMouseDrag();
            Assert.IsTrue(boundary.IsAdjusting, "站稳时可以开始拖动边界，位置 " + motor.transform.position + " 状态 " + state.SubState);
            boundary.RequestAdjustment(value);
            boundary.EndMouseDrag();
            yield return null;
        }

        /// <summary>关键状态截图（Temp/L04Shots，人工核对画面与碰撞；不影响断言）。</summary>
        private static IEnumerator Shot(string name)
        {
            string dir = Path.Combine(Application.dataPath, "../Temp/L04Shots");
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        private static IEnumerator Settle() { yield return new WaitForSeconds(.4f); }

        private IEnumerator TryMove(float x)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Mathf.Abs(motor.transform.position.x - x) > .09f && Time.realtimeSinceStartup < deadline)
            {
                input.move = Mathf.Sign(x - motor.transform.position.x);
                yield return null;
            }
            input.move = 0f;
            yield return Settle();
        }

        private IEnumerator Move(float x)
        {
            yield return TryMove(x);
            Assert.LessOrEqual(Mathf.Abs(motor.transform.position.x - x), .15f, "走到 " + x + "，实际 " + motor.transform.position);
        }

        private IEnumerator Climb(float y)
        {
            input.climb = 1f;
            yield return Until(() => motor.transform.position.y >= y, 8f, "爬到 " + y);
            input.climb = 0f;
        }

        private static IEnumerator Until(Func<bool> condition, float timeout, string label)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), label);
        }

        private sealed class RouteInput : IInputSource
        {
            public float move, climb;
            public int jumpFrame = -1, interactFrame = -1;
            private int consumed = -1;
            public float MoveAxis => move;
            public float ClimbAxis => climb;
            public bool JumpPressedThisFrame => Time.frameCount == jumpFrame;
            public bool JumpHeld => JumpPressedThisFrame;
            public bool InteractPressedThisFrame => Time.frameCount == interactFrame;
            public bool ConsumeInteract()
            {
                if (!InteractPressedThisFrame || consumed == Time.frameCount) return false;
                consumed = Time.frameCount;
                return true;
            }
            public float BoundaryNudgeDirection => 0f;
            public bool RespawnPressedThisFrame => false;
            public bool ObjectiveHeld => false;
            public bool PausePressedThisFrame => false;
        }
    }
}
