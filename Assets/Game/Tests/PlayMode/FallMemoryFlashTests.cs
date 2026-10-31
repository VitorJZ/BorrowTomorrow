using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Save;
using BorrowTomorrow.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    public sealed class FallMemoryFlashTests
    {
        private GameSession session;
        private FallMemoryFlash flash;
        private string saves;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private int Seen => session.StoryFlags.AllFlags.Count(s => s.StartsWith("FallMemory_v1_Seen_"));
        [UnitySetUp] public IEnumerator Boot()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2);
            session = GameSession.Instance;
            Assert.IsNotNull(session);
            saves = Path.Combine(Path.GetTempPath(), "BorrowTomorrowFallMemory_" + System.Guid.NewGuid());
            Directory.CreateDirectory(saves);
            typeof(SaveService).GetField("saveDirectory", Private).SetValue(Object.FindObjectOfType<SaveService>(), saves);
            session.LoadLevel(session.FindLevel("L03"));
            float deadline = Time.realtimeSinceStartup + 20;
            while (session.State.Phase != GamePhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(.6f);
            Assert.AreEqual(GamePhase.Playing, session.State.Phase);
            flash = Object.FindObjectOfType<FallMemoryFlash>();
            Assert.IsNotNull(flash);
            session.StoryFlags.Clear();
            session.State.SetSubState(PlayingSubState.Free);
            ServiceRegistry.Get<ICheckpointService>().Capture("FallMemoryTest");
        }
        [UnityTearDown] public IEnumerator Clean()
        {
            if (session != null) Object.Destroy(session.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1; AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu"); yield return null;
            if (Directory.Exists(saves)) Directory.Delete(saves, true);
        }
        private IEnumerator Done()
        {
            float limit = Time.realtimeSinceStartup + 6;
            while (session.State.SubState == PlayingSubState.Respawning && Time.realtimeSinceStartup < limit) yield return null;
            Assert.AreEqual(PlayingSubState.Free, session.State.SubState);
            Assert.IsFalse(flash.IsPlaying);
            Assert.IsFalse(flash.OverlayVisible);
        }
        [UnityTest] public IEnumerator OnlyApprovedImagesAndSupportedShaderArePackaged()
        {
            CollectionAssert.AreEqual(new[]{"01","03","04","06","07","08","09","10","11","12"},FallMemoryFlash.ImageIds);
            foreach (var id in FallMemoryFlash.ImageIds)
            {
                var texture = Resources.Load<Texture2D>("FallMemories/CG_Fall_"+id);
                Assert.IsNotNull(texture); Assert.Greater(texture.width, 1500);
            }
            Assert.IsNull(Resources.Load<Texture2D>("FallMemories/CG_Fall_02"));
            Assert.IsNull(Resources.Load<Texture2D>("FallMemories/CG_Fall_05"));
            var shader=Resources.Load<Shader>("FallMemories/FallMemorySoft");
            Assert.IsNotNull(shader); Assert.IsTrue(shader.isSupported);
            yield return null;
        }
        [UnityTest] public IEnumerator DuplicateFallLocksControlsConsumesOneAndReturnsToCheckpoint()
        {
            var player=ServiceRegistry.Get<IPlayerContext>(); var checkpoint=player.Position;
            session.RequestRespawnFromFall(); session.RequestRespawnFromFall();
            Assert.AreEqual(PlayingSubState.Respawning,session.State.SubState);
            Assert.IsFalse(session.State.IsPlayerControlAllowed);
            yield return Done();
            Assert.AreEqual(1, Seen); Assert.AreEqual("01",flash.DisplayedId);
            Assert.Less(Vector2.Distance(checkpoint,player.Position), .35f);
        }
        [UnityTest] public IEnumerator AllTenFallsAdvanceInExactOrderThenUseNormalRespawn()
        {
            for(int i=0;i<10;i++)
            {
                session.RequestRespawnFromFall(); yield return Done();
                Assert.AreEqual(FallMemoryFlash.ImageIds[i],flash.DisplayedId);
                Assert.AreEqual(i+1,Seen);
            }
            Assert.IsFalse(flash.HasNext);
            session.RequestRespawnFromFall(); yield return Done();
            Assert.AreEqual(10,Seen); Assert.AreEqual("12",flash.DisplayedId);
        }
        [UnityTest] public IEnumerator ManualCheckpointRestoreDoesNotConsumeMemory()
        {
            session.ConfirmCheckpointRestore(); yield return Done();
            Assert.AreEqual(0,Seen); Assert.AreEqual(0,flash.NextIndex);
            Assert.IsNull(flash.DisplayedId);
        }
        [UnityTest] public IEnumerator PauseHidesOverlayFreezesTimeAndRejectsExtraFalls()
        {
            session.RequestRespawnFromFall();
            yield return new WaitForSeconds(.38f);
            Assert.IsTrue(flash.IsPlaying); Assert.IsTrue(flash.OverlayVisible);
            session.State.EnterPause(); float pausedAt=flash.Elapsed;
            session.RequestRespawnFromFall();
            Assert.IsFalse(flash.OverlayVisible);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(pausedAt,flash.Elapsed,.001f);
            session.State.ExitPause(); Assert.IsTrue(flash.OverlayVisible);
            yield return Done(); Assert.AreEqual(1,Seen);
        }
        [UnityTest] public IEnumerator ReturningToMenuDuringFlashClearsPresentation()
        {
            session.RequestRespawnFromFall(); yield return new WaitForSeconds(.38f);
            session.ReturnToMainMenu(); yield return null;
            Assert.AreEqual(GamePhase.MainMenu,session.State.Phase);
            Assert.IsFalse(flash.IsPlaying); Assert.IsFalse(flash.OverlayVisible);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(GamePhase.MainMenu,session.State.Phase);
        }
        [UnityTest] public IEnumerator StoryFlagsRetainProgressAcrossSpatialRestores()
        {
            session.RequestRespawnFromFall(); yield return Done();
            session.ConfirmCheckpointRestore(); yield return Done();
            Assert.AreEqual(1,flash.NextIndex);
            var saved=session.StoryFlags.AllFlags.ToArray();
            session.StoryFlags.Clear(); Assert.AreEqual(0,flash.NextIndex);
            session.StoryFlags.RestoreFrom(saved); Assert.AreEqual(1,flash.NextIndex);
            session.RequestRespawnFromFall(); yield return Done();
            Assert.AreEqual("03",flash.DisplayedId); Assert.AreEqual(2,Seen);
        }
        [UnityTest] public IEnumerator CaptureRealFlashForVisualReview()
        {
            session.RequestRespawnFromFall();
            yield return new WaitForSeconds(.40f);
            Assert.IsTrue(flash.OverlayVisible);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("C:/Users/chris/Documents/Codex/2026-10-17/tu-a/outputs/FallMemoryIntegration_v2/in-engine-memory.png");
            yield return Done();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("C:/Users/chris/Documents/Codex/2026-10-17/tu-a/outputs/FallMemoryIntegration_v2/in-engine-restored.png");
        }
    }
}
