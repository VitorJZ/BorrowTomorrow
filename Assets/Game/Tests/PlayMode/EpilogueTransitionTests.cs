using System.Collections;
using System.IO;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Save;
using BorrowTomorrow.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    public sealed class EpilogueTransitionTests
    {
        private GameSession session;
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator Boot()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            session = GameSession.Instance;
            Assert.IsNotNull(session);
            saveDirectory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowEpilogue_" + System.Guid.NewGuid());
            Directory.CreateDirectory(saveDirectory);
            typeof(SaveService).GetField("saveDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Object.FindObjectOfType<SaveService>(), saveDirectory);
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
        public IEnumerator WardStaysCoveredUntilTwoDaysLaterTitle_ThenIllustrationAppears()
        {
            session.StoryFlags.SetFlag(StoryFlags.BoxLocated);
            session.LoadLevel(session.FindLevel("Epilogue"));
            var dialogue = (DialoguePlayer)ServiceRegistry.Get<IDialogueService>();
            var fade = Object.FindObjectOfType<FadeScreen>().GetComponent<Image>();
            float deadline = Time.realtimeSinceStartup + 15f;
            int coveredFrames = 0;
            while (dialogue.Current == null && Time.realtimeSinceStartup < deadline)
            {
                // Sample after presentation/layout work, at the point the frame is rendered.
                yield return new WaitForEndOfFrame();
                if (SceneManager.GetActiveScene().name == "Epilogue" && dialogue.Current == null)
                {
                    Assert.IsTrue(fade.enabled, "医院不能先于时间字幕出现");
                    Assert.GreaterOrEqual(fade.color.a, .999f, "等待自动剧情时保持全黑");
                    coveredFrames++;
                }
            }
            Assert.Greater(coveredFrames, 0, "必须检查到尾声加载后、字幕前的帧");
            Assert.AreEqual("DLG_EPI_01", dialogue.Current?.entryId);
            var presentation = Object.FindObjectOfType<NarrativePresentation>();
            var backdrop = (Image)typeof(NarrativePresentation).GetField("backdrop", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presentation);
            Assert.IsTrue(backdrop.gameObject.activeInHierarchy);
            Assert.AreEqual(1f, backdrop.color.a, "时间字幕接管不透明背景");
            yield return new WaitForSeconds(.7f);
            Assert.IsFalse(fade.enabled, "字幕出现后转场遮罩应释放");
            deadline = Time.realtimeSinceStartup + 8f;
            while (dialogue.Current?.entryId == "DLG_EPI_01" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual("DLG_EPI_02", dialogue.Current?.entryId);
            Assert.IsNotNull(dialogue.Current.illustration, "时间字幕后正常进入尾声插图");
        }

        [UnityTest]
        public IEnumerator EpilogueWithoutPendingStory_RevealsNormally()
        {
            session.LoadLevel(session.FindLevel("Epilogue"));
            float deadline = Time.realtimeSinceStartup + 15f;
            while (session.State.Phase != GamePhase.Playing && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GamePhase.Playing, session.State.Phase);
            yield return new WaitForSeconds(.7f);
            Assert.IsFalse(Object.FindObjectOfType<FadeScreen>().GetComponent<Image>().enabled);
        }
    }
}
