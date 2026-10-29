using System.Collections.Generic;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 时间边界把手（策划 5.3）：
    /// 屏幕顶部把手，至少 44 像素舒适命中区；
    /// 鼠标位置先转换到当前房间世界坐标，再交给边界系统约束；
    /// 拖动期间镜头保持不动；禁止点背景任何位置瞬移边界，防止误触。
    /// 把手中心始终贴着已提交边界线，靠近角落年代角标时让角标淡出，
    /// 不再把把手硬停在“1994 / 2024”告示牌外侧。
    /// Q/R 键盘调整时把手视觉同步到已提交边界。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoundaryHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("世界 X 与屏幕 X 的换算：使用的正交相机（空则用主相机）")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("把手压到年代角标时，角标淡出的过渡距离（UI 像素）；值越大越提前让位")]
        [SerializeField] private float eraLabelFadeDistance = 56f;

        private IBoundaryContext boundary;
        private bool subscribed;
        private RectTransform guide;
        private float mouseOffset;
        private bool dragging;
        private CanvasGroup presentation;
        private bool visible;

        private readonly List<CanvasGroup> eraPastGroups = new List<CanvasGroup>(2);
        private readonly List<CanvasGroup> eraPresentGroups = new List<CanvasGroup>(2);
        private RectTransform eraPastLabel;
        private RectTransform eraPresentLabel;
        private bool eraLabelsResolved;

        public void SetPresentationVisible(bool value)
        {
            visible = value;
            if (presentation == null) presentation = gameObject.AddComponent<CanvasGroup>();
            presentation.alpha = value ? 1f : 0f;
            presentation.blocksRaycasts = value;
            presentation.interactable = value;
            if (guide != null) guide.gameObject.SetActive(value);
            if (!value)
            {
                SetEraAlpha(eraPastGroups, 1f);
                SetEraAlpha(eraPresentGroups, 1f);
            }
            if (!value && dragging) OnEndDrag(null);
        }

        public void ResetBinding()
        {
            OnEndDrag(null);
            if (boundary != null && subscribed) boundary.OnBoundaryCommitted -= SyncHandleToBoundary;
            boundary = null;
            subscribed = false;
            worldCamera = null;
            SetPresentationVisible(false);
        }

        private void Update()
        {
            if (GameSession.Instance == null || GameSession.Instance.State.Phase != GamePhase.Playing) return;
            // 重新解析活动房间：双房间场景（L01）切换后注册表会换成新房间的控制器。
            if (ServiceRegistry.TryGet<IBoundaryContext>(out var found) && !ReferenceEquals(found, boundary))
            {
                if (boundary != null && subscribed)
                {
                    boundary.OnBoundaryCommitted -= SyncHandleToBoundary;
                }
                boundary = found;
                boundary.OnBoundaryCommitted += SyncHandleToBoundary;
                subscribed = true;
                SyncHandleToBoundary(boundary.CommittedBoundary);
            }
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
            // Camera follow, resizing and Game-view changes move the projected boundary
            // even when B itself has not changed.
            if (boundary != null && visible) SyncHandleToBoundary(boundary.CommittedBoundary);
        }

        private void OnDestroy()
        {
            if (guide != null) Destroy(guide.gameObject);
            if (boundary != null && subscribed)
            {
                boundary.OnBoundaryCommitted -= SyncHandleToBoundary;
            }
        }

        private void LateUpdate()
        {
            if (boundary != null) SyncHandleToBoundary(boundary.CommittedBoundary);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!visible || GameSession.Instance == null || !GameSession.Instance.State.IsBoundaryAdjustAllowed) return;
            if (boundary == null && !ServiceRegistry.TryGet<IBoundaryContext>(out boundary))
            {
                return;
            }
            // 进入“调整时间”状态：指针变化、主角站稳、候选状态出现轮廓（策划 5.3）。
            if (boundary is BoundaryController controller)
            {
                controller.BeginMouseDrag();
                if (!controller.IsAdjusting) return;
                if (worldCamera != null)
                    mouseOffset = boundary.CommittedBoundary - worldCamera.ScreenToWorldPoint(eventData.position).x;
            }
            dragging = true;
            // 冻结镜头跟随（策划 5.3：拖动时镜头保持不动）。
            if (ServiceRegistry.TryGet<ICameraRig>(out var cameraRig))
            {
                cameraRig.SetFollowFrozen(true);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || boundary == null || worldCamera == null)
            {
                return;
            }
            // 鼠标屏幕位置 → 当前房间世界坐标（策划 5.3），约束在房间范围内由控制器完成。
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 0f));
            float target = world.x + mouseOffset;
            float margin = ((RectTransform)transform).rect.width * GetComponentInParent<Canvas>().scaleFactor * 0.5f;
            if (eventData.position.x <= margin) target = boundary.RangeMin;
            else if (eventData.position.x >= Screen.width - margin) target = boundary.RangeMax;
            boundary.RequestAdjustment(target);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
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
            if (worldCamera == null || boundary == null)
            {
                return;
            }
            float clamped = Mathf.Clamp(committed, boundary.RangeMin, boundary.RangeMax);
            // 把手水平位置映射到屏幕宽度（UI 锚点自定，这里用像素）。
            RectTransform parent = transform.parent as RectTransform;
            if (parent != null)
            {
                RectTransform handle = (RectTransform)transform;
                Canvas canvas = GetComponentInParent<Canvas>();
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera : null;
                Vector3 screen = worldCamera.WorldToScreenPoint(new Vector3(clamped, 0f, 0f));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var local))
                {
                    float anchorX = Mathf.Lerp(parent.rect.xMin, parent.rect.xMax,
                        (handle.anchorMin.x + handle.anchorMax.x) * 0.5f);
                    float half = handle.rect.width * 0.5f;
                    // 把手中心就是边界线本身：只保证整块把手留在屏幕内，
                    // 不再为了避让年代角标把它硬停在角落。
                    float handleX = Mathf.Clamp(local.x, parent.rect.xMin + half, parent.rect.xMax - half);
                    handle.anchoredPosition = new Vector2(handleX - anchorX, handle.anchoredPosition.y);
                    if (guide == null)
                    {
                        var go = new GameObject("CommittedBoundaryGuide", typeof(RectTransform), typeof(Image));
                        guide = (RectTransform)go.transform;
                        guide.SetParent(parent, false);
                        guide.SetAsFirstSibling();
                        guide.anchorMin = guide.anchorMax = new Vector2(0.5f, 0.5f);
                        var image = go.GetComponent<Image>();
                        image.color = new Color(0.85f, 0.58f, 0.2f, 0.5f);
                        image.raycastTarget = false;
                    }
                    guide.gameObject.SetActive(visible);
                    guide.sizeDelta = new Vector2(2f, parent.rect.height * 0.84f);
                    guide.anchoredPosition = new Vector2(local.x, 0f);

                    SyncEraLabels(parent, handleX, half);
                }
            }
        }

        /// <summary>把手扫到角落时让“1994 / 2024”告示牌平滑淡出，避免遮挡与硬停。</summary>
        private void SyncEraLabels(RectTransform parent, float handleX, float half)
        {
            if (!eraLabelsResolved) ResolveEraLabels(parent);
            if (!visible)
            {
                SetEraAlpha(eraPastGroups, 1f);
                SetEraAlpha(eraPresentGroups, 1f);
                return;
            }
            SetEraAlpha(eraPastGroups, EraLabelAlpha(eraPastLabel, parent, handleX, half));
            SetEraAlpha(eraPresentGroups, EraLabelAlpha(eraPresentLabel, parent, handleX, half));
        }

        private float EraLabelAlpha(RectTransform label, RectTransform parent, float handleX, float half)
        {
            if (label == null) return 1f;
            Vector3 localCenter = parent.InverseTransformPoint(label.TransformPoint(label.rect.center));
            float distance = Mathf.Abs(handleX - localCenter.x);
            float contact = half + label.rect.width * 0.5f;
            return Mathf.Clamp01((distance - contact) / Mathf.Max(1f, eraLabelFadeDistance));
        }

        private void ResolveEraLabels(RectTransform parent)
        {
            eraPastLabel = parent.Find("EraLabelPast") as RectTransform;
            eraPresentLabel = parent.Find("EraLabelPresent") as RectTransform;
            if (eraPastLabel == null || eraPresentLabel == null) return;
            eraLabelsResolved = true;
            CollectEraGroups(eraPastLabel, "EraLabelPastPlate", eraPastGroups);
            CollectEraGroups(eraPresentLabel, "EraLabelPresentPlate", eraPresentGroups);
            // 把手压在告示牌之上，淡出过渡期间不会被半透明的牌子盖住。
            transform.SetAsLastSibling();
        }

        private static void CollectEraGroups(RectTransform label, string plateName, List<CanvasGroup> groups)
        {
            groups.Clear();
            AddGroup(label, groups);
            Transform parent = label != null ? label.parent : null;
            if (parent != null) AddGroup(parent.Find(plateName) as RectTransform, groups);
        }

        private static void AddGroup(RectTransform rect, List<CanvasGroup> groups)
        {
            if (rect == null) return;
            if (!rect.TryGetComponent(out CanvasGroup group))
            {
                group = rect.gameObject.AddComponent<CanvasGroup>();
            }
            groups.Add(group);
        }

        private static void SetEraAlpha(List<CanvasGroup> groups, float alpha)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null) groups[i].alpha = alpha;
            }
        }
    }
}
