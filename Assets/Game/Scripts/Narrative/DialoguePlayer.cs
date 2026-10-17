using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 对白播放器（策划 10.4 DialoguePlayer）：
    /// 显示字幕、播放语音、继续或跳过，向调用者报告完成。
    /// 排队规则（策划 12.1）：一句对白播放期间不允许被环境触发器截断；
    /// 高优先级可打断低优先级，低优先级在忙碌时直接跳过。
    /// 文字停留由玩家确认，配音可以跳过，标记仍正常写入（策划 5.5）。
    /// 执行顺序先于 PlayerInteractor：同一帧 E 优先推进对白（ConsumeInteract 消费式）。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DefaultExecutionOrder(-60)]
    public sealed class DialoguePlayer : MonoBehaviour, IDialogueService, IBootInitializable
    {
        [Tooltip("语音默认音量")]
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;

        private AudioSource voiceSource;
        private DialogueEntry current;
        private DialoguePriority currentPriority;
        private System.Action currentOnCompleted;
        private float lineTimer;
        private bool waitingForConfirm;
        private bool voiceFinished;
        private bool chainActive;

        public bool IsPlaying { get; private set; }

        public int InitializeOrder => -20;

        private void Awake()
        {
            voiceSource = GetComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f; // 语音为 2D
            voiceSource.volume = voiceVolume;
        }

        public void OnBootInitialize()
        {
            ServiceRegistry.Register<IDialogueService>(this);
            if (ServiceRegistry.TryGet<IHudService>(out _))
            {
                // HUD 就绪由系统根装配顺序保证。
            }
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<IDialogueService>(this);
        }

        private void Update()
        {
            GameSession session = GameSession.Instance;
            if (session == null)
            {
                return;
            }
            // 暂停时停止对白推进（策划 5.5：恢复后不丢句）。
            if (session.State.IsPaused)
            {
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            lineTimer += Time.deltaTime;

            bool voiceDone = voiceFinished || !voiceSource.isPlaying;
            bool lineShownEnough = voiceDone && lineTimer >= MinLineDuration;
            if (!lineShownEnough)
            {
                return;
            }

            // 自动推进：holdForConfirm=false 的条目到时自动进入下一条（策划 5.5 仅确认模式才等玩家）。
            if (current != null && !current.holdForConfirm)
            {
                Advance();
                return;
            }

            if (ServiceRegistry.TryGet<IInputSource>(out var input) && input.ConsumeInteract())
            {
                if (!voiceFinished && voiceSource.isPlaying)
                {
                    // 第一次按 E：跳过配音，字幕完整显示等待确认（策划 5.5 配音可跳过）。
                    voiceSource.Stop();
                    voiceFinished = true;
                    return;
                }
                Advance();
            }
        }

        private float MinLineDuration => current != null ? current.minDurationSeconds : 1f;

        public void Play(DialogueEntry entry, DialoguePriority priority, System.Action onCompleted = null)
        {
            if (entry == null)
            {
                onCompleted?.Invoke();
                return;
            }
            GameSession session = GameSession.Instance;
            if (session != null)
            {
                StoryFlagStore flags = session.StoryFlags;
                if (entry.requiredFlags != null)
                {
                    foreach (string required in entry.requiredFlags)
                    {
                        if (!flags.HasFlag(required))
                        {
                            onCompleted?.Invoke();
                            return;
                        }
                    }
                }
            }

            if (IsPlaying)
            {
                // 策划 12.1：正在播放的句子不被环境触发器截断；
                // 只有更高优先级能打断，打断时立即写入标记避免进度丢失。
                if (priority <= currentPriority)
                {
                    onCompleted?.Invoke(); // 低优先级直接跳过，不排队堆积
                    return;
                }
                FinishCurrentLine();
                EndChain(); // 被更高优先级打断
            }

            current = entry;
            currentPriority = priority;
            currentOnCompleted = onCompleted;
            chainActive = true;
            IsPlaying = true;
            BeginLine(entry);

            // Story 及以上优先级进入 Cutscene：角色站定专注阅读（策划 10.6）。
            if (session != null && priority >= DialoguePriority.Story
                && session.State.SubState == PlayingSubState.Free)
            {
                session.State.SetSubState(PlayingSubState.Cutscene);
            }
            if (ServiceRegistry.TryGet<IAudioService>(out var audio))
            {
                audio.SetDialogueDucking(true); // 对白期间轻度压低其他声部（策划 09）
            }
        }

        private void BeginLine(DialogueEntry entry)
        {
            waitingForConfirm = entry.holdForConfirm;
            voiceFinished = entry.voiceClip == null;
            lineTimer = 0f;
            if (ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                hud.ShowSubtitle(entry.speakerLabel, entry.subtitle);
            }
            if (entry.voiceClip != null)
            {
                voiceSource.clip = entry.voiceClip;
                voiceSource.Play();
            }
        }

        /// <summary>推进当前台词（玩家确认或自动到时）。</summary>
        public void Advance()
        {
            if (!IsPlaying)
            {
                return;
            }
            FinishCurrentLine();
            if (current != null && !string.IsNullOrEmpty(current.nextId))
            {
                DialogueEntry next = FindNext(current.nextId);
                if (next != null)
                {
                    current = next;
                    BeginLine(next);
                    return;
                }
                Debug.LogWarning($"[DialoguePlayer] 对白链断开：找不到 {current.nextId}。");
            }
            EndChain();
        }

        /// <summary>合法跳过当前对白；跳过仍须写入 setFlags（策划 5.5）。</summary>
        public void SkipCurrent()
        {
            if (!IsPlaying)
            {
                return;
            }
            FinishCurrentLine();
            EndChain();
        }

        private void FinishCurrentLine()
        {
            if (current == null)
            {
                return;
            }
            // 展示完成（或合法跳过）即写入本条标记。
            if (GameSession.Instance != null && current.setFlags != null)
            {
                GameSession.Instance.StoryFlags.SetFlags(current.setFlags);
            }
            if (voiceSource.isPlaying)
            {
                voiceSource.Stop();
            }
        }

        private void EndChain()
        {
            chainActive = false;
            IsPlaying = false;
            waitingForConfirm = false;
            current = null;
            currentPriority = DialoguePriority.Ambient;
            if (ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                hud.HideSubtitle();
            }
            if (ServiceRegistry.TryGet<IAudioService>(out var audio))
            {
                audio.SetDialogueDucking(false);
            }
            GameSession session = GameSession.Instance;
            if (session != null && session.State.SubState == PlayingSubState.Cutscene)
            {
                session.State.SetSubState(PlayingSubState.Free);
            }
            System.Action callback = currentOnCompleted;
            currentOnCompleted = null;
            callback?.Invoke();
        }

        private DialogueEntry FindNext(string nextId)
        {
            // 对白库由 NarrativeDirector 持有；此处通过资源加载兜底。
            if (ServiceRegistry.TryGet(out DialogueLibrary library))
            {
                return library.Find(nextId);
            }
            return null;
        }
    }
}
