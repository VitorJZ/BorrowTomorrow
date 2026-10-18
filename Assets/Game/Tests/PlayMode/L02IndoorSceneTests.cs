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
    public sealed class L02IndoorSceneTests
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
            saveDirectory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowL02Tests_" + System.Guid.NewGuid());
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

        private void DragTo(float position)
        {
            var boundary = (BoundaryController)ServiceRegistry.Get<IBoundaryContext>();
            boundary.BeginMouseDrag();
            Assert.IsTrue(boundary.IsAdjusting, "A grounded player can drag the time boundary.");
            boundary.RequestAdjustment(position);
            boundary.EndMouseDrag();
            Assert.AreEqual(position, boundary.CommittedBoundary, .01f);
        }

        [UnityTest]
        public IEnumerator ExteriorDoorLoadsIndependentInterior_PastOnlyObjectsRevealAtTheirAnchors()
        {
            session.LoadLevel(session.FindLevel("L02"));
            yield return WaitForPlaying();
            Assert.AreEqual(1, Object.FindObjectsOfType<LevelRoot>().Length);
            var exteriorDoor = Object.FindObjectOfType<ExitDoor>();
            Assert.IsTrue(exteriorDoor.AreConditionsMet, "Exterior does not require reading the indoor uniform.");
            Assert.IsTrue(exteriorDoor.Interact());
            yield return WaitForPlaying();
            Assert.AreEqual("L02_SewingRoom", SceneManager.GetActiveScene().name);
            Assert.AreEqual("L02Indoor", session.CurrentLevel.levelId);
            Assert.AreEqual(1, Object.FindObjectsOfType<LevelRoot>().Length);
            var uniform = Object.FindObjectsOfType<NarrativeInteractable>(true).Single(t => t.InteractId == "uniform");
            var ledger = Object.FindObjectsOfType<NarrativeInteractable>(true).Single(t => t.InteractId == "ledger");
            var tidy = Object.FindObjectOfType<ClothTidyInteractable>(true);
            Assert.IsFalse(uniform.gameObject.activeInHierarchy);
            Assert.IsFalse(uniform.CanInteract);
            Assert.IsFalse(ledger.CanInteract);
            Assert.IsFalse(tidy.CanInteract);
            Assert.IsFalse(Object.FindObjectOfType<ExitDoor>().AreConditionsMet);

            DragTo(10f);
            yield return null;
            Assert.IsTrue(uniform.CanInteract);
            Assert.IsFalse(ledger.CanInteract, "Items switch individually when the line passes their anchor.");
            Assert.IsFalse(tidy.CanInteract);
            DragTo(18f);
            yield return null;
            Assert.IsTrue(ledger.CanInteract);
            Assert.IsTrue(tidy.CanInteract);
            Assert.IsTrue(tidy.Interact());
            yield return new WaitForSeconds(1.3f);
            Assert.IsTrue(session.StoryFlags.HasFlag("ClothTidyDone"));
            Assert.AreEqual(tidy.chairPosition, tidy.cloth.position);
            DragTo(0f);
            yield return null;
            Assert.IsFalse(tidy.cloth.gameObject.activeInHierarchy);
            Assert.IsFalse(uniform.CanInteract);
            DragTo(18f);
            yield return null;
            Assert.IsTrue(tidy.cloth.gameObject.activeInHierarchy);
            Assert.AreEqual(tidy.completedSprite, tidy.cloth.GetComponent<SpriteRenderer>().sprite);
            Assert.IsFalse(tidy.CanInteract, "Returning to the past must not reset tidied clothing.");

            ServiceRegistry.Get<ICheckpointService>().Capture("cp_L02_indoor");
            session.SaveProgressNow();
            session.ContinueGame();
            yield return WaitForPlaying();
            Assert.AreEqual("L02Indoor", session.ActiveRoom.RoomId);
            Assert.AreEqual(18f, ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary, .01f);
            tidy = Object.FindObjectOfType<ClothTidyInteractable>(true);
            Assert.AreEqual(tidy.chairPosition, tidy.cloth.position);
            Assert.IsFalse(tidy.CanInteract);
            session.StoryFlags.SetFlag("UniformRead");
            Assert.IsTrue(Object.FindObjectOfType<ExitDoor>().Interact());
            yield return WaitForPlaying();
            Assert.AreEqual("L03", session.CurrentLevel.levelId);
        }

        [UnityTest]
        public IEnumerator LegacyInteriorSaveMigratesToNewSceneAndCoordinates()
        {
            var legacy = new SaveData
            {
                currentLevelId = "L02", checkpointRoomId = "L02Indoor", checkpointId = "cp_L02_indoor",
                hasCheckpointPosition = true, checkpointPosition = new Vector2(43f, .05f),
                roomBoundaries = new System.Collections.Generic.List<RoomBoundarySnapshot>
                { new RoomBoundarySnapshot("L02", 2f), new RoomBoundarySnapshot("L02Indoor", 52f) }
            };
            typeof(GameSession).GetMethod("RestoreFromSave", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { legacy, false });
            yield return WaitForPlaying();
            Assert.AreEqual("L02_SewingRoom", SceneManager.GetActiveScene().name);
            Assert.AreEqual("L02Indoor", session.CurrentLevel.levelId);
            Assert.AreEqual(12f, ServiceRegistry.Get<IBoundaryContext>().CommittedBoundary, .01f);
            Assert.AreEqual(3f, ServiceRegistry.Get<IPlayerContext>().Position.x, .1f);
            Assert.IsFalse(session.StoryFlags.HasFlag("UniformRead"));
        }
    }
}
