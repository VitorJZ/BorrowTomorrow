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
        private enum ActionPhase { None, WaitBefore, Playing, WaitAfter }
        private ActionPhase actionPhase;
        private NarrativeAction action;
        private DialogueEntry actionTarget;
        private float actionTimer;
        private float stageTime;
        private int stageFrame;
        private int pointerReleasedFrame;
        public bool IsPlaying { get; private set; }
        public bool IsWaitingForAction => actionPhase == ActionPhase.WaitBefore || actionPhase == ActionPhase.WaitAfter;
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
            EnterLine(entry);
        }
        // An action stage belongs to the same event: it gates the line without adding entries or flags.
        private void EnterLine(DialogueEntry entry)
        {
            NarrativeAction gate = readOnly || !ServiceRegistry.TryGet<DialogueLibrary>(out var lib) ? null : lib.FindAction(entry.entryId);
            if (gate == null) { BeginLine(entry); return; }
            action = gate;
            actionTarget = entry;
            actionTimer = 0f;
            voiceSource.Stop();
            tapeSource.Stop();
            if (ServiceRegistry.TryGet<IHudService>(out var hud)) hud.HideSubtitle();
            if (string.IsNullOrEmpty(gate.confirmBefore)) StartActionPlay();
            else EnterStage(ActionPhase.WaitBefore);
        }
        private void EnterStage(ActionPhase phase)
        {
            actionPhase = phase;
            stageTime = 0f;
            stageFrame = Time.frameCount;
            pointerReleasedFrame = -1;
            RenderAction();
        }
        private void StartActionPlay()
        {
            EnterStage(ActionPhase.Playing);
            if (!string.IsNullOrEmpty(action.sfxId) && ServiceRegistry.TryGet<IAudioService>(out var audio)) audio.PlaySfx(action.sfxId);
            if (!string.IsNullOrEmpty(action.gesture) && ServiceRegistry.TryGet<ICharacterArt>(out var character)) character.PlayGesture(action.gesture);
        }
        private void RenderAction()
        {
            string prompt = actionPhase == ActionPhase.WaitBefore ? action.confirmBefore
                : actionPhase == ActionPhase.WaitAfter ? action.confirmAfter : null;
            float progress = actionPhase == ActionPhase.WaitBefore ? 0f
                : actionPhase == ActionPhase.WaitAfter ? 1f : Mathf.Clamp01(actionTimer / Mathf.Max(0.01f, action.seconds));
            if (ServiceRegistry.TryGet<INarrativePresentation>(out var view) && view is INarrativeActionView actionView)
                actionView.PresentAction(action, progress, prompt, ownsControl);
        }
        private void FinishAction()
        {
            DialogueEntry target = actionTarget;
            actionPhase = ActionPhase.None;
            action = null;
            actionTarget = null;
            BeginLine(target);
        }
        // Only a fresh press after the stage began counts; keyboard presses are edge-triggered,
        // and the pointer must be seen released on an earlier frame so one click cannot pass two stages.
        private void ConfirmAction()
        {
            if (!IsWaitingForAction || stageFrame == Time.frameCount || stageTime < 0.18f
                || pointerReleasedFrame < 0 || pointerReleasedFrame == Time.frameCount
                || GameSession.Instance.State.IsPaused) return;
            if (actionPhase == ActionPhase.WaitBefore) StartActionPlay();
            else FinishAction();
        }
        private void UpdateAction()
        {
            stageTime += Time.deltaTime;
            if (pointerReleasedFrame < 0 && !Input.GetMouseButton(0)) pointerReleasedFrame = Time.frameCount;
            if (actionPhase == ActionPhase.Playing)
            {
                actionTimer += Time.deltaTime;
                RenderAction();
                if (actionTimer < action.seconds) return;
                if (string.IsNullOrEmpty(action.confirmAfter)) FinishAction();
                else EnterStage(ActionPhase.WaitAfter);
                return;
            }
            if (ServiceRegistry.TryGet<IInputSource>(out var input) && input.ConsumeInteract()) ConfirmAction();
            else if (ownsControl && Input.GetMouseButtonDown(0)) ConfirmAction();
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
            if (ServiceRegistry.TryGet<ICharacterArt>(out var character)
                && (current.entryId == "DLG_L03_REVEAL_01" || current.entryId == "DLG_L04_LOOK_01"))
                character.PlayGesture("Interact");
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
            if (actionPhase != ActionPhase.None) { UpdateAction(); return; }
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
            if (IsPlaying && actionPhase != ActionPhase.None) { ConfirmAction(); return; }
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
                EnterLine(next);
                return;
            }
            if (!readOnly && current.endHoldSeconds > 0f) endingTimer = current.endHoldSeconds;
            else End(true);
        }
        // Whole-event skip commits the event's completion result (first line's completionFlags),
        // including any pending action stage; future lines are not walked (spec 1.4 §4.1).
        public void SkipCurrent()
        {
            if (!IsPlaying || GameSession.Instance.State.IsPaused) return;
            End(true);
        }
        /// <summary>环境对象消失时停止对应回声，保留未完成状态供返回后触发。</summary>
        public void CancelAmbient(DialogueEntry entry)
        {
            if (!IsPlaying || readOnly || currentPriority != DialoguePriority.Ambient || first != entry) return;
            End(false);
        }
        private void End(bool completed)
        {
            voiceSource.Stop();
            tapeSource.Stop();
            voicePaused = false;
            actionPhase = ActionPhase.None;
            action = null;
            actionTarget = null;
            var session = GameSession.Instance;
            if (completed && !readOnly && session != null)
            {
                if (!string.IsNullOrEmpty(first.objectiveAfter)) session.SetObjective(first.objectiveAfter == "HIDE" ? null : first.objectiveAfter);
                if (first.completionFlags != null) foreach (string flag in first.completionFlags) pendingFlags.Add(flag);
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
