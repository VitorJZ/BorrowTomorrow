using System;
using System.Collections;
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
        [Tooltip("黑屏开场两句录音的磁带底噪音量（相对语音音量）")]
        [SerializeField, Range(0f, 1f)] private float introTapeHissVolume = 0.55f;
        [SerializeField] private float tapeHissFadeSeconds = 0.35f;
        [Tooltip("黑屏开场后淡入关卡的时长")]
        [SerializeField] private float sceneRevealFadeSeconds = 1.2f;
        private AudioSource voiceSource;
        private AudioSource tapeSource;
        private Coroutine tapeFade;
        private float tapeTargetVolume;
        private DialogueEntry current;
        private bool replayWholeChain;
        private DialogueEntry first;
        private DialoguePriority currentPriority;
        private Action onCompleted;
        private readonly HashSet<string> pendingFlags = new HashSet<string>();
        private readonly HashSet<string> visited = new HashSet<string>();
        private readonly List<DialogueEntry> history = new List<DialogueEntry>();
        private string[] pages;
        private int page;
        private float timer;
        private float autoTimer;
        private float endingTimer = -1f;
        private bool presented;
        private bool ownsControl;
        private bool readOnly;
        private bool voicePaused;
        private int lastAdvanceFrame = -1;
        private bool pendingManualAdvance;
        private int dialogueBeginFrame;
        private readonly List<UnityEngine.EventSystems.RaycastResult> pointerHits = new List<UnityEngine.EventSystems.RaycastResult>();
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
        public float SceneRevealFadeSeconds => sceneRevealFadeSeconds;
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
            replayWholeChain = false;
            Begin(entry, priority >= DialoguePriority.Story, false, callback);
        }
        public void PlayReadOnly(DialogueEntry entry, bool wholeChain = false)
        {
            if (entry != null && !IsPlaying)
            {
                replayWholeChain = wholeChain;
                Begin(entry, true, true, null);
            }
        }
        private void Begin(DialogueEntry entry, bool blocking, bool replay, Action callback)
        {
            first = entry;
            pendingManualAdvance=false;dialogueBeginFrame=Time.frameCount;
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
            NarrativeAction gate = !ServiceRegistry.TryGet<DialogueLibrary>(out var lib) ? null : lib.FindAction(entry.entryId);
            if(readOnly && gate!=null && !gate.requireTargetClick && !gate.turnPhoto)gate=null;
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
            ServiceRegistry.TryGet<IInputSource>(out var source);
            var pointer=source as IPointerInputSource;
            bool pointerHeld=pointer!=null?pointer.PrimaryHeld:Input.GetMouseButton(0);
            if (pointerReleasedFrame < 0 && !pointerHeld) pointerReleasedFrame = Time.frameCount;
            if (actionPhase == ActionPhase.Playing)
            {
                actionTimer += Time.deltaTime;
                RenderAction();
                if (actionTimer < action.seconds) return;
                if (string.IsNullOrEmpty(action.confirmAfter)) FinishAction();
                else EnterStage(ActionPhase.WaitAfter);
                return;
            }
            if(actionPhase==ActionPhase.WaitBefore && action.requireTargetClick)
            {
                bool pressed=pointer!=null?pointer.PrimaryPressedThisFrame:Input.GetMouseButtonDown(0);
                Vector2 position=pointer!=null?pointer.PointerPosition:(Vector2)Input.mousePosition;
                if(pressed && ServiceRegistry.TryGet<INarrativePresentation>(out var v) && v is INarrativeTargetView target && target.IsActionTargetHit(action,position))ConfirmAction();
                return;
            }
            if (source!=null && source.ConsumeInteract()) ConfirmAction();
            else if (ownsControl && (pointer!=null?pointer.PrimaryPressedThisFrame:Input.GetMouseButtonDown(0))) ConfirmAction();
        }
        private void BeginLine(DialogueEntry entry)
        {
            current = entry;
            visited.Add(entry.entryId);
            page = 0;
            pages = SplitPages(entry.subtitle, entry.centeredTitle ? 100 : 52);
            timer = -entry.delayBefore;
            autoTimer = 0f;
            presented = false;
            voiceSource.Stop();
            // 连续两条磁带时让底噪自然延续，非磁带台词才淡出。
            if (entry.source != "Tape")
            {
                StopTapeHiss();
            }
            // Retain the previous line's view until the next line is ready, so delays
            // cannot flash the gameplay camera or cancel an in-progress UI click.
            if (!readOnly && !history.Contains(entry)) history.Add(entry);
            if (timer >= 0f) PresentLine();
        }
        private void PresentLine()
        {
            presented = true;
            autoTimer = 0f;
            GameSession.Instance?.RecordDialogue(current.entryId);
            if (ServiceRegistry.TryGet<ICharacterArt>(out var character)
                && (current.entryId == "DLG_L03_REVEAL_01" || current.entryId == "DLG_L04_LOOK_01"))
                character.PlayGesture("Interact");
            if (current.source == "Tape" && tapeHiss != null)
            {
                bool cinematic = current.centeredTitle && current.blackBackground;
                PlayTapeHiss(voiceSource.volume * (cinematic ? introTapeHissVolume : 0.045f));
            }
            if (current.voiceClip != null)
            {
                voiceSource.clip = current.voiceClip;
                voiceSource.Play();
            }
            if (!string.IsNullOrEmpty(current.sfxId) && ServiceRegistry.TryGet<IAudioService>(out var audio))
                audio.PlaySfx(current.sfxId);
            ShowPage();
            if (current.fadeInOnStart && ServiceRegistry.TryGet<IHudService>(out var fadeHud))
                fadeHud.FadeIn(sceneRevealFadeSeconds);
            LineStarted?.Invoke(current);
        }
        private void ShowPage()
        {
            string text = pages[page];
            // 黑屏居中开场保持干净排版，不加引用括号。
            if (current.source == "Tape" && !current.centeredTitle) text = "「" + text + "」";
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
                pendingManualAdvance=false;
                if (!voicePaused) { voiceSource.Pause(); tapeSource.Pause(); voicePaused = true; }
                return;
            }
            if (voicePaused)
            {
                if (ServiceRegistry.TryGet<ISaveService>(out var save)) voiceSource.volume = voiceVolume * save.LoadSettings().volumeVoice;
                if (current != null && current.source == "Tape" && tapeHiss != null)
                {
                    bool cinematic = current.centeredTitle && current.blackBackground;
                    tapeTargetVolume = voiceSource.volume * (cinematic ? introTapeHissVolume : 0.045f);
                }
                tapeSource.volume = tapeTargetVolume;
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
            autoTimer += Time.deltaTime;
            if (current.autoAdvance && autoTimer >= AutoAdvanceTime())
            {
                Advance();
                return;
            }
            if(pendingManualAdvance && timer>=.18f)
            {
                pendingManualAdvance=false;Advance();return;
            }
            if (ServiceRegistry.TryGet<IInputSource>(out var input) && input.ConsumeInteract()) Advance();
            else if (ownsControl && DialoguePointerPressed()) Advance();
        }
        private bool DialoguePointerPressed()
        {
            ServiceRegistry.TryGet<IInputSource>(out var source);
            var pointer=source as IPointerInputSource;
            bool pressed=pointer!=null?pointer.PrimaryPressedThisFrame:Input.GetMouseButtonDown(0);
            if(!pressed)return false;
            // Buttons dispatch on click-up. Do not also advance from their click-down.
            if(UnityEngine.EventSystems.EventSystem.current!=null)
            {
                Vector2 position=pointer!=null?pointer.PointerPosition:(Vector2)Input.mousePosition;
                var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=position};
                pointerHits.Clear();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,pointerHits);
                foreach(var hit in pointerHits)
                    if(hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>()!=null)return false;
            }
            return true;
        }
        public void Advance()
        {
            if (IsPlaying && actionPhase != ActionPhase.None) { if(actionPhase!=ActionPhase.WaitBefore || !action.requireTargetClick)ConfirmAction(); return; }
            if (!IsPlaying || endingTimer >= 0f || GameSession.Instance.State.IsPaused
                || lastAdvanceFrame == Time.frameCount || Time.frameCount==dialogueBeginFrame) return;
            if(!presented || timer<.18f)
            {
                // Preserve one fresh press during the short anti-double-advance interval.
                pendingManualAdvance=true;
                return;
            }
            pendingManualAdvance=false;
            lastAdvanceFrame = Time.frameCount;
            if (page + 1 < pages.Length)
            {
                page++;
                timer = 0f;
                autoTimer = 0f;
                ShowPage();
                return;
            }
            // Confirmation can interrupt voice immediately; no mandatory wait through the whole recording.
            voiceSource.Stop();
            if (!readOnly && current.setFlags != null)
                foreach (string flag in current.setFlags) pendingFlags.Add(flag);
            if ((!readOnly || replayWholeChain) && !string.IsNullOrEmpty(current.nextId))
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
            StopTapeHiss();
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
            IsPlaying = false;pendingManualAdvance=false;
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
        /// <summary>自动推进时间：配音播完后再停留一段自然停顿。</summary>
        private float AutoAdvanceTime()
        {
            if (current == null)
            {
                return 0f;
            }
            float voice = current.voiceClip != null ? current.voiceClip.length : 0f;
            return Mathf.Max(current.minDurationSeconds, voice) + Mathf.Max(0f, current.autoAdvanceDelay);
        }

        private void PlayTapeHiss(float targetVolume)
        {
            if (tapeHiss == null)
            {
                return;
            }
            if (tapeSource.clip != tapeHiss)
            {
                tapeSource.Stop();
                tapeSource.clip = tapeHiss;
                tapeSource.volume = 0f;
            }
            if (!tapeSource.isPlaying)
            {
                tapeSource.volume = 0f;
                tapeSource.Play();
            }
            tapeTargetVolume = Mathf.Clamp01(targetVolume);
            if (tapeFade != null)
            {
                StopCoroutine(tapeFade);
            }
            tapeFade = StartCoroutine(FadeTapeHiss(tapeTargetVolume, Mathf.Max(0.01f, tapeHissFadeSeconds), false));
        }

        private void StopTapeHiss()
        {
            if (tapeSource == null)
            {
                return;
            }
            tapeTargetVolume = 0f;
            if (!tapeSource.isPlaying)
            {
                tapeSource.Stop();
                return;
            }
            if (tapeFade != null)
            {
                StopCoroutine(tapeFade);
            }
            tapeFade = StartCoroutine(FadeTapeHiss(0f, Mathf.Max(0.01f, tapeHissFadeSeconds), true));
        }

        private IEnumerator FadeTapeHiss(float target, float seconds, bool stopWhenDone)
        {
            float start = tapeSource.volume;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                tapeSource.volume = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            tapeSource.volume = target;
            if (stopWhenDone)
            {
                tapeSource.Stop();
            }
            tapeFade = null;
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
