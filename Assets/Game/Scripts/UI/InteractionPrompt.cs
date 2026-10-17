using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 交互提示（策划 5.4）：交互物附近显示 E 小提示，只在范围内显示。
    /// 把交互物世界坐标投影到屏幕坐标，锚点式跟随。
    /// </summary>
    public sealed class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMPro.TMP_Text hintText;
        [SerializeField] private RectTransform followRoot;
        [Tooltip("投影到屏幕边缘内缩（像素）")]
        [SerializeField] private float screenEdgePadding = 60f;
        [Tooltip("提示相对交互物的屏幕偏移")]
        [SerializeField] private Vector2 screenOffset = new Vector2(0f, 40f);

        private Camera mainCamera;

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
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        public void SetPrompt(bool visible, string hint, Vector2 worldPosition)
        {
            if (root == null)
            {
                root = gameObject;
            }
            if (!visible)
            {
                root.SetActive(false);
                return;
            }
            root.SetActive(true);
            if (hintText != null)
            {
                hintText.text = string.IsNullOrEmpty(hint) ? "E" : $"E：{hint}";
            }
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            if (followRoot != null && mainCamera != null)
            {
                Vector2 screen = mainCamera.WorldToScreenPoint(worldPosition);
                screen += screenOffset;
                // 限制在屏幕内（16:9 基准，其他宽高比保留完整范围，策划 8.6）。
                var canvas = GetComponentInParent<Canvas>().rootCanvas;
                RectTransform canvasRect = canvas.transform as RectTransform;
                if (canvasRect != null)
                {
                    float scale = canvasRect.rect.width / Screen.width;
                    screen.x = Mathf.Clamp(screen.x, screenEdgePadding, Screen.width - screenEdgePadding);
                    screen.y = Mathf.Clamp(screen.y, screenEdgePadding, Screen.height - screenEdgePadding);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                        out Vector2 local);
                    followRoot.localPosition = local;
                }
            }
        }
    }
}
