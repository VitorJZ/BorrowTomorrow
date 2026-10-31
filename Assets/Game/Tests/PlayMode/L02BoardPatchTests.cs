using System.Collections;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// L02 外台补板，两段都只在单一年代里完成：
    /// 1994 年把墙边木板搬到砖台；2024 年把砖台上的旧木板铺进维修架缺口。
    /// </summary>
    public sealed class L02BoardPatchTests
    {
        private string saveDirectory;
        private GameSession session;

        [UnitySetUp]
        public IEnumerator Boot()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            session = GameSession.Instance;
            Assert.IsNotNull(session);
            saveDirectory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowL02Board_" + System.Guid.NewGuid());
            Directory.CreateDirectory(saveDirectory);
            typeof(SaveService).GetField("saveDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Object.FindObjectOfType<SaveService>(), saveDirectory);
        }

        private IEnumerator WaitForPlaying()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (session.State.Phase != GamePhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(GamePhase.Playing, session.State.Phase);
            yield return new WaitForSeconds(.6f);
        }

        private void DragTo(float position)
        {
            var boundary = (BoundaryController)ServiceRegistry.Get<IBoundaryContext>();
            boundary.BeginMouseDrag();
            Assert.IsTrue(boundary.IsAdjusting, "A grounded player can drag the time boundary.");
            boundary.RequestAdjustment(position);
            boundary.EndMouseDrag();
            Assert.AreEqual(position, boundary.CommittedBoundary, .01f);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + name);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + name);
            return (T)field.GetValue(target);
        }

        [UnityTest]
        public IEnumerator PlankMovesOnBenchIn1994_ThenBridgesGapIn2024()
        {
            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying();
            var patch = Object.FindObjectOfType<L02BoardPatch>(true);
            Assert.IsNotNull(patch, "L02 exterior must carry the board patch interactable.");
            var temporals = Object.FindObjectsOfType<TemporalObject>(true);
            var plank = temporals.Single(t => t.ObjectId == "L02_plank");
            var maintenance = temporals.Single(t => t.ObjectId == "L02_maintenance");
            var gap = maintenance.transform.Find("Present/GapBoard");
            var boardArt = plank.transform.Find("Past/ArtSkin");
            var bench = Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "BenchBoard").gameObject;
            Assert.IsNotNull(gap, "Maintenance gap board must exist under the present root.");
            Assert.IsNotNull(boardArt, "Past plank art must exist.");
            Assert.IsNotNull(bench, "Bench board must exist.");
            Assert.IsFalse(gap.gameObject.activeSelf, "The gap starts open.");
            Assert.IsFalse(bench.activeSelf, "The bench board starts hidden.");

            DragTo(0f);
            yield return null;
            Assert.AreEqual(Era.Present, plank.CurrentEra);
            Assert.IsFalse(patch.CanInteract, "2024 年墙边没有木板，不能搬。");

            DragTo(26f);
            yield return null;
            Assert.AreEqual(Era.Past, plank.CurrentEra, "全 1994：墙边木板可见。");
            Assert.IsTrue(patch.CanInteract, "1994 年可以把墙边木板搬到砖台上。");
            Assert.AreEqual("把木板搬到砖台上", patch.InteractPrompt);
            Assert.IsTrue(patch.Interact());
            yield return new WaitForSeconds(1.6f);
            Assert.IsTrue(session.StoryFlags.HasFlag(L02BoardPatch.PlankMovedFlag));
            Assert.IsTrue(bench.activeSelf, "搬板后砖台上出现旧木板。");
            Assert.IsFalse(boardArt.gameObject.activeSelf, "墙边木板已搬走。");
            Assert.IsFalse(session.StoryFlags.HasFlag(L02BoardPatch.BoardPlacedFlag));

            DragTo(0f);
            yield return null;
            Assert.AreEqual(Era.Present, maintenance.CurrentEra, "全 2024：维修架存在。");
            Assert.IsTrue(bench.activeSelf, "2024 年砖台上仍留着那块旧木板。");
            Assert.IsTrue(patch.CanInteract, "2024 年可以抓住砖台上的旧木板。");
            Assert.AreEqual("抓住旧木板（A/D 推，E 放下）", patch.InteractPrompt);
            Assert.IsTrue(patch.Interact(), "按 E 抓住木板。");
            yield return null;
            Assert.IsTrue(GetField<bool>(patch, "dragging"), "抓住后进入拖动状态。");
            SetField(patch, "boardX", 6.9f);
            Assert.IsTrue(patch.Interact(), "对准后按 E 放下。");
            yield return null;
            Assert.IsTrue(session.StoryFlags.HasFlag(L02BoardPatch.BoardPlacedFlag));
            Assert.IsTrue(gap.gameObject.activeSelf, "搭板后缺口碰撞体启用。");
            Assert.IsFalse(bench.activeSelf, "砖台上的木板已移走。");
            Assert.IsFalse(boardArt.gameObject.activeSelf);

            DragTo(26f);
            yield return null;
            Assert.IsFalse(gap.gameObject.activeInHierarchy, "维修架处于过去态时补板随现在态根隐藏。");
            DragTo(0f);
            yield return null;
            Assert.IsTrue(gap.gameObject.activeInHierarchy, "切回现在态补板重新出现。");
            Assert.IsFalse(patch.CanInteract, "完成后的搭板不再重复触发。");
            session.SaveProgressNow();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovedPlankSurvivesReloadBeforePlacement()
        {
            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying();
            DragTo(26f);
            yield return null;
            Assert.IsTrue(Object.FindObjectOfType<L02BoardPatch>(true).Interact());
            yield return new WaitForSeconds(1.6f);
            session.SaveProgressNow();
            yield return null;

            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying();
            var bench = Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "BenchBoard").gameObject;
            Assert.IsTrue(session.StoryFlags.HasFlag(L02BoardPatch.PlankMovedFlag));
            Assert.IsTrue(bench.activeSelf, "读档后砖台上的旧木板仍在。");
            Assert.IsFalse(session.StoryFlags.HasFlag(L02BoardPatch.BoardPlacedFlag));
        }

        [UnityTest]
        public IEnumerator MisalignedBoardSlidesBackAndCanRetry()
        {
            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying();
            var patch = Object.FindObjectOfType<L02BoardPatch>(true);
            DragTo(26f);
            yield return null;
            Assert.IsTrue(patch.Interact());
            yield return new WaitForSeconds(1.6f);
            DragTo(0f);
            yield return null;

            Assert.IsTrue(patch.Interact(), "抓住木板。");
            yield return null;
            SetField(patch, "boardX", 8.0f);
            Assert.IsTrue(patch.Interact(), "未对准就放下。");
            yield return new WaitForSeconds(.7f);
            Assert.IsFalse(session.StoryFlags.HasFlag(L02BoardPatch.BoardPlacedFlag), "没推进缺口不能完成。");
            var bench = Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "BenchBoard").gameObject;
            Assert.IsTrue(bench.activeSelf, "木板滑回砖台，可重试。");
            Assert.AreEqual(9.6f, bench.transform.position.x, .05f, "木板回到砖台原位。");

            Assert.IsTrue(patch.Interact(), "可以再试一次。");
            yield return null;
            SetField(patch, "boardX", 6.9f);
            Assert.IsTrue(patch.Interact());
            yield return null;
            Assert.IsTrue(session.StoryFlags.HasFlag(L02BoardPatch.BoardPlacedFlag));
        }

        [UnityTest]
        public IEnumerator PlacedBoardSurvivesLevelReload()
        {
            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying();
            var patch = Object.FindObjectOfType<L02BoardPatch>(true);
            DragTo(26f);
            yield return null;
            Assert.IsTrue(patch.Interact());
            yield return new WaitForSeconds(1.6f);
            DragTo(0f);
            yield return null;
            Assert.IsTrue(patch.Interact(), "按 E 抓住木板。");
            yield return null;
            SetField(patch, "boardX", 6.9f);
            Assert.IsTrue(patch.Interact(), "对准后按 E 放下。");
            yield return null;
            Assert.IsTrue(session.StoryFlags.HasFlag(L02BoardPatch.BoardPlacedFlag));
            session.SaveProgressNow();
            yield return null;

            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying();
            var maintenance = Object.FindObjectsOfType<TemporalObject>(true)
                .Single(t => t.ObjectId == "L02_maintenance");
            var gap = maintenance.transform.Find("Present/GapBoard");
            Assert.IsTrue(session.StoryFlags.HasFlag(L02BoardPatch.BoardPlacedFlag), "读档后旗标仍在。");
            Assert.IsTrue(gap.gameObject.activeSelf, "读档后补板恢复到维修架上。");
            var bench = Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "BenchBoard").gameObject;
            Assert.IsFalse(bench.activeSelf, "读档后砖台上的木板已移走。");
            DragTo(0f);
            yield return null;
            Assert.IsFalse(Object.FindObjectOfType<L02BoardPatch>(true).CanInteract, "已完成后的搭板不再重复触发。");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (session != null) Object.Destroy(session.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
            yield return null;
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }
    }
}
