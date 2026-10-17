using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 提示气泡（策划 12.4）：“H 查看提示”引导与三级提示文本展示。
    /// 只有玩家按 H 才展开下一层，不用对白直接自动报答案。
    /// </summary>
    public sealed class HintToast : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMPro.TMP_Text hintText;
        [Tooltip("每条提示显示时长（秒）")]
        [SerializeField] private float displaySeconds = 5f;

        private float timer = -1f;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }
            root.SetActive(false);
        }

        private void Update()
        {
            if (timer > 0f)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f)
                {
                    Hide();
                }
            }
        }

        public void ShowAvailable(string hintId)
        {
            Show("卡住了？按 H 查看提示。");
        }

        public void ShowHint(string hintLine)
        {
            Show(hintLine);
        }

        private void Show(string text)
        {
            root.SetActive(true);
            if (hintText != null)
            {
                hintText.text = text;
            }
            timer = displaySeconds;
        }

        public void Hide()
        {
            timer = -1f;
            root.SetActive(false);
        }
    }
}
