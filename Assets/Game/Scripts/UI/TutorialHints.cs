using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 操作提示用的皮肤引用。由编辑器安装器把现有的旧纸吊牌、
    /// 键帽、怀表和箭头素材挂到 NarrativePresentation 上，运行时再传给本组件。
    /// 任一字段为空时自动退回素色卡片，不影响流程。
    /// </summary>
    [Serializable]
    public sealed class TutorialHintSkin
    {
        public Sprite card;
        public Sprite keycap;
        public Sprite keycapWide;
        public Sprite clock;
        public Sprite arrow;
        public Sprite ornament;
    }

    /// <summary>
    /// L00 操作提示卡：旧纸吊牌 + 象牙键帽 + 手绘时间图标，
    /// 与项目现有对白/目标 UI 同一套纸张与木料质感。
    /// 只负责呈现与淡入淡出，显示时机由 IntroTutorialDirector 决定。
    /// </summary>
    public sealed class TutorialHints : MonoBehaviour
    {
        private static readonly Color CardColor = new Color(0.065f, 0.055f, 0.048f, 0.93f);
        private static readonly Color PaperColor = new Color(0.97f, 0.93f, 0.85f, 1f);
        private static readonly Color InkColor = new Color(0.16f, 0.09f, 0.045f, 1f);
        private static readonly Color SoftInkColor = new Color(0.22f, 0.13f, 0.06f, 1f);
        private static readonly Color GoldColor = new Color(0.85f, 0.67f, 0.36f, 1f);

        private Font font;
        private TutorialHintSkin skin;
        private bool paperCard;

        private CanvasGroup controlsGroup;
        private CanvasGroup timelineGroup;
        private RectTransform controlsRect;
        private RectTransform timelineRect;
        private RectTransform controlsOrnament;
        private RectTransform timelineArrow;
        private Vector2 timelineArrowBase;
        private Vector2 controlsBase;
        private Vector2 timelineBase;
        private Coroutine controlsRoutine;
        private Coroutine timelineRoutine;
        private bool controlsVisible;
        private bool timelineVisible;

        public static TutorialHints Create(Transform parent, Font font, TutorialHintSkin skin)
        {
            var go = new GameObject("TutorialHints", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var hints = go.AddComponent<TutorialHints>();
            hints.font = font != null ? font : Font.CreateDynamicFontFromOSFont(
                new[] { "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS" }, 24);
            hints.skin = skin;
            hints.paperCard = skin != null && skin.card != null;
            hints.Build();
            go.transform.SetAsLastSibling();
            return hints;
        }

        private void Build()
        {
            Color primary = paperCard ? InkColor : PaperColor;
            Color secondary = paperCard ? SoftInkColor : new Color(PaperColor.r, PaperColor.g, PaperColor.b, .78f);

            // 底部：基础移动/跳跃。旧纸吊牌自带左侧绳扣，内容从绳扣右侧开始。
            controlsRect = Card("ControlsHint", new Vector2(.5f, 0f), new Vector2(0f, 172f),
                new Vector2(760f, 168f), new Vector2(.5f, 0f), out controlsGroup);
            controlsBase = controlsRect.anchoredPosition;
            Keycap(controlsRect, "A", new Vector2(-272f, 26f), new Vector2(56f, 56f), skin != null ? skin.keycap : null,
                primary, 26);
            Keycap(controlsRect, "D", new Vector2(-208f, 26f), new Vector2(56f, 56f), skin != null ? skin.keycap : null,
                primary, 26);
            Label(controlsRect, "移动", new Vector2(-118f, 26f), new Vector2(90f, 40f), 27, primary,
                TextAnchor.MiddleLeft);
            Divider(controlsRect, new Vector2(-46f, 26f), 34f);
            Keycap(controlsRect, "", new Vector2(62f, 26f), new Vector2(112f, 52f),
                skin != null ? skin.keycapWide : null, primary, 20);
            Label(controlsRect, "空格 · 跳跃", new Vector2(220f, 26f), new Vector2(190f, 40f), 24, primary,
                TextAnchor.MiddleLeft);
            Label(controlsRect, "先四处走走，看看墙上的通知。", new Vector2(0f, -42f), new Vector2(620f, 32f), 22,
                secondary, TextAnchor.MiddleCenter);
            controlsOrnament = Ornament(controlsRect, 360f);

            // 顶部：时间边界拖动提示。怀表在左，标题与说明靠左，箭头在右。
            timelineRect = Card("TimelineHint", new Vector2(.5f, 1f), new Vector2(0f, -104f),
                new Vector2(840f, 168f), new Vector2(.5f, 1f), out timelineGroup);
            timelineBase = timelineRect.anchoredPosition;
            if (skin != null && skin.clock != null)
            {
                var clock = Node(timelineRect, "ClockIcon", new Vector2(-300f, 4f), new Vector2(92f, 92f));
                var clockImage = clock.gameObject.AddComponent<Image>();
                clockImage.sprite = skin.clock;
                clockImage.preserveAspect = true;
                clockImage.raycastTarget = false;
                var clockShadow = clock.gameObject.AddComponent<Shadow>();
                clockShadow.effectColor = new Color(0f, 0f, 0f, .28f);
                clockShadow.effectDistance = new Vector2(0f, -3f);
            }
            Label(timelineRect, "拖动上方的时间边界", new Vector2(70f, 28f), new Vector2(600f, 46f), 30, primary,
                TextAnchor.MiddleLeft);
            Label(timelineRect, "向右推过木箱，回到 1994 年", new Vector2(70f, -30f), new Vector2(600f, 36f), 22,
                secondary, TextAnchor.MiddleLeft);
            if (skin != null && skin.arrow != null)
            {
                var arrow = Node(timelineRect, "ArrowIcon", new Vector2(320f, 28f), new Vector2(78f, 40f));
                var arrowImage = arrow.gameObject.AddComponent<Image>();
                arrowImage.sprite = skin.arrow;
                arrowImage.preserveAspect = true;
                arrowImage.raycastTarget = false;
                timelineArrow = arrow;
                timelineArrowBase = arrow.anchoredPosition;
            }
            else
            {
                Label(timelineRect, "→", new Vector2(320f, 28f), new Vector2(60f, 40f), 30, GoldColor,
                    TextAnchor.MiddleCenter);
            }
            Ornament(timelineRect, 400f);

            controlsGroup.alpha = 0f;
            timelineGroup.alpha = 0f;
            controlsGroup.gameObject.SetActive(false);
            timelineGroup.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool anyVisible = (controlsGroup != null && controlsGroup.gameObject.activeSelf)
                || (timelineGroup != null && timelineGroup.gameObject.activeSelf);
            if (!anyVisible)
            {
                return;
            }
            if (controlsOrnament != null)
            {
                float pulse = 0.9f + 0.1f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f));
                controlsOrnament.localScale = new Vector3(pulse, 1f, 1f);
            }
            if (timelineArrow != null)
            {
                timelineArrow.anchoredPosition = timelineArrowBase +
                    new Vector2(Mathf.Sin(Time.unscaledTime * 2.5f) * 4f, 0f);
            }
        }

        public void ShowControls()
        {
            if (controlsGroup == null || controlsVisible)
            {
                return;
            }
            controlsVisible = true;
            if (controlsRoutine != null) StopCoroutine(controlsRoutine);
            controlsRoutine = StartCoroutine(Fade(controlsGroup, controlsRect, controlsBase, true, -14f, 0.4f));
        }

        public void HideControls()
        {
            if (controlsGroup == null || !controlsVisible)
            {
                return;
            }
            controlsVisible = false;
            if (controlsRoutine != null) StopCoroutine(controlsRoutine);
            controlsRoutine = StartCoroutine(Fade(controlsGroup, controlsRect, controlsBase, false, -10f, 0.28f));
        }

        public void ShowTimeline()
        {
            if (timelineGroup == null || timelineVisible)
            {
                return;
            }
            timelineVisible = true;
            if (timelineRoutine != null) StopCoroutine(timelineRoutine);
            timelineRoutine = StartCoroutine(Fade(timelineGroup, timelineRect, timelineBase, true, 12f, 0.42f));
        }

        public void HideTimeline()
        {
            if (timelineGroup == null || !timelineVisible)
            {
                return;
            }
            timelineVisible = false;
            if (timelineRoutine != null) StopCoroutine(timelineRoutine);
            timelineRoutine = StartCoroutine(Fade(timelineGroup, timelineRect, timelineBase, false, 8f, 0.3f));
        }

        private IEnumerator Fade(CanvasGroup group, RectTransform rect, Vector2 basePosition, bool show,
            float offset, float seconds)
        {
            group.gameObject.SetActive(true);
            float startAlpha = group.alpha;
            float endAlpha = show ? 1f : 0f;
            float startOffset = rect.anchoredPosition.y - basePosition.y;
            float endOffset = show ? 0f : offset;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                group.alpha = Mathf.Lerp(startAlpha, endAlpha, k);
                rect.anchoredPosition = basePosition + Vector2.up * Mathf.Lerp(startOffset, endOffset, k);
                yield return null;
            }
            group.alpha = endAlpha;
            rect.anchoredPosition = basePosition;
            if (!show)
            {
                group.gameObject.SetActive(false);
            }
        }

        private RectTransform Card(string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot,
            out CanvasGroup group)
        {
            RectTransform rect = Node(transform, name, position, size);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            var background = rect.gameObject.AddComponent<Image>();
            if (paperCard)
            {
                background.sprite = skin.card;
                background.color = Color.white;
            }
            else
            {
                background.color = CardColor;
            }
            background.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, paperCard ? .32f : .5f);
            shadow.effectDistance = new Vector2(0f, paperCard ? -4f : -5f);
            group = rect.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return rect;
        }

        private void Divider(RectTransform parent, Vector2 position, float height)
        {
            RectTransform rect = Node(parent, "Divider", position, new Vector2(1.5f, height));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = paperCard ? new Color(InkColor.r, InkColor.g, InkColor.b, .28f)
                : new Color(GoldColor.r, GoldColor.g, GoldColor.b, .45f);
            image.raycastTarget = false;
        }

        private RectTransform Ornament(RectTransform card, float width)
        {
            RectTransform rect = Node(card, "Ornament", Vector2.zero, new Vector2(width, 18f));
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 6f);
            var image = rect.gameObject.AddComponent<Image>();
            if (skin != null && skin.ornament != null)
            {
                image.sprite = skin.ornament;
                image.color = paperCard ? new Color(1f, 1f, 1f, .8f)
                    : new Color(GoldColor.r, GoldColor.g, GoldColor.b, .6f);
            }
            else
            {
                rect.sizeDelta = new Vector2(width, 2f);
                image.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, .55f);
            }
            image.raycastTarget = false;
            return rect;
        }

        private void Keycap(Transform parent, string text, Vector2 position, Vector2 size, Sprite art,
            Color textColor, int fontSize)
        {
            RectTransform rect = Node(parent, "Key_" + text, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            if (art != null)
            {
                image.sprite = art;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.93f, 0.88f, 0.78f, .97f);
            }
            image.raycastTarget = false;
            if (art == null)
            {
                var shadow = rect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.25f, .16f, .08f, .45f);
                shadow.effectDistance = new Vector2(0f, -3f);
            }
            Label(rect, text, Vector2.zero, size, fontSize, art != null ? InkColor : textColor,
                TextAnchor.MiddleCenter);
        }

        private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color,
            TextAnchor alignment)
        {
            RectTransform rect = Node(parent, "Text_" + text, position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.text = text;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = paperCard ? new Color(.18f, .10f, .05f, .22f) : new Color(.05f, .03f, .015f, .75f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        private static RectTransform Node(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
