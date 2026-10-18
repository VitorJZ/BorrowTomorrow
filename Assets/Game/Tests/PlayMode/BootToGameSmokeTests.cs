using System.Collections;
using System.Linq;
using System.IO;
using System.Reflection;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Save;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Player;
using BorrowTomorrow.TimeBoundary;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 玩法冒烟测试（播放模式）：复现“从 Bootstrap 启动 → 主菜单 → 开始新游戏 →
    /// 进入 L00 → 玩家落地 → 边界服务就绪”的完整流程。
    /// 用户报告“一直提示先到稳固的地方”即此链路的 IsGrounded/注册断言失败（策划 15.4）。
    /// </summary>
    public sealed class BootToGameSmokeTests
    {
        private string saveDirectory;

        private void IsolateSave()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowPlayTests_" + System.Guid.NewGuid());
            Directory.CreateDirectory(saveDirectory);
            var service = Object.FindObjectOfType<SaveService>();
            Assert.IsNotNull(service);
            typeof(SaveService).GetField("saveDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(service, saveDirectory);
        }

        private static IEnumerator WaitForPlaying(GameSession session)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (session.State.Phase != GamePhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(GamePhase.Playing, session.State.Phase);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (GameSession.Instance != null) Object.Destroy(GameSession.Instance.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
            yield return null;
            if (!string.IsNullOrEmpty(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator BootstrapToL00_PlayerGroundedAndBoundaryReady()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f); // Bootstrap.Start → 同步进入主菜单

            GameSession session = GameSession.Instance;
            Assert.IsNotNull(session, "Bootstrap 加载后 GameSession.Instance 应存在");
            Assert.AreEqual(GamePhase.MainMenu, session.State.Phase, "应处于主菜单阶段");

            IsolateSave();
            session.StartNewGame();
            float elapsed = 0f;
            while (session.State.Phase != GamePhase.Playing && elapsed < 20f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(GamePhase.Playing, session.State.Phase, $"20 秒内应进入 Playing（等待了 {elapsed:0.0}s）");

            Assert.IsTrue(ServiceRegistry.TryGet<IPlayerContext>(out var player),
                "IPlayerContext 应已注册（玩家未生成则 PlacePlayer/预制体有问题）");
            Assert.IsTrue(player.IsPresent, "玩家应存活在场");
            float groundedTimeout = Time.realtimeSinceStartup + 5f;
            while (!player.IsGrounded && Time.realtimeSinceStartup < groundedTimeout) yield return null;
            Assert.IsTrue(player.IsGrounded,
                $"玩家应落地站稳（pos={player.Position}）——IsGrounded 为假即用户所报“先到稳固的地方”无法消除的直接原因");
            Assert.IsFalse(player.IsClimbingLadder, "平地不应处于攀爬状态");

            Assert.IsTrue(ServiceRegistry.TryGet<IBoundaryContext>(out var boundary),
                "IBoundaryContext 应已注册（激活房间未接线则把手/QR全部失效）");
            Assert.IsTrue(ServiceRegistry.TryGet<ILevelContext>(out var level),
                "ILevelContext 应已注册（LevelDirector 缺失则出口条件永远不满足）");

            // 门口地面探测：把手/QR 的“可开始调整”判定与 IsGrounded 同源，此处直接断言。
            Assert.IsTrue(player.IsGrounded && !player.IsClimbingLadder,
                "边界调整前置条件（落地且非攀爬）应成立");
        }

        private sealed class WalkInput : IInputSource
        {
            public float MoveAxis { get; set; }
            public float ClimbAxis => 0f;
            public bool JumpPressedThisFrame => false;
            public bool JumpHeld => false;
            public bool InteractPressedThisFrame => false;
            public bool ConsumeInteract() => false;
            public float BoundaryNudgeDirection => 0f;
            public bool RespawnPressedThisFrame => false;
            public bool ObjectiveHeld => false;
            public bool PausePressedThisFrame => false;
        }

        [UnityTest]
        public IEnumerator L00_NoticeFollowsEraAndClearedCratesAllowWalkingBothWays()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            var session = GameSession.Instance;
            IsolateSave();
            session.StartNewGame();
            yield return WaitForPlaying(session);
            yield return null;
            var dialogue = ServiceRegistry.Get<IDialogueService>();
            float introDeadline = Time.realtimeSinceStartup + 5f;
            while (!dialogue.IsPlaying && Time.realtimeSinceStartup < introDeadline) yield return null;
            Assert.IsTrue(dialogue.IsPlaying, "应先等待开场对白实际开始");
            dialogue.SkipCurrent();
            yield return null;
            yield return null;

            var boundary = (BoundaryController)ServiceRegistry.Get<IBoundaryContext>();
            var notice = Object.FindObjectOfType<PresentOnlyInvestigation>();
            var crates = Object.FindObjectsOfType<TemporalObject>().Single(t => t.ObjectId == "L00_fence_gate");
            Assert.Greater(notice.anchorX, crates.AnchorX, "通知应位于木箱之后的必经路上");
            Assert.Less(notice.anchorX, Object.FindObjectOfType<NarrativeDogEcho>().anchorX, "通知应在阿黄之前");
            var interaction = notice.GetComponent<NarrativeInteractable>();
            var art = notice.transform.Find("ArtSkin").GetComponent<SpriteRenderer>();
            var graybox = notice.GetComponent<SpriteRenderer>();
            var area = notice.GetComponent<Collider2D>();
            foreach (float position in new[] { 26f, 0f, 26f, 0f })
            {
                boundary.RestoreBoundary(position);
                yield return null;
                yield return null;
                bool present = position < notice.anchorX;
                Assert.AreEqual(present, art.enabled, "通知子物体的美术必须跟随年代");
                Assert.AreEqual(present, area.enabled, "过去态不得保留调查范围");
                Assert.AreEqual(present, interaction.CanInteract);
                Assert.IsFalse(graybox.enabled, "切回现在不得重新显示灰盒贴图");
                Assert.AreEqual(present, interaction.Interact(), "过去态不得直接触发通知对白");
                if (present) dialogue.SkipCurrent();
            }

            var motor = Object.FindObjectOfType<PlayerMotor2D>();
            var input = new WalkInput { MoveAxis = 1f };
            typeof(PlayerMotor2D).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(motor, input);
            boundary.RestoreBoundary(26f);
            motor.TeleportTo(new Vector2(7f, .05f));
            yield return new WaitForSeconds(1.5f);
            Assert.Less(motor.transform.position.x, 8f, "过去堆放的木箱必须阻挡玩家");

            boundary.RestoreBoundary(0f);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(motor.transform.position.x, 12f, "现在木箱清走后应能从左侧沿平地通过");
            input.MoveAxis = -1f;
            yield return new WaitForSeconds(1.8f);
            Assert.Less(motor.transform.position.x, 8f, "清空后的巷道也应能向左返回");
            input.MoveAxis = 0f;
        }

        [UnityTest]
        public IEnumerator L00_DogEchoRequiresVisiblePastDogAndStopsWhenHidden()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            var session = GameSession.Instance;
            IsolateSave();
            session.StartNewGame();
            yield return WaitForPlaying(session);
            var dialogue = (DialoguePlayer)ServiceRegistry.Get<IDialogueService>();
            float introDeadline = Time.realtimeSinceStartup + 5f;
            while (!dialogue.IsPlaying && Time.realtimeSinceStartup < introDeadline) yield return null;
            Assert.IsTrue(dialogue.IsPlaying);
            dialogue.SkipCurrent();

            var boundary = (BoundaryController)ServiceRegistry.Get<IBoundaryContext>();
            var dog = Object.FindObjectOfType<NarrativeDogEcho>();
            var dogEvent = Object.FindObjectsOfType<NarrativeAutoEvent>()
                .Single(e => e.entry.entryId == "DLG_L00_DOG_01");
            Assert.AreSame(dog, dogEvent.requiredVisibleDog, "场景事件必须使用实际阿黄的年代条件");
            var player = ServiceRegistry.Get<IPlayerContext>();
            boundary.RestoreBoundary(0f);
            player.TeleportToPosition(new Vector2(14f, .05f));
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(player.IsGrounded);
            Assert.IsFalse(dog.GetComponent<SpriteRenderer>().enabled);
            Assert.IsFalse(dialogue.IsPlaying, "现代空狗窝旁不得触发阿黄台词");
            Assert.IsFalse(session.StoryFlags.HasFlag("DogEchoPlayed"));

            boundary.RestoreBoundary(dog.anchorX);
            yield return new WaitForSeconds(.2f);
            Assert.IsFalse(dialogue.IsPlaying, "边界恰在锚点时，阿黄仍属现在态");
            player.TeleportToPosition(new Vector2(18f, .05f));
            boundary.RestoreBoundary(26f);
            yield return new WaitForSeconds(.8f);
            Assert.IsFalse(dialogue.IsPlaying, "离开阿黄附近后不得补播回声");

            player.TeleportToPosition(new Vector2(14f, .05f));
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(dog.GetComponent<SpriteRenderer>().enabled);
            Assert.AreEqual("DLG_L00_DOG_01", dialogue.Current?.entryId);
            StringAssert.DoesNotContain("你脚边", dialogue.Current.subtitle);
            var dogVoice = (AudioSource)typeof(DialoguePlayer)
                .GetField("voiceSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dialogue);
            if (dialogue.Current.voiceClip != null)
            {
                Assert.AreEqual("vo_l00_dog_01", dialogue.Current.voiceClip.name);
                Assert.AreSame(dialogue.Current.voiceClip, dogVoice.clip);
                Assert.IsTrue(dogVoice.isPlaying, "已接入的阿黄配音应随回声开始播放");
            }
            boundary.RestoreBoundary(0f);
            yield return new WaitForSeconds(.2f);
            Assert.IsFalse(dialogue.IsPlaying, "播放中阿黄消失时应停止回声");
            Assert.IsFalse(dogVoice.isPlaying, "阿黄消失时不得继续播放回声配音");
            Assert.IsFalse(session.StoryFlags.HasFlag("DogEchoPlayed"), "中止不得写入已完成标记");

            boundary.RestoreBoundary(26f);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual("DLG_L00_DOG_01", dialogue.Current?.entryId, "切回过去后仍可触发");
            float echoSeconds = Mathf.Max(3f, dialogue.Current.voiceClip != null ? dialogue.Current.voiceClip.length : 0f);
            yield return new WaitForSeconds(echoSeconds + .3f);
            Assert.IsFalse(dialogue.IsPlaying);
            Assert.IsTrue(session.StoryFlags.HasFlag("DogEchoPlayed"));
            boundary.RestoreBoundary(0f);
            yield return null;
            boundary.RestoreBoundary(26f);
            yield return new WaitForSeconds(.2f);
            Assert.IsFalse(dialogue.IsPlaying, "完整播放后不重复触发");
        }

        [UnityTest]
        public IEnumerator ColdContinue_RestoresL01BThenNewLevelUsesOwnSnapshot()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            var session = GameSession.Instance;
            IsolateSave();
            var data = new SaveData { currentLevelId = "L01", checkpointId = "cp_L01B_entry", currentObjective = "旧目标" };
            data.roomBoundaries.Add(new RoomBoundarySnapshot("L01A", 12f));
            data.roomBoundaries.Add(new RoomBoundarySnapshot("L01B", 69f));
            data.triggeredStoryIds.Add(StoryFlags.IdentityKnown);
            Assert.IsTrue(ServiceRegistry.Get<ISaveService>().SaveProgress(data));
            session.ContinueGame();
            yield return WaitForPlaying(session);
            Assert.AreEqual("L01B", session.ActiveRoom.RoomId);
            var player = ServiceRegistry.Get<IPlayerContext>();
            Assert.IsTrue(player.IsPresent);
            Assert.Greater(player.Position.x, 40f);
            Assert.AreEqual(40f, ServiceRegistry.Get<IBoundaryContext>().RangeMin);
            Assert.AreEqual(Objectives.TellXiaoxaoBoxLocation, session.CurrentObjective);
            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying(session);
            var checkpoint = ServiceRegistry.Get<ICheckpointService>();
            var written = new SaveData { currentLevelId = "L02" };
            checkpoint.WriteTo(written);
            Assert.AreEqual("L02", written.checkpointRoomId);
            Assert.AreEqual(1, written.roomBoundaries.Count);
            checkpoint.RestoreLatest();
            Assert.AreEqual("L02", session.ActiveRoom.RoomId);
            Assert.Less(ServiceRegistry.Get<IPlayerContext>().Position.x, 40f);
        }

        [UnityTest]
        public IEnumerator LegacySaveWithoutCheckpoint_CreatesPlayerAtEntryAndPauseFreezesPhysics()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            var session = GameSession.Instance;
            IsolateSave();
            Assert.IsTrue(ServiceRegistry.Get<ISaveService>().SaveProgress(new SaveData { currentLevelId = "L00" }));
            session.ContinueGame();
            yield return WaitForPlaying(session);
            var player = ServiceRegistry.Get<IPlayerContext>();
            Assert.IsTrue(player.IsPresent);
            var entry = session.ActiveRoom.FindSpawn(session.CurrentLevel.spawnId);
            Assert.Less(Vector2.Distance(entry.Position, player.Position), 0.5f);
            player.TeleportToPosition(player.Position + Vector2.up * 3f);
            session.State.EnterPause();
            var pausedPosition = player.Position;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.Less(Vector2.Distance(pausedPosition, player.Position), 0.01f);
            session.State.ExitPause();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
        }
        private static IEnumerator AdvanceUntil(DialoguePlayer dialogue, string entryId)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((dialogue.Current == null || dialogue.Current.entryId != entryId) && Time.realtimeSinceStartup < deadline)
            {
                dialogue.Advance();
                yield return new WaitForSeconds(0.25f);
            }
            Assert.AreEqual(entryId, dialogue.Current?.entryId);
        }
        [UnityTest]
        public IEnumerator Story_RevealBoardReportSmoothingAndHistoryPersist()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            IsolateSave();
            var session = GameSession.Instance;
            session.LoadLevel(session.FindLevel("L03"));
            yield return WaitForPlaying(session);
            var director = ServiceRegistry.Get<NarrativeDirector>();
            var dialogue = (DialoguePlayer)ServiceRegistry.Get<IDialogueService>();
            session.StoryFlags.SetFlag(StoryFlags.UniformRead);
            Assert.IsTrue(director.PlayStory("DLG_L03_REVEAL_01", DialoguePriority.Reveal));
            yield return null;
            Assert.IsFalse(session.StoryFlags.HasFlag(StoryFlags.IdentityKnown));
            string capture = System.Environment.GetEnvironmentVariable("BORROWTOMORROW_STORY_CAPTURE");
            if (!string.IsNullOrEmpty(capture))
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(capture, "story-photo.png"));
                yield return new WaitForSecondsRealtime(0.2f);
            }
            dialogue.SkipCurrent();
            Assert.IsTrue(session.StoryFlags.HasFlag(StoryFlags.IdentityKnown));
            Assert.IsTrue(session.StoryFlags.HasFlag(StoryFlags.PhotoRevealed));
            Assert.IsFalse(director.PlayStory("DLG_L03_REVEAL_01", DialoguePriority.Reveal));

            // L04: board action plays first, then waits for a separate "tell Xiaoxiao" confirmation.
            session.LoadLevel(session.FindLevel("L04"));
            yield return WaitForPlaying(session);
            yield return null;
            if (dialogue.IsPlaying) dialogue.SkipCurrent();
            session.StoryFlags.SetFlag("CompartmentSeen");
            Assert.IsTrue(director.PlayStory("DLG_L04_BOX_01", DialoguePriority.Reveal));
            yield return null;
            Assert.IsNull(dialogue.Current, "拨板动作期间不显示报位置台词");
            Assert.IsFalse(dialogue.IsWaitingForAction, "拨板动作播放中不等待确认");
            float actionDeadline = Time.realtimeSinceStartup + 5f;
            while (!dialogue.IsWaitingForAction && Time.realtimeSinceStartup < actionDeadline) yield return null;
            Assert.IsTrue(dialogue.IsWaitingForAction, "拨板后等待“告诉晓晓位置”");
            Assert.IsNull(dialogue.Current);
            yield return new WaitForSeconds(0.3f);
            dialogue.Advance();
            yield return null;
            Assert.AreEqual("DLG_L04_BOX_01", dialogue.Current?.entryId);
            Assert.IsFalse(session.StoryFlags.HasFlag(StoryFlags.BoxLocated), "进入报告链不等于完成");
            dialogue.SkipCurrent();
            Assert.IsTrue(session.StoryFlags.HasFlag(StoryFlags.BoxLocated));

            float deadline = Time.realtimeSinceStartup + 15f;
            while ((session.CurrentLevel.levelId != "Epilogue" || session.State.Phase != GamePhase.Playing)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual("Epilogue", session.CurrentLevel.levelId);
            deadline = Time.realtimeSinceStartup + 10f;
            while (!dialogue.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(dialogue.IsPlaying, "尾声自动开始");
            yield return AdvanceUntil(dialogue, "DLG_EPI_06");
            yield return new WaitForSeconds(0.25f);
            dialogue.Advance();
            Assert.IsTrue(dialogue.IsWaitingForAction, "第 6 条后等待抚平布角");
            dialogue.Advance();
            Assert.IsTrue(dialogue.IsWaitingForAction, "翻页输入不能同时触发抚平");
            yield return new WaitForSeconds(0.3f);
            dialogue.Advance();
            Assert.IsFalse(dialogue.IsWaitingForAction);
            Assert.AreEqual("DLG_EPI_06", dialogue.Current?.entryId, "动作结束前不播放第 7 条");
            float smoothDeadline = Time.realtimeSinceStartup + 5f;
            while (dialogue.Current?.entryId != "DLG_EPI_07" && Time.realtimeSinceStartup < smoothDeadline) yield return null;
            Assert.AreEqual("DLG_EPI_07", dialogue.Current?.entryId);
            Assert.IsFalse(session.StoryFlags.HasFlag(StoryFlags.EpilogueSeen));
            yield return AdvanceUntil(dialogue, "DLG_EPI_08");
            session.SaveProgressNow();
            var save = ServiceRegistry.Get<ISaveService>();
            Assert.AreEqual(SaveLoadResult.Loaded, save.TryLoadProgress(out var data));
            CollectionAssert.Contains(data.readDialogueIds, "DLG_EPI_07");
            dialogue.SkipCurrent();
            Assert.IsTrue(session.StoryFlags.HasFlag(StoryFlags.EpilogueSeen));
        }
        private static bool IsVisible(GameObject go)
        {
            if (!go.activeInHierarchy) return false;
            return go.GetComponentsInParent<CanvasGroup>().All(group => group.alpha > 0.01f);
        }

        private static Transform HudElement(string name)
        {
            var hud = Object.FindObjectOfType<BorrowTomorrow.UI.HudService>();
            return hud.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        }

        private static void AssertMenuClean()
        {
            foreach (string name in new[] { "BoundaryHandle", "EraLabelPast", "EraLabelPresent", "SubtitlePanel",
                "ObjectivePanel", "BlockedToast" })
                Assert.IsFalse(IsVisible(HudElement(name).gameObject), "菜单不得显示 " + name);
            var guide = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "CommittedBoundaryGuide");
            if (guide != null) Assert.IsFalse(IsVisible(guide.gameObject), "菜单不得显示边界辅助线");
            Assert.IsTrue(Object.FindObjectsOfType<UnityEngine.UI.Text>().Any(t => t.text == "开始新游戏"));
        }

        [UnityTest]
        public IEnumerator MenuHud_NewGamePauseReturnContinueAndEveryLevel()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            IsolateSave();
            var session = GameSession.Instance;
            AssertMenuClean();
            Assert.IsFalse(HudElement("SubtitlePanel").Find("Root").gameObject.activeSelf,
                "没有对白时字幕黑底必须默认隐藏");
            Canvas.ForceUpdateCanvases();
            var start = Object.FindObjectsOfType<UnityEngine.UI.Button>()
                .Single(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text == "开始新游戏");
            var rect = (RectTransform)start.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { position = position, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "新游戏按钮必须可被指针命中");
            var hitButton = hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>();
            Assert.AreSame(start, hitButton, "新游戏按钮不能被常驻 HUD 或透明面板遮住");
            UnityEngine.EventSystems.ExecuteEvents.Execute(start.gameObject, pointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return WaitForPlaying(session);
            yield return null;
            var dialogue = ServiceRegistry.Get<IDialogueService>();
            dialogue.SkipCurrent();
            yield return null;
            yield return null;
            Assert.IsTrue(IsVisible(HudElement("BoundaryHandle").gameObject));
            var hud = ServiceRegistry.Get<IHudService>();
            hud.ShowSubtitle("测试", "暂停前的当前句");
            hud.ShowBoundaryBlocked("旧关卡受阻提示");
            session.State.EnterPause();
            yield return null;
            yield return null;
            Assert.IsFalse(IsVisible(HudElement("BoundaryHandle").gameObject));
            Assert.IsTrue(IsVisible(HudElement("PauseMenu").Find("Root").gameObject));
            session.State.ExitPause();
            yield return null;
            yield return null;
            Assert.IsTrue(IsVisible(HudElement("SubtitlePanel").Find("Root").gameObject));
            session.State.EnterPause();
            session.ReturnToMainMenu();
            yield return null;
            yield return null;
            AssertMenuClean();
            Assert.IsNull(session.CurrentLevel);
            Assert.IsNull(session.ActiveRoom);
            Assert.IsFalse(ServiceRegistry.TryGet<IBoundaryContext>(out _), "菜单不得残留已销毁房间的边界服务");
            Assert.IsNull(session.CurrentObjective);
            Assert.IsFalse(HudElement("PauseMenu").Find("Root").gameObject.activeSelf);
            Assert.AreEqual(1f, Time.timeScale);
            session.ContinueGame();
            yield return WaitForPlaying(session);
            yield return null;
            dialogue.SkipCurrent();
            foreach (string id in new[] { "L01", "L02", "L03", "L04", "Epilogue" })
            {
                session.LoadLevel(session.FindLevel(id));
                yield return WaitForPlaying(session);
                yield return null;
                dialogue.SkipCurrent();
                yield return null;
                Assert.AreEqual(id, session.CurrentLevel.levelId);
                Assert.IsTrue(ServiceRegistry.Get<IPlayerContext>().IsPresent);
                Assert.AreEqual(1, Object.FindObjectsOfType<BorrowTomorrow.UI.HudService>().Length);
                Assert.AreEqual(1, Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length);
            }
            session.ReturnToMainMenu();
            yield return null;
            yield return null;
            AssertMenuClean();
        }

        [UnityTest]
        public IEnumerator PauseSettings_LoadDoesNotOverwritePreferencesAndFadeResetIsImmediate()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            IsolateSave();
            var save = ServiceRegistry.Get<ISaveService>();
            var expected = new SaveSettings { volumeMaster = 0.23f, volumeMusic = 0.47f,
                volumeVoice = 0.71f, subtitlesEnabled = false, screenShakeEnabled = false };
            save.SaveSettings(expected);
            var pause = Object.FindObjectOfType<BorrowTomorrow.UI.PauseMenu>();
            foreach (string field in new[] { "masterVolume", "musicVolume", "voiceVolume" })
            {
                var control = new GameObject(field, typeof(RectTransform), typeof(UnityEngine.UI.Slider));
                control.transform.SetParent(pause.transform, false);
                var slider = control.GetComponent<UnityEngine.UI.Slider>();
                slider.onValueChanged.AddListener(_ => typeof(BorrowTomorrow.UI.PauseMenu)
                    .GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pause, null));
                typeof(BorrowTomorrow.UI.PauseMenu).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(pause, slider);
            }
            typeof(BorrowTomorrow.UI.PauseMenu).GetMethod("LoadCurrentSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(pause, null);
            var actual = save.LoadSettings();
            Assert.AreEqual(expected.volumeMaster, actual.volumeMaster);
            Assert.AreEqual(expected.volumeMusic, actual.volumeMusic);
            Assert.AreEqual(expected.volumeVoice, actual.volumeVoice);
            Assert.IsFalse(actual.subtitlesEnabled);
            var fade = Object.FindObjectOfType<BorrowTomorrow.UI.FadeScreen>();
            fade.FadeOut(0f);
            Assert.AreEqual(1f, fade.GetComponent<UnityEngine.UI.Image>().color.a);
            fade.FadeIn(0f);
            Assert.IsFalse(fade.GetComponent<UnityEngine.UI.Image>().enabled);
        }

        [UnityTest]
        public IEnumerator CorruptedContinue_ShowsWarningAndKeepsOriginalFiles()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            IsolateSave();
            string path = Path.Combine(saveDirectory, "progress.json");
            File.WriteAllText(path, "broken save");
            var menu = Object.FindObjectOfType<BorrowTomorrow.UI.MainMenuUi>();
            menu.OnContinue();
            yield return null;
            Assert.AreEqual(GamePhase.MainMenu, GameSession.Instance.State.Phase);
            Assert.AreEqual("broken save", File.ReadAllText(path));
            var warning = GameObject.Find("CorruptedSaveWarning");
            Assert.IsNotNull(warning);
            StringAssert.Contains("无法读取", warning.GetComponent<UnityEngine.UI.Text>().text);
        }

        [UnityTest]
        public IEnumerator HudLayout_StaysInsideWideAndTallScreensAndSubtitlesDoNotOverlap()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            IsolateSave();
            var session = GameSession.Instance;
            session.StartNewGame();
            yield return WaitForPlaying(session);
            var canvas = Object.FindObjectOfType<BorrowTomorrow.UI.HudService>().GetComponent<Canvas>();
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            var originalScale = scaler.uiScaleMode;
            var originalMode = canvas.renderMode;
            var canvasRect = (RectTransform)canvas.transform;
            scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            foreach (var size in new[] { new Vector2(1920f, 1080f), new Vector2(2560f, 1080f), new Vector2(1024f, 768f) })
            {
                canvasRect.sizeDelta = size;
                Canvas.ForceUpdateCanvases();
                foreach (string name in new[] { "EraLabelPast", "EraLabelPresent", "BoundaryHandle" })
                {
                    var rect = (RectTransform)HudElement(name);
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        Vector3 local = canvasRect.InverseTransformPoint(corner);
                        Assert.That(local.y, Is.InRange(canvasRect.rect.yMin - 0.1f, canvasRect.rect.yMax + 0.1f), name + " vertical bounds " + size);
                    }
                }
                var speaker = (RectTransform)HudElement("SpeakerText");
                var content = (RectTransform)HudElement("ContentText");
                var speakerCorners = new Vector3[4]; var contentCorners = new Vector3[4];
                speaker.GetWorldCorners(speakerCorners); content.GetWorldCorners(contentCorners);
                Assert.Greater(speaker.parent.InverseTransformPoint(speakerCorners[0]).y,
                    content.parent.InverseTransformPoint(contentCorners[1]).y, "说话人和正文必须保留间隔");
            }
            canvas.renderMode = originalMode;
            scaler.uiScaleMode = originalScale;
            scaler.enabled = true;
            var settings = ServiceRegistry.Get<ISaveService>().LoadSettings();
            settings.subtitlesEnabled = false;
            ServiceRegistry.Get<ISaveService>().SaveSettings(settings);
            ServiceRegistry.Get<IHudService>().ShowSubtitle("测试", "开关应生效");
            Assert.IsFalse(HudElement("SubtitlePanel").Find("Root").gameObject.activeSelf);
            Object.FindObjectOfType<BorrowTomorrow.UI.HudService>().SetSubtitlesEnabled(true);
            Assert.IsTrue(HudElement("SubtitlePanel").Find("Root").gameObject.activeSelf);
        }

    }
}
