using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;

namespace BorrowTomorrow.Audio
{
    /// <summary>
    /// Persistent boundary sound bus. Measures accepted movement in LateUpdate so
    /// mouse and keyboard share the same feedback; rejected targets remain silent.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioService))]
    public sealed class BoundaryAudioEmitter : MonoBehaviour
    {
        [Header("Synchronized eight-second loop layers")]
        [SerializeField] private AudioClip pastSlow;
        [SerializeField] private AudioClip pastFast;
        [SerializeField] private AudioClip presentSlow;
        [SerializeField] private AudioClip presentFast;
        [Header("One-shot feedback")]
        [SerializeField] private AudioClip dragStart;
        [SerializeField] private AudioClip dragStop;
        [SerializeField] private AudioClip switchOk;
        [SerializeField] private AudioClip switchBlocked;
        [SerializeField, Range(0f, 1f)] private float dragVolume = .85f;
        [SerializeField, Range(0f, 1f)] private float feedbackVolume = .85f;
        [SerializeField] private float fullSpeed = 8f;

        private readonly AudioSource[] loops = new AudioSource[4];
        // A phase-aligned second bus gives Q/R exactly twice the amplitude
        // without AudioSource.volume clamping gains above one. Mouse is unchanged.
        private readonly AudioSource[] keyboardBoost = new AudioSource[4];
        private readonly float[] targets = new float[4];
        private AudioSource feedback;
        private AudioService audioService;
        private BoundaryController boundary;
        private float lastBoundary;
        private float lastMotionAt = -10f;
        private float lastSuccessAt = -10f;
        private float lastBlockedAt = -10f;
        private bool moving;
        private bool blockedLatched;
        private bool audioPaused;
        private bool focused = true;

        private void Awake()
        {
            audioService = GetComponent<AudioService>();
            AudioClip[] clips = { pastSlow, pastFast, presentSlow, presentFast };
            for (int i = 0; i < loops.Length; i++)
            {
                loops[i] = NewSource();
                loops[i].clip = clips[i];
                loops[i].loop = true;
                loops[i].volume = 0f;
                keyboardBoost[i] = NewSource();
                keyboardBoost[i].clip = clips[i];
                keyboardBoost[i].loop = true;
                keyboardBoost[i].volume = 0f;
            }
            feedback = NewSource();
        }

        private AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 160;
            return source;
        }

        private void OnEnable()
        {
            // Start at the same DSP instant; gains can crossfade without restarting clips.
            double startAt = AudioSettings.dspTime + .05;
            foreach (var source in loops)
                if (source != null && source.clip != null) source.PlayScheduled(startAt);
            foreach (var source in keyboardBoost)
                if (source != null && source.clip != null) source.PlayScheduled(startAt);
        }

