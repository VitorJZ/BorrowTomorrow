using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 对白字幕面板（策划 5.4：底部最多两行对白字幕；每句可通过记录回看）。
    /// 文本组件可拖入 TMP 或旧版 Text；默认开启、字号可调（无障碍，策划 5.5）。
    /// </summary>
    public sealed class SubtitlePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMPro.TMP_Text speakerText;
        [SerializeField] private TMPro.TMP_Text contentText;

        public void Show(string speakerLabel, string text)
        {
            EnsureRoot();
            root.SetActive(true);
            if (speakerText != null)
            {
                speakerText.text = string.IsNullOrEmpty(speakerLabel) ? string.Empty : $"{speakerLabel}";
                speakerText.gameObject.SetActive(!string.IsNullOrEmpty(speakerLabel));
            }
            if (contentText != null)
            {
                contentText.text = text;
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        /// <summary>字幕字号缩放（设置界面无障碍选项）。</summary>
        public void SetScale(float scale)
        {
            if (contentText != null)
            {
                contentText.fontSize = Mathf.Max(contentText.fontSizeMin, contentText.fontSize * scale);
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
