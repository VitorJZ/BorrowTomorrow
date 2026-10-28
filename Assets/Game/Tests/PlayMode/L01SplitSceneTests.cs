using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Save;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    public sealed class L01SplitSceneTests
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
            saveDirectory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowL01Tests_" + System.Guid.NewGuid());
            Directory.CreateDirectory(saveDirectory);
            typeof(SaveService).GetField("saveDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Object.FindObjectOfType<SaveService>(), saveDirectory);
        }

        private IEnumerator WaitForPlaying()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (session.State.Phase != GamePhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(GamePhase.Playing, session.State.Phase);
            yield return new WaitForSeconds(.5f);
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

        [UnityTest]
        public IEnumerator CanalDoorLoadsTreeScene_ThenTreeDoorLoadsSewingYard()
        {
            session.LoadLevel(session.FindLevel("L01"));
            yield return WaitForPlaying();
            Assert.AreEqual("L01A", session.ActiveRoom.RoomId);
            Assert.AreEqual(1, Object.FindObjectsOfType<LevelRoot>(true).Length);
            Assert.IsTrue(Object.FindObjectOfType<ExitDoor>().Interact());
            yield return WaitForPlaying();
            Assert.AreEqual("L01_TreeCourtyard", SceneManager.GetActiveScene().name);
            Assert.AreEqual("L01B", session.CurrentLevel.levelId);
            Assert.AreEqual("L01B", session.ActiveRoom.RoomId);
            Assert.AreEqual(1, Object.FindObjectsOfType<LevelRoot>(true).Length);
            Assert.IsNotNull(Object.FindObjectOfType<RopeDryingRack>());
            Assert.AreEqual(40f, ServiceRegistry.Get<IBoundaryContext>().RangeMin);
            Assert.AreEqual(69f, ServiceRegistry.Get<IBoundaryContext>().RangeMax);
            Assert.Greater(ServiceRegistry.Get<IPlayerContext>().Position.x, 40f);
            Assert.IsTrue(Object.FindObjectOfType<ExitDoor>().Interact());
            yield return WaitForPlaying();
            Assert.AreEqual("L02", session.CurrentLevel.levelId);
        }

        private IEnumerator RestoreLegacy(bool explicitRoom, string checkpoint)
        {
            var data = new SaveData
            {
                currentLevelId = "L01", checkpointRoomId = explicitRoom ? "L01B" : null,
                checkpointId = checkpoint,
                roomBoundaries = new System.Collections.Generic.List<RoomBoundarySnapshot>
                { new RoomBoundarySnapshot("L01A", 12f), new RoomBoundarySnapshot("L01B", 52.5f) }
            };
            data.triggeredStoryIds.Add(RopeDryingRack.ReleasedFlag);
            data.triggeredStoryIds.Add(RopeDryingRack.RoutedFlag);
            data.triggeredStoryIds.Add(RopeDryingRack.SecuredFlag);
            data.mechanismStates.Add(new MechanismStateSnapshot("L01B_tree_branch", Era.Present));
            Assert.IsTrue(ServiceRegistry.Get<ISaveService>().SaveProgress(data));
            session.ContinueGame();
            yield return WaitForPlaying();
            Assert.AreEqual("L01_TreeCourtyard", SceneManager.GetActiveScene().name);
            Assert.AreEqual("L01B", session.CurrentLevel.levelId);
            Assert.AreEqual("L01B", session.ActiveRoom.RoomId);
            Assert.AreEqual(52.5f, ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary, .01f);
            Assert.IsTrue(Object.FindObjectOfType<RopeDryingRack>().SupportValid);
            Assert.IsTrue(Object.FindObjectOfType<RopeDryingRack>().deck.enabled);
            var written = new SaveData { currentLevelId = "L01B" };
            ServiceRegistry.Get<ICheckpointService>().WriteTo(written);
            Assert.AreEqual("L01B", written.checkpointRoomId);
            Assert.AreEqual(1, written.roomBoundaries.Count);
            session.SaveProgressNow();
            session.ReturnToMainMenu();
            yield return new WaitForSeconds(.5f);
            session.ContinueGame();
            yield return WaitForPlaying();
            Assert.AreEqual("L01B", session.CurrentLevel.levelId);
            Assert.IsTrue(Object.FindObjectOfType<RopeDryingRack>().SupportValid);
            Assert.AreEqual(52.5f, ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary, .01f);
        }

        [UnityTest]
        public IEnumerator ExplicitLegacyTreeRoomMigrates() { yield return RestoreLegacy(true, "cp_L01B_entry"); }

        [UnityTest]
        public IEnumerator OldCheckpointOnlyTreeSaveMigrates() { yield return RestoreLegacy(false, "cp_L01B_entry"); }

        [UnityTest]
        public IEnumerator OldBoundaryOnlyTreeSaveMigrates() { yield return RestoreLegacy(false, null); }

        [UnityTest]
        public IEnumerator LegacyCanalCheckpointStaysInCanal()
        {
            var data = new SaveData { currentLevelId = "L01", checkpointId = "cp_L01A_bank" };
            data.roomBoundaries.Add(new RoomBoundarySnapshot("L01A", 18f));
            data.roomBoundaries.Add(new RoomBoundarySnapshot("L01B", 52.5f));
            Assert.IsTrue(ServiceRegistry.Get<ISaveService>().SaveProgress(data));
            session.ContinueGame();
            yield return WaitForPlaying();
            Assert.AreEqual("L01_Courtyard", SceneManager.GetActiveScene().name);
            Assert.AreEqual("L01A", session.ActiveRoom.RoomId);
            Assert.AreEqual(18f, ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary, .01f);
        }
    }
}
