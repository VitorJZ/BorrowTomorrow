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
    public sealed class PhotoRevealTransitionTests
    {
        private GameSession session;
        private string saveDirectory;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [UnitySetUp] public IEnumerator Boot()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f);
            session = GameSession.Instance;
            saveDirectory = Path.Combine(Path.GetTempPath(), "BorrowTomorrowPhotoReveal_" + System.Guid.NewGuid());
            Directory.CreateDirectory(saveDirectory);
            typeof(SaveService).GetField("saveDirectory", Private).SetValue(Object.FindObjectOfType<SaveService>(), saveDirectory);
            session.LoadLevel(session.FindLevel("L03"));
            float deadline = Time.realtimeSinceStartup + 20f;
            while (session.State.Phase != GamePhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(GamePhase.Playing, session.State.Phase);
            yield return new WaitForSeconds(.7f);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (session != null) Object.Destroy(session.transform.root.gameObject);
            yield return null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
            yield return null;
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest] public IEnumerator SharedPhoto_PullsBackIntoHospital_ThenWaitsBeforeRealityVoice()
        {
            var dialogue = (DialoguePlayer)ServiceRegistry.Get<IDialogueService>();
            var library = ServiceRegistry.Get<DialogueLibrary>();
            var previous = library.Find("V5_L03_004");
            var incoming = library.Find("V5_L03_005");
            Assert.Greater(incoming.scenePhotoRect.width, .5f);
            Assert.Greater(incoming.scenePreviousPhotoCrop.width, .8f);
            var view = Object.FindObjectOfType<NarrativePresentation>();
            var shots = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "work/photo_reveal_transition");
            Directory.CreateDirectory(shots);
            dialogue.PlayReadOnly(previous, true);
            yield return new WaitForSeconds(.25f);
            dialogue.Advance();
            Assert.IsTrue(dialogue.IsTransitioningScene);
            Assert.AreEqual(previous, dialogue.Current);
            Assert.IsFalse(Field<RectTransform>(view, "dock").gameObject.activeSelf);
            Assert.IsFalse(Field<AudioSource>(dialogue, "voiceSource").isPlaying);
            dialogue.Advance();
            Assert.AreEqual(previous, dialogue.Current, "过渡期间输入不能推进下一句");
            session.State.EnterPause();
            var pausedAt = Field<float>(dialogue, "sceneTransitionTimer");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual(pausedAt, Field<float>(dialogue, "sceneTransitionTimer"), .001f);
            session.State.ExitPause();
            yield return new WaitForSeconds(incoming.sceneLeadHold + incoming.sceneBlendSeconds * .5f);
            Assert.IsTrue(dialogue.IsTransitioningScene);
            var photo = Field<RawImage>(view, "transitionPhoto");
            Assert.IsTrue(photo.gameObject.activeInHierarchy);
            Assert.Less(photo.rectTransform.sizeDelta.x, 960f);
            Assert.Greater(Field<CanvasGroup>(view, "pictureOpacity").alpha, 0f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shots, "hospital_midpoint.png"));
            yield return new WaitForSeconds(incoming.sceneBlendSeconds * .5f + .1f);
            Assert.IsTrue(dialogue.IsTransitioningScene, "病房显现后仍须停顿");
            Assert.AreEqual(1f, Field<CanvasGroup>(view, "pictureOpacity").alpha);
            Assert.IsFalse(photo.gameObject.activeSelf);
            Assert.IsFalse(Field<RectTransform>(view, "dock").gameObject.activeSelf);
            Assert.IsFalse(Field<AudioSource>(dialogue, "voiceSource").isPlaying);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shots, "hospital_hold.png"));
            yield return new WaitForSeconds(incoming.sceneImageHold + .1f);
            Assert.IsFalse(dialogue.IsTransitioningScene);
            Assert.AreEqual(incoming, dialogue.Current);
            Assert.IsTrue(Field<RectTransform>(view, "dock").gameObject.activeSelf);
            Assert.IsTrue(Field<AudioSource>(dialogue, "voiceSource").isPlaying);
            Assert.IsTrue(dialogue.SuppressesBackgroundMusic);
            dialogue.SkipCurrent();
            Assert.IsFalse(dialogue.IsTransitioningScene);
            Assert.IsFalse(dialogue.SuppressesBackgroundMusic);
        }

        [UnityTest] public IEnumerator SolvedPhoto_TransitionsAndHoldsSilently_BeforeFirstLine()
        {
            var puzzle = Object.FindObjectOfType<PhotoReconstructionPuzzle>();
            Assert.IsNotNull(puzzle);
            var dialogue = (DialoguePlayer)ServiceRegistry.Get<IDialogueService>();
            var entry = ServiceRegistry.Get<DialogueLibrary>().Find("V5_L03_001");
            int callbacks = 0;
            Assert.IsTrue(puzzle.Open(() => { callbacks++; dialogue.Play(entry, DialoguePriority.Reveal); }));
            typeof(PhotoReconstructionPuzzle).GetMethod("BeginComplete", Private).Invoke(puzzle, null);
            var audio = ServiceRegistry.Get<IAudioService>();
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(0f, Field<AudioSource>(audio, "musicPastSource").volume);
            Assert.AreEqual(0f, Field<AudioSource>(audio, "musicPresentSource").volume);
            Assert.IsFalse(dialogue.IsPlaying, "照片过渡期间不能先出现文字或配音");
            session.State.EnterPause();
            float pausedAt = Field<float>(puzzle, "finishTimer");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual(pausedAt, Field<float>(puzzle, "finishTimer"), .001f);
            session.State.ExitPause();
            yield return new WaitForSeconds(PhotoReconstructionPuzzle.RevealTransitionSeconds + .1f);
            Assert.IsTrue(puzzle.IsOpen);
            Assert.IsFalse(dialogue.IsPlaying, "过渡结束后仍应留白停顿");
            var image = Field<RawImage>(puzzle, "completedImage");
            Assert.AreEqual(new Rect(0, 0, 1, 1), image.uvRect);
            Assert.Less(Vector2.Distance(image.rectTransform.anchoredPosition, new Vector2(0, 110)), .01f);
            Assert.IsFalse(Field<Text>(puzzle, "message").enabled);
            yield return new WaitForSeconds(PhotoReconstructionPuzzle.RevealHoldSeconds + .1f);
            Assert.AreEqual(1, callbacks);
            Assert.IsFalse(puzzle.IsOpen);
            Assert.AreEqual(entry, dialogue.Current);
            Assert.IsTrue(dialogue.SuppressesBackgroundMusic);
            Assert.IsFalse(ServiceRegistry.TryGet<IBackgroundMusicSuppression>(out var transition));
            Assert.AreEqual(0f, Field<AudioSource>(audio, "musicPastSource").volume);
            dialogue.SkipCurrent();
            yield return new WaitForSeconds(.2f);
            Assert.IsFalse(dialogue.SuppressesBackgroundMusic);
            Assert.Greater(Field<AudioSource>(audio, "musicPastSource").volume + Field<AudioSource>(audio, "musicPresentSource").volume, 0f);
        }
    }
}
