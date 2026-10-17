using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 目标面板（策划 5.4）：左上当前区域名与一行目标；
    /// 进入区域后数秒淡出，按住 Tab 重新展开。
    /// 不显示血条、分数、任务感叹号链。
    /// </summary>
    public sealed class ObjectivePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private UnityEngine.UI.Text objectiveText;
        [SerializeField] private float fadeSeconds = 0.6f;

        private string currentObjective;
        private float autoHideTimer = -1f;
        private float alpha;
        private bool persistent;

        private void Awake()
        {
            EnsureRoot();
            alpha = 0f;
            root.SetActive(false);
        }

        private void Update()
        {
            // Tab 按住时重新显示（策划 5.1：查看当前目标）。
            bool holdingRecords = ServiceRegistry.TryGet<IInputSource>(out var input) && input.ObjectiveHeld;
            if (holdingRecords && !string.IsNullOrEmpty(currentObjective))
            {
                alpha = Mathf.MoveTowards(alpha, 1f, fadeSeconds * Time.deltaTime * 2f);
            }
            else if (autoHideTimer > 0f)
            {
                autoHideTimer -= Time.deltaTime;
                alpha = Mathf.MoveTowards(alpha, 1f, fadeSeconds * Time.deltaTime * 2f);
                if (autoHideTimer <= 0f)
                {
                    autoHideTimer = -1f;
                }
            }
            else if (!persistent)
            {
                alpha = Mathf.MoveTowards(alpha, 0f, fadeSeconds * Time.deltaTime);
            }

            bool visible = alpha > 0.01f && !string.IsNullOrEmpty(currentObjective);
            if (root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }

        public void Show(string text, float autoHideSeconds)
        {
            currentObjective = text;
            persistent = false;
            autoHideTimer = autoHideSeconds;
            alpha = 1f;
            ApplyText();
            root.SetActive(!string.IsNullOrEmpty(text));
        }

        public void ShowPersistent(string text)
        {
            currentObjective = text;
            persistent = true;
            autoHideTimer = -1f;
            alpha = 1f;
            ApplyText();
            root.SetActive(!string.IsNullOrEmpty(text));
        }

        public void Hide()
        {
            EnsureRoot();
            root.SetActive(false);
            alpha = 0f;
            currentObjective = null;
            persistent = false;
            autoHideTimer = -1f;
        }

        private void ApplyText()
        {
            EnsureRoot();
            if (objectiveText != null)
            {
                objectiveText.text = currentObjective;
            }
        }

        private void EnsureRoot()
        {
            if (root == null)
            {
                root = gameObject;
            }
        }
    }
}