        private void LateUpdate()
        {
            BoundaryController active = null;
            if (ServiceRegistry.TryGet<IBoundaryContext>(out var context))
                active = context as BoundaryController;
            if (!ReferenceEquals(active, boundary)) Bind(active);

            var session = GameSession.Instance;
            bool playing = session != null && session.State.Phase == GamePhase.Playing;
            bool suspended = !focused || (session != null && session.State.IsPaused);
            SetPaused(suspended);
            bool available = playing && !suspended && boundary != null
                && boundary.isActiveAndEnabled
                && (session.State.SubState == PlayingSubState.Free
                    || session.State.SubState == PlayingSubState.AdjustingBoundary);
            if (!available)
            {
                Silence();
                if (boundary != null) lastBoundary = boundary.CommittedBoundary;
                return;
            }

            float delta = boundary.CommittedBoundary - lastBoundary;
            lastBoundary = boundary.CommittedBoundary;
            float dt = Mathf.Max(Time.unscaledDeltaTime, .001f);
            if (boundary.IsAdjusting && Mathf.Abs(delta) > .0001f)
            {
                if (!moving) PlayFeedback(dragStart);
                moving = true;
                lastMotionAt = Time.unscaledTime;
                // A larger B expands the 1994 area; a smaller B expands 2024.
                int offset = delta > 0 ? 0 : 2;
                float speed = Mathf.Abs(delta) / dt;
                float fast = Mathf.InverseLerp(1.2f, fullSpeed, speed);
                float level = dragVolume * (.35f + .65f * Mathf.Sqrt(Mathf.Clamp01(speed / fullSpeed)));
                for (int i = 0; i < targets.Length; i++) targets[i] = 0f;
                targets[offset] = level * Mathf.Sqrt(1f - fast);
                targets[offset + 1] = level * Mathf.Sqrt(fast);
                if (Time.unscaledTime - lastBlockedAt > .12f) blockedLatched = false;
            }
            else if (moving && (!boundary.IsAdjusting || Time.unscaledTime - lastMotionAt > .10f))
            {
                moving = false;
                for (int i = 0; i < targets.Length; i++) targets[i] = 0f;
                PlayFeedback(dragStop);
            }

            // Release resets the rejection latch even when adjustment never began
            // (for example, holding Q while airborne).
            if (!boundary.IsAdjusting && !Input.GetMouseButton(0)
                && (!ServiceRegistry.TryGet<IInputSource>(out var input)
                    || Mathf.Abs(input.BoundaryNudgeDirection) < .01f))
                blockedLatched = false;

            float duck = audioService != null ? audioService.EffectsDuckingScale : 1f;
            bool keyboard = ServiceRegistry.TryGet<IInputSource>(out var sourceInput)
                && Mathf.Abs(sourceInput.BoundaryNudgeDirection) > .01f;
            for (int i = 0; i < loops.Length; i++)
            {
                loops[i].volume = Mathf.MoveTowards(loops[i].volume, targets[i] * duck, dt * 7f);
                keyboardBoost[i].volume = keyboard ? loops[i].volume
                    : Mathf.MoveTowards(keyboardBoost[i].volume, 0f, dt * 14f);
            }
            feedback.volume = feedbackVolume * duck;
        }

        private void Bind(BoundaryController next)
        {
            if (boundary != null)
            {
                boundary.OnAdjustBlocked -= OnBlocked;
                boundary.OnEraSwitchCommitted -= OnSwitched;
            }
            Silence();
            boundary = next;
            blockedLatched = false;
            lastSuccessAt = -10f;
            if (boundary != null)
            {
                lastBoundary = boundary.CommittedBoundary;
                boundary.OnAdjustBlocked += OnBlocked;
                boundary.OnEraSwitchCommitted += OnSwitched;
            }
        }

        private bool CanFeedback()
        {
            var session = GameSession.Instance;
            return focused && session != null && !session.State.IsPaused
                && session.State.Phase == GamePhase.Playing
                && (session.State.SubState == PlayingSubState.Free
                    || session.State.SubState == PlayingSubState.AdjustingBoundary);
        }

        private void OnSwitched()
        {
            // A single mouse step can cross many objects. Avoid stacked chimes.
            if (!CanFeedback() || Time.unscaledTime - lastSuccessAt < .16f) return;
            lastSuccessAt = Time.unscaledTime;
            PlayFeedback(switchOk);
        }

        private void OnBlocked(string reason)
        {
            if (!CanFeedback() || blockedLatched || Time.unscaledTime - lastBlockedAt < .35f) return;
            blockedLatched = true;
            lastBlockedAt = Time.unscaledTime;
            PlayFeedback(switchBlocked);
        }

        private void PlayFeedback(AudioClip clip)
        {
            if (clip == null || feedback == null || !CanFeedback()) return;
            feedback.volume = feedbackVolume * (audioService != null ? audioService.EffectsDuckingScale : 1f);
            feedback.PlayOneShot(clip);
        }

        private void Silence()
        {
            moving = false;
            for (int i = 0; i < loops.Length; i++)
            {
                targets[i] = 0f;
                if (loops[i] != null) loops[i].volume = 0f;
                if (keyboardBoost[i] != null) keyboardBoost[i].volume = 0f;
            }
            if (feedback != null) feedback.Stop();
        }

        private void SetPaused(bool pause)
        {
            if (audioPaused == pause) return;
            audioPaused = pause;
            foreach (var source in loops)
            {
                if (source == null) continue;
                if (pause) source.Pause(); else source.UnPause();
            }
            foreach (var source in keyboardBoost)
                if (source != null) { if (pause) source.Pause(); else source.UnPause(); }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) { Silence(); SetPaused(true); }
        }

        private void OnDisable()
        {
            Bind(null);
            foreach (var source in loops) if (source != null) source.Stop();
            foreach (var source in keyboardBoost) if (source != null) source.Stop();
            audioPaused = false;
        }
    }
}
