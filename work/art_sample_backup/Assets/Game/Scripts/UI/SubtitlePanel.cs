using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 对白字幕面板（策划 5.4：底部最多两行对白字幕；每句可通过记录回看）。
    /// 灰盒期使用旧版 UI.Text + 系统中文字体（simhei 占位）；正式版由 B 组替换 TMP 与授权字库（策划 8.6）。
    /// 字幕默认开启、字号可调（无障碍，策划 5.5）。
    /// </summary>
    public sealed class SubtitlePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private UnityEngine.UI.Text speakerText;
        [SerializeField] private UnityEngine.UI.Text contentText;

        private int baseFontSize;
        private bool subtitlesEnabled = true;
        private bool hasContent;

        public void SetSubtitlesEnabled(bool enabled)
        {
            subtitlesEnabled = enabled;
            EnsureRoot();
            root.SetActive(subtitlesEnabled && hasContent);
        }

        private void Awake()
        {
            EnsureRoot();
            if (contentText != null) baseFontSize = contentText.fontSize;
            Hide();
        }

        public void Show(string speakerLabel, string text)
        {
            EnsureRoot();
            hasContent = !string.IsNullOrEmpty(text);
            root.SetActive(subtitlesEnabled && hasContent);
            if (speakerText != null)
            {
                speakerText.text = string.IsNullOrEmpty(speakerLabel) ? string.Empty : speakerLabel;
                speakerText.gameObject.SetActive(!string.IsNullOrEmpty(speakerLabel));
            }
            if (contentText != null)
            {
                contentText.text = text;
            }
        }

        public void Hide()
        {
            EnsureRoot();
            hasContent = false;
            root.SetActive(false);
        }

        /// <summary>字幕字号缩放（设置界面无障碍选项）。</summary>
        public void SetScale(float scale)
        {
            if (contentText != null)
            {
                contentText.fontSize = Mathf.Max(10, Mathf.RoundToInt(baseFontSize * scale));
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
