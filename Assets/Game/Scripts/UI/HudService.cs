using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// HUD 服务（策划 10.4/5.4）：UI 根协调者。
    /// 转发目标、字幕、交互提示、边界受阻提示与年代角标到各子面板；
    /// 挂在系统根的 Canvas 上，注册为全局 IHudService。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class HudService : MonoBehaviour, IHudService, IBootInitializable
    {
        [Header("子面板")]
        [SerializeField] private SubtitlePanel subtitlePanel;
        [SerializeField] private ObjectivePanel objectivePanel;
        [SerializeField] private InteractionPrompt interactionPrompt;
        [SerializeField] private BoundaryHandle boundaryHandle;
        [SerializeField] private EraBadge eraBadge;
        [SerializeField] private FadeScreen fadeScreen;
        [SerializeField] private UnityEngine.UI.Text blockedToastText;

        private CanvasGroup gameplayGroup;
        private GameSession session;

        public int InitializeOrder => -60;

        private void Awake()
        {
            // Keep panel behaviours running; hide their presentation without disabling timers.
            var root = new GameObject("GameplayHud", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            gameplayGroup = root.GetComponent<CanvasGroup>();
            foreach (Transform child in new System.Collections.Generic.List<Transform>(GetChildren()))
            {
                if (child.GetComponent<SubtitlePanel>() != null || child.GetComponent<ObjectivePanel>() != null
                    || child.GetComponent<InteractionPrompt>() != null || child.GetComponent<BoundaryHandle>() != null
                    || child.GetComponent<EraBadge>() != null
                    || child.name == "EraLabelPast" || child.name == "EraLabelPresent" || child.name == "BlockedToast")
                    child.SetParent(rect, false);
            }
            SetGameplayVisible(false);
        }

        private System.Collections.Generic.IEnumerable<Transform> GetChildren()
        {
            foreach (Transform child in transform) yield return child;
        }

        private void SetGameplayVisible(bool visible)
        {
            gameplayGroup.alpha = visible ? 1f : 0f;
            gameplayGroup.interactable = visible;
            gameplayGroup.blocksRaycasts = visible;
        }

        private void LateUpdate()
        {
            bool playing = session != null && session.State.Phase == GamePhase.Playing;
            SetGameplayVisible(playing && !session.State.IsPaused);
            if (boundaryHandle != null)
                boundaryHandle.SetPresentationVisible(playing && !session.State.IsPaused
                    && (session.State.SubState == PlayingSubState.Free
                        || session.State.SubState == PlayingSubState.AdjustingBoundary));
        }

        private void HandlePhaseChanged(GamePhase previous, GamePhase next)
        {
            if (next == GamePhase.Playing) return;
            SetGameplayVisible(false);
            HideSubtitle();
            HideObjective();
            SetInteractionPrompt(false);
            CancelInvoke(nameof(HideBlockedToast));
            HideBlockedToast();
            if (eraBadge != null) eraBadge.Hide();
            if (boundaryHandle != null) boundaryHandle.ResetBinding();
            if (next == GamePhase.MainMenu && fadeScreen != null) fadeScreen.FadeIn(0f);
        }

        public void OnBootInitialize()
        {
            ServiceRegistry.Register<IHudService>(this);
            session = GameSession.Instance;
            if (session != null) session.State.PhaseChanged += HandlePhaseChanged;
            HideSubtitle();
            HideBlockedToast();
        }

        private void OnDestroy()
        {
            if (session != null) session.State.PhaseChanged -= HandlePhaseChanged;
            ServiceRegistry.Unregister<IHudService>(this);
        }

        public void ShowObjective(string text, float autoHideSeconds = 4f)
        {
            if (objectivePanel != null)
            {
                objectivePanel.Show(text, autoHideSeconds);
            }
        }

        public void ShowObjectivePersistent(string text)
        {
            if (objectivePanel != null)
            {
                objectivePanel.ShowPersistent(text);
            }
        }

        public void HideObjective()
        {
            if (objectivePanel != null)
            {
                objectivePanel.Hide();
            }
        }

        public void ShowSubtitle(string speakerLabel, string text)
        {
            if (subtitlePanel != null)
            {
                if (ServiceRegistry.TryGet<ISaveService>(out var save))
                    subtitlePanel.SetSubtitlesEnabled(save.LoadSettings().subtitlesEnabled);
                subtitlePanel.Show(speakerLabel, text);
            }
        }

        public void SetSubtitlesEnabled(bool enabled)
        {
            if (subtitlePanel != null) subtitlePanel.SetSubtitlesEnabled(enabled);
        }

        public void HideSubtitle()
        {
            if (subtitlePanel != null)
            {
                subtitlePanel.Hide();
            }
        }

        public void SetInteractionPrompt(bool visible, string hint = null, Vector2 worldPosition = default)
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetPrompt(visible, hint, worldPosition);
            }
        }

        public void ShowBoundaryBlocked(string reason)
        {
            // 统一受阻提示（策划 4.5：“先到稳固的地方。”），短暂显示。
            if (blockedToastText != null)
            {
                blockedToastText.text = reason;
                blockedToastText.enabled = true;
                CancelInvoke(nameof(HideBlockedToast));
                Invoke(nameof(HideBlockedToast), 1.6f);
            }
        }

        private void HideBlockedToast()
        {
            if (blockedToastText != null)
            {
                blockedToastText.enabled = false;
            }
        }

        public void FlashEraBadge(Era dominantEra)
        {
            if (eraBadge != null)
            {
                eraBadge.Flash(dominantEra);
            }
        }

        public void FadeOut(float seconds)
        {
            if (fadeScreen != null)
            {
                fadeScreen.FadeOut(seconds);
            }
        }

        public void FadeIn(float seconds)
        {
            if (fadeScreen != null)
            {
                fadeScreen.FadeIn(seconds);
            }
        }

        /// <summary>拖动时间边界时把手视觉同步（供 BoundaryHandle 以外的 UI 使用）。</summary>
        public BoundaryHandle BoundaryHandle => boundaryHandle;
    }
}
