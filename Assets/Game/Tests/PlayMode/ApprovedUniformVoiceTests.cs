using System.Collections;
using System.IO;
using System.Reflection;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BorrowTomorrow.Tests
{
    public sealed class ApprovedUniformVoiceTests
    {
        private GameSession session;
        private string saves;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [UnitySetUp] public IEnumerator Boot()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2);
            session = GameSession.Instance;
            saves = Path.Combine(Path.GetTempPath(), "BorrowTomorrowVoiceApproval_" + System.Guid.NewGuid());
            Directory.CreateDirectory(saves);
            typeof(SaveService).GetField("saveDirectory", Private).SetValue(Object.FindObjectOfType<SaveService>(), saves);
            session.LoadLevel(session.FindLevel("L03"));
            float limit = Time.realtimeSinceStartup + 20;
            while (session.State.Phase != GamePhase.Playing && Time.realtimeSinceStartup < limit) yield return null;
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(GamePhase.Playing, session.State.Phase);
        }
        [UnityTearDown] public IEnumerator Clean()
        {
            if (session != null) Object.Destroy(session.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1; AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu"); yield return null;
            if (Directory.Exists(saves)) Directory.Delete(saves, true);
        }
        private IEnumerator Check(string id, string expectedClip, string text)
        {
            var entry = ServiceRegistry.Get<DialogueLibrary>().Find(id);
            Assert.IsNotNull(entry); Assert.AreEqual(text, entry.subtitle);
            Assert.IsNotNull(entry.voiceClip); Assert.AreEqual(expectedClip, entry.voiceClip.name);
            var player = (DialoguePlayer)ServiceRegistry.Get<IDialogueService>();
            int flagsBefore = session.StoryFlags.AllFlags.Count;
            player.PlayReadOnly(entry);
            // Keep the authored pause before the quilt recollection; do not mistake it for missing audio.
            yield return new WaitForSeconds(entry.delayBefore + .12f);
            var source = (AudioSource)typeof(DialoguePlayer).GetField("voiceSource", Private).GetValue(player);
            Assert.AreSame(entry.voiceClip, source.clip);
            Assert.IsTrue(source.isPlaying, "Approved voice must actually play, not merely be assigned");
            Assert.Greater(source.volume, 0);
            player.SkipCurrent();
            Assert.AreEqual(flagsBefore, session.StoryFlags.AllFlags.Count, "Audit replay must not change progress");
        }
        [UnityTest] public IEnumerator ApprovedSleeveVoicePlaysThroughDialoguePlayer()
        {
            yield return Check("DLG_L02_UNIFORM_01", "vo_v35_l02_uniform_01", "袖口留了一寸。明年还能穿。");
        }
        [UnityTest] public IEnumerator ApprovedQuiltVoicePlaysThroughDialoguePlayer()
        {
            yield return Check("DLG_L02_UNIFORM_04", "vo_v35_l02_uniform_04", "……红碎花，还有那块蓝格子。那床小被面，用的就是这两块布。还收在蓝铁盒里呢。");
        }
    }
}
