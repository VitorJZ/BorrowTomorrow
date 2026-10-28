using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 主菜单构图舞台：底图与按钮始终按 16:9 设计尺寸等比缩放，
    /// 避免屏幕比例变化时按钮与底图错位。
    /// fitInside = true 时完整显示构图（宽/高屏幕留装饰边），
    /// false 时铺满屏幕（可裁切边缘）。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class MenuCoverStage : MonoBehaviour
    {
        [SerializeField] private Vector2 designSize = new Vector2(1920f, 1080f);
        [SerializeField] private bool fitInside;

        private RectTransform rect;
        private RectTransform parentRect;
        private Vector2 lastParentSize;

        private void Awake()
        {
            rect = (RectTransform)transform;
            parentRect = rect.parent as RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = designSize;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            if (parentRect == null) return;
            Vector2 size = parentRect.rect.size;
            if ((size - lastParentSize).sqrMagnitude > 0.01f)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (parentRect == null) parentRect = rect.parent as RectTransform;
            if (parentRect == null) return;
            Vector2 size = parentRect.rect.size;
            if (size.x <= 0f || size.y <= 0f || designSize.x <= 0f || designSize.y <= 0f) return;
            lastParentSize = size;
            float scale = fitInside
                ? Mathf.Min(size.x / designSize.x, size.y / designSize.y)
                : Mathf.Max(size.x / designSize.x, size.y / designSize.y);
            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
