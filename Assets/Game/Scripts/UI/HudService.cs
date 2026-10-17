using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// HUD 服务（策划 10.4/5.4）：UI 根协调者。
    /// 转发目标、字幕、交互提示、边界受阻提示、提示气泡与年代角标到各子面板；
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
        [SerializeField] private HintToast hintToast;
        [SerializeField] private EraBadge eraBadge;
        [SerializeField] private FadeScreen fadeScreen;
        [SerializeField] private UnityEngine.UI.Text blockedToastText;

        public int InitializeOrder => -60;

        public void OnBootInitialize()
        {
            ServiceRegistry.Register<IHudService>(this);
        }

        private void OnDestroy()
        {
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
                subtitlePanel.Show(speakerLabel, text);
            }
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

        public void ShowHintAvailable(string hintId)
        {
            if (hintToast != null)
            {
                hintToast.ShowAvailable(hintId);
            }
        }

        public void ShowHintText(string hintLine)
        {
            if (hintToast != null)
            {
                hintToast.ShowHint(hintLine);
            }
        }

        public void HideHint()
        {
            if (hintToast != null)
            {
                hintToast.Hide();
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
