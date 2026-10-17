using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 时间边界把手（策划 5.3）：
    /// 屏幕顶部把手，至少 44 像素舒适命中区；
    /// 鼠标位置先转换到当前房间世界坐标，再交给边界系统约束；
    /// 拖动期间镜头保持不动；禁止点背景任何位置瞬移边界，防止误触。
    /// Q/R 键盘调整时把手视觉同步到已提交边界。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoundaryHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("世界 X 与屏幕 X 的换算：使用的正交相机（空则用主相机）")]
        [SerializeField] private Camera worldCamera;

        private IBoundaryContext boundary;
        private bool subscribed;

        private void Update()
        {
            if (boundary == null && ServiceRegistry.TryGet<IBoundaryContext>(out var found))
            {
                boundary = found;
                if (!subscribed)
                {
                    boundary.OnBoundaryCommitted += SyncHandleToBoundary;
                    subscribed = true;
                }
                SyncHandleToBoundary(boundary.CommittedBoundary);
            }
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void OnDestroy()
        {
            if (boundary != null && subscribed)
            {
                boundary.OnBoundaryCommitted -= SyncHandleToBoundary;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (boundary == null && !ServiceRegistry.TryGet<IBoundaryContext>(out boundary))
            {
                return;
            }
            // 进入“调整时间”状态：指针变化、主角站稳、候选状态出现轮廓（策划 5.3）。
            if (boundary is BoundaryController controller)
            {
                controller.BeginMouseDrag();
            }
            // 冻结镜头跟随（策划 5.3：拖动时镜头保持不动）。
            if (ServiceRegistry.TryGet<ICameraRig>(out var cameraRig))
            {
                cameraRig.SetFollowFrozen(true);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (boundary == null || worldCamera == null)
            {
                return;
            }
            // 鼠标屏幕位置 → 当前房间世界坐标（策划 5.3），约束在房间范围内由控制器完成。
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 0f));
            boundary.RequestAdjustment(world.x);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (boundary is BoundaryController controller)
            {
                controller.EndMouseDrag();
            }
            if (ServiceRegistry.TryGet<ICameraRig>(out var cameraRig))
            {
                cameraRig.SetFollowFrozen(false);
            }
        }

        private void SyncHandleToBoundary(float committed)
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
            if (worldCamera == null)
            {
                return;
            }
            float clamped = Mathf.Clamp(committed, boundary.RangeMin, boundary.RangeMax);
            float t = boundary.RangeMax > boundary.RangeMin
                ? (clamped - boundary.RangeMin) / (boundary.RangeMax - boundary.RangeMin)
                : 0.5f;
            // 把手水平位置映射到屏幕宽度（UI 锚点自定，这里用像素）。
            RectTransform parent = transform.parent as RectTransform;
            if (parent != null)
            {
                float width = parent.rect.width;
                ((RectTransform)transform).anchoredPosition = new Vector2(
                    (t - 0.5f) * width, ((RectTransform)transform).anchoredPosition.y);
            }
        }
    }
}
