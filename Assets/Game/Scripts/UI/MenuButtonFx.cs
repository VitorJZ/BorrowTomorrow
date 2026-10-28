using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 主菜单木牌按钮的悬停/按压小动效。
    /// 只缩放和位移视觉子节点，按钮的命中区域保持稳定。
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class MenuButtonFx : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private Text label;
        [SerializeField] private float hoverScale = 1.02f;
        [SerializeField] private float pressedScale = 0.985f;
        [SerializeField] private float pressedDrop = 3f;
        [SerializeField] private float response = 18f;
        [SerializeField, Range(0f, 1f)] private float disabledLabelAlpha = 0.45f;

        private Button button;
        private Vector2 restPosition;
        private Vector3 restScale = Vector3.one;
        private Color restLabelColor = Color.white;
        private bool hovered;
        private bool selected;
        private bool pressed;

        private void Awake()
        {
            Cache();
            if (label != null)
            {
                restLabelColor = label.color;
            }
        }

        private void OnEnable()
        {
            Cache();
            hovered = selected = pressed = false;
            if (visual != null)
            {
                visual.anchoredPosition = restPosition;
                visual.localScale = restScale;
            }
            if (label != null)
            {
                label.color = restLabelColor;
            }
        }

        private void Cache()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
            if (visual == null)
            {
                visual = transform as RectTransform;
            }
            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }
            if (visual != null)
            {
                restPosition = visual.anchoredPosition;
                restScale = visual.localScale;
            }
        }

        private void Update()
        {
            if (visual == null)
            {
                return;
            }
            bool interactable = button == null || button.IsInteractable();
            float targetScale = interactable
                ? (pressed ? pressedScale : (hovered || selected ? hoverScale : 1f))
                : 1f;
            Vector2 targetPosition = restPosition;
            if (interactable && pressed)
            {
                targetPosition += new Vector2(0f, -pressedDrop);
            }
            float step = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
            visual.localScale = Vector3.Lerp(visual.localScale, restScale * targetScale, step);
            visual.anchoredPosition = Vector2.Lerp(visual.anchoredPosition, targetPosition, step);
            if (label != null)
            {
                Color target = restLabelColor;
                target.a *= interactable ? 1f : disabledLabelAlpha;
                label.color = Color.Lerp(label.color, target, step);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                pressed = true;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
        }
    }
}
