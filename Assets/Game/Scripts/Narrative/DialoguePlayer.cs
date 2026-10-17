using System;
using System.Collections.Generic;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    [DefaultExecutionOrder(-60)]
    public sealed class DialoguePlayer : MonoBehaviour, IDialogueService, IBootInitializable
    {
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
        [SerializeField] private AudioClip tapeHiss;
        private AudioSource voiceSource;
        private AudioSource tapeSource;
        private DialogueEntry current;
        private DialogueEntry first;
        private DialoguePriority currentPriority;
        private Action onCompleted;
        private readonly HashSet<string> pendingFlags = new HashSet<string>();
        private readonly HashSet<string> visited = new HashSet<string>();
        private readonly List<DialogueEntry> history = new List<DialogueEntry>();
        private string[] pages;
        private int page;
        private float timer;
        private float endingTimer = -1f;
        private bool presented;
        private bool ownsControl;
        private bool readOnly;
        private bool voicePaused;
        private int lastAdvanceFrame = -1;
        public bool IsPlaying { get; private set; }
        public IReadOnlyList<DialogueEntry> History => history;
        public int InitializeOrder => -20;
        public DialogueEntry Current => current;
        public event Action<DialogueEntry> LineStarted;

        private void Awake()
        {
            // A dedicated source avoids sharing AudioService's music/SFX sources on SystemRoot.
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;
            tapeSource = gameObject.AddComponent<AudioSource>();
            tapeSource.playOnAwake = false;
            tapeSource.loop = true;
            tapeSource.spatialBlend = 0f;
        }
        public void OnBootInitialize() => ServiceRegistry.Register<IDialogueService>(this);
        private void OnDestroy() => ServiceRegistry.Unregister<IDialogueService>(this);

        public static bool RequirementsMet(string[] required)
        {
            var flags = GameSession.Instance?.StoryFlags;
            if (flags == null) return false;
            if (required == null) return true;
            foreach (string f in required)
            {
                if (string.IsNullOrEmpty(f)) continue;
                if (f[0] == '!') { if (flags.HasFlag(f.Substring(1))) return false; }
                else if (!flags.HasFlag(f)) return false;
            }
            return true;
        }
        public static bool Completed(DialogueEntry entry)
        {
            if (entry == null) return false;
            if (entry.completionFlags == null || entry.completionFlags.Length == 0)
                return GameSession.Instance != null && ServiceRegistry.TryGet<DialogueLibrary>(out var library)
                    && library.IsChainComplete(entry, GameSession.Instance.StoryFlags);
            foreach (string flag in entry.completionFlags)
                if (GameSession.Instance == null || !GameSession.Instance.StoryFlags.HasFlag(flag)) return false;
            return true;
        }
        public void Play(DialogueEntry entry, DialoguePriority priority, Action callback = null)
        {
            if (entry == null || !RequirementsMet(entry.requiredFlags)) return;
            if (IsPlaying)
            {
                if (priority <= currentPriority) return;
                End(false);
            }
            currentPriority = priority;
            Begin(entry, priority >= DialoguePriority.Story, false, callback);
        }
        public void PlayReadOnly(DialogueEntry entry)
        {
            if (entry != null && !IsPlaying) Begin(entry, true, true, null);
        }
        private void Begin(DialogueEntry entry, bool blocking, bool replay, Action callback)
        {
            first = entry;
            readOnly = replay;
            ownsControl = blocking;
            onCompleted = callback;
            pendingFlags.Clear();
            visited.Clear();
            endingTimer = -1f;
            IsPlaying = true;
            if (blocking) GameSession.Instance?.State.SetSubState(PlayingSubState.Cutscene);
            if (ServiceRegistry.TryGet<IAudioService>(out var audio)) audio.SetDialogueDucking(true);
            if (ServiceRegistry.TryGet<ISaveService>(out var save)) voiceSource.volume = voiceVolume * save.LoadSettings().volumeVoice;
            else voiceSource.volume = voiceVolume;
            BeginLine(entry);
        }
        private void BeginLine(DialogueEntry entry)
        {
            current = entry;
            visited.Add(entry.entryId);
            page = 0;
            pages = SplitPages(entry.subtitle, entry.centeredTitle ? 100 : 52);
            timer = -entry.delayBefore;
            presented = false;
            voiceSource.Stop();
            tapeSource.Stop();
            if (ServiceRegistry.TryGet<INarrativePresentation>(out var view)) view.Close();
            if (ServiceRegistry.TryGet<IHudService>(out var hud)) hud.HideSubtitle();
            if (!readOnly && !history.Contains(entry)) history.Add(entry);
            if (timer >= 0f) PresentLine();
        }
        private void PresentLine()
        {
            presented = true;
            GameSession.Instance?.RecordDialogue(current.entryId);
            if (current.source == "Tape" && tapeHiss != null)
            {
                tapeSource.clip = tapeHiss;
                tapeSource.volume = voiceSource.volume * 0.045f;
                tapeSource.Play();
            }
            if (current.voiceClip != null)
            {
                voiceSource.clip = current.voiceClip;
                voiceSource.Play();
            }
            if (!string.IsNullOrEmpty(current.sfxId) && ServiceRegistry.TryGet<IAudioService>(out var audio))
                audio.PlaySfx(current.sfxId);
            ShowPage();
            LineStarted?.Invoke(current);
        }
        private void ShowPage()
        {
            string text = pages[page];
            if (current.source == "Tape") text = "「" + text + "」";
            if (ServiceRegistry.TryGet<INarrativePresentation>(out var view))
                view.Present(current, text, ownsControl, page + 1, pages.Length);
            else if (ServiceRegistry.TryGet<IHudService>(out var hud)) hud.ShowSubtitle(current.speakerLabel, text);
        }
        private void Update()
        {
            if (!IsPlaying) return;
            var session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing)
            {
                End(false);
                return;
            }
            if (session.State.IsPaused)
            {
                if (!voicePaused) { voiceSource.Pause(); tapeSource.Pause(); voicePaused = true; }
                return;
            }
            if (voicePaused)
            {
                if (ServiceRegistry.TryGet<ISaveService>(out var save)) voiceSource.volume = voiceVolume * save.LoadSettings().volumeVoice;
                tapeSource.volume = voiceSource.volume * 0.045f;
                voiceSource.UnPause(); tapeSource.UnPause(); voicePaused = false;
            }
            timer += Time.deltaTime;
            if (endingTimer >= 0f)
            {
                endingTimer -= Time.deltaTime;
                if (endingTimer <= 0f) End(true);
                return;
            }
            if (!presented) { if (timer >= 0f) PresentLine(); return; }
            if (!current.holdForConfirm)
            {
                if (timer >= current.minDurationSeconds && !voiceSource.isPlaying) Advance();
                return;
            }
            if (ServiceRegistry.TryGet<IInputSource>(out var input) && input.ConsumeInteract()) Advance();
            else if (ownsControl && Input.GetMouseButtonDown(0)) Advance();
        }
        public void Advance()
        {
            if (!IsPlaying || !presented || endingTimer >= 0f || timer < 0.18f
                || GameSession.Instance.State.IsPaused || lastAdvanceFrame == Time.frameCount) return;
            lastAdvanceFrame = Time.frameCount;
            if (page + 1 < pages.Length)
            {
                page++;
                timer = 0f;
                ShowPage();
                return;
            }
            // Confirmation can interrupt voice immediately; no mandatory wait through the whole recording.
            voiceSource.Stop();
            if (!readOnly && current.setFlags != null)
                foreach (string flag in current.setFlags) pendingFlags.Add(flag);
            if (!readOnly && !string.IsNullOrEmpty(current.nextId))
            {
                DialogueEntry next = FindNext(current.nextId);
                if (next == null || visited.Contains(next.entryId))
                {
                    Debug.LogError("[DialoguePlayer] Invalid chain: " + current.nextId);
                    End(false);
                    return;
                }
                BeginLine(next);
                return;
            }
            if (!readOnly && current.endHoldSeconds > 0f) endingTimer = current.endHoldSeconds;
            else End(true);
        }
        public void SkipCurrent()
        {
            if (!IsPlaying || GameSession.Instance.State.IsPaused) return;
            if (!readOnly)
            {
                var walked = new HashSet<string>();
                DialogueEntry line = current;
                while (line != null)
                {
                    if (!walked.Add(line.entryId)) { End(false); return; }
                    if (line.setFlags != null) foreach (string flag in line.setFlags) pendingFlags.Add(flag);
                    if (string.IsNullOrEmpty(line.nextId)) break;
                    line = FindNext(line.nextId);
                    if (line == null) { End(false); return; }
                }
            }
            End(true);
        }
        private void End(bool completed)
        {
            voiceSource.Stop();
            tapeSource.Stop();
            voicePaused = false;
            var session = GameSession.Instance;
            if (completed && !readOnly && session != null)
            {
                if (!string.IsNullOrEmpty(first.objectiveAfter)) session.SetObjective(first.objectiveAfter == "HIDE" ? null : first.objectiveAfter);
                session.StoryFlags.SetFlags(pendingFlags);
                session.SaveProgressNow();
            }
            IsPlaying = false;
            if (ServiceRegistry.TryGet<INarrativePresentation>(out var view)) view.Close();
            if (ServiceRegistry.TryGet<IHudService>(out var hud)) hud.HideSubtitle();
            if (ServiceRegistry.TryGet<IAudioService>(out var audio)) audio.SetDialogueDucking(false);
            if (ownsControl && session != null && session.State.SubState == PlayingSubState.Cutscene)
                session.State.SetSubState(PlayingSubState.Free);
            current = null;
            ownsControl = false;
            Action callback = onCompleted;
            onCompleted = null;
            if (completed) callback?.Invoke();
        }
        private DialogueEntry FindNext(string id) => ServiceRegistry.TryGet<DialogueLibrary>(out var lib) ? lib.Find(id) : null;
        private static string[] SplitPages(string text, int limit)
        {
            var result = new List<string>();
            text = text ?? string.Empty;
            while (text.Length > limit)
            {
                int cut = limit;
                for (int i = limit - 1; i >= limit / 2; i--)
                    if ("。！？；，、…".IndexOf(text[i]) >= 0) { cut = i + 1; break; }
                result.Add(text.Substring(0, cut));
                text = text.Substring(cut);
            }
            result.Add(text);
            return result.ToArray();
        }
    }
}
