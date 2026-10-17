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

        public void Show(string speakerLabel, string text)
        {
            EnsureRoot();
            root.SetActive(true);
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
                contentText.fontSize = Mathf.Max(10, Mathf.RoundToInt(contentText.fontSize * scale));
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
