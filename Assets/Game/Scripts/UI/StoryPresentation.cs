using System.Collections.Generic;
using System.Text;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    /// <summary>剧情插画与已读文字回看；插画只作 UI，不改变关卡碰撞。</summary>
    public sealed class StoryPresentation : MonoBehaviour, IStoryPresentation
    {
        [SerializeField] private Font font;
        [SerializeField] private DialogueEntry[] entries = new DialogueEntry[0];
        private readonly Dictionary<string, DialogueEntry> byId = new Dictionary<string, DialogueEntry>();
        private GameObject illustrationRoot, recordsRoot;
        private RawImage illustration;
        private AspectRatioFitter aspect;
        private Text records, heading;
        private ScrollRect scroll;
        private string shownObjective;
        private string shownIds;
        private bool showingIllustration;

        private void Awake()
        {
            foreach (var entry in entries) if (entry != null) byId[entry.entryId] = entry;
            ServiceRegistry.Register<IStoryPresentation>(this);
            var pictures = CanvasRoot("StoryIllustration", -1);
            illustrationRoot = pictures.gameObject;
            Panel(pictures, "Backdrop", new Color(0.035f, 0.045f, 0.04f, 0.98f));
            var frame = Rect(pictures, "PictureFrame", new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.94f));
            illustration = Rect(frame, "Picture", Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            illustration.raycastTarget = false;
            aspect = illustration.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var instructions = Label(pictures, "ContinueHint", new Vector2(0.2f, 0.17f), new Vector2(0.8f, 0.22f), 22);
            instructions.text = "E 跳过配音 / 继续　　按住 Tab 回看文字　　Esc 暂停";
            instructions.alignment = TextAnchor.MiddleCenter;
            illustrationRoot.SetActive(false);

            var history = CanvasRoot("StoryRecords", 10);
            recordsRoot = history.gameObject;
            Panel(history, "Backdrop", new Color(0.045f, 0.065f, 0.055f, 0.98f));
            heading = Label(history, "Objective", new Vector2(0.1f, 0.83f), new Vector2(0.9f, 0.96f), 28);
            var viewport = Rect(history, "Viewport", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.81f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var background = viewport.gameObject.AddComponent<Image>();
            background.color = new Color(0.1f, 0.13f, 0.11f, 0.5f);
            records = Label(viewport, "ReadLines", Vector2.zero, Vector2.one, 25);
            records.supportRichText = false;
            records.alignment = TextAnchor.UpperLeft;
            records.rectTransform.anchorMin = new Vector2(0f, 1f);
            records.rectTransform.anchorMax = Vector2.one;
            records.rectTransform.pivot = new Vector2(0.5f, 1f);
            records.horizontalOverflow = HorizontalWrapMode.Wrap;
            records.verticalOverflow = VerticalWrapMode.Overflow;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = records.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            recordsRoot.SetActive(false);
        }

        private RectTransform CanvasRoot(string label, int order)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }
        private static RectTransform Rect(Transform parent, string label, Vector2 min, Vector2 max)
        {
            var go = new GameObject(label, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static void Panel(Transform parent, string label, Color color)
        {
            var image = Rect(parent, label, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
        }
        private Text Label(Transform parent, string label, Vector2 min, Vector2 max, int size)
        {
            var text = Rect(parent, label, min, max).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size;
            text.color = new Color(0.96f, 0.92f, 0.82f);
            text.raycastTarget = false;
            return text;
        }
        private void Update()
        {
            var session = GameSession.Instance;
            illustrationRoot.SetActive(showingIllustration && session != null
                && session.State.Phase == GamePhase.Playing && !session.State.IsPaused);
            bool show = session != null && session.State.Phase == GamePhase.Playing
                && !session.State.IsPaused && ServiceRegistry.TryGet<IInputSource>(out var input) && input.ObjectiveHeld;
            recordsRoot.SetActive(show);
            if (!show) return;
            string ids = string.Join("|", session.ReadDialogueIds);
            if (shownIds != ids || shownObjective != session.CurrentObjective)
            {
                shownIds = ids;
                shownObjective = session.CurrentObjective;
                heading.text = "目标与已读记录（松开 Tab 返回；滚轮翻阅）\n" + shownObjective;
                var builder = new StringBuilder();
                foreach (string id in session.ReadDialogueIds)
                    if (byId.TryGetValue(id, out var line) && !string.IsNullOrEmpty(line.subtitle))
                        builder.Append(string.IsNullOrEmpty(line.speakerLabel) ? "" : line.speakerLabel + "：")
                            .AppendLine(line.subtitle).AppendLine();
                records.text = builder.Length > 0 ? builder.ToString() : "尚无已读记录。";
                Canvas.ForceUpdateCanvases();
                records.rectTransform.sizeDelta = new Vector2(0f, Mathf.Max(records.preferredHeight + 32f, scroll.viewport.rect.height));
                scroll.verticalNormalizedPosition = 0f;
            }
        }
        public void ShowIllustration(Texture2D texture, bool blackBackground = false)
        {
            illustration.texture = texture;
            showingIllustration = texture != null || blackBackground;
            illustration.enabled = texture != null;
            illustrationRoot.SetActive(showingIllustration);
            if (texture != null) aspect.aspectRatio = (float)texture.width / texture.height;
        }
        public void HideIllustration()
        {
            showingIllustration = false;
            illustrationRoot.SetActive(false);
        }
        private void OnDestroy() => ServiceRegistry.Unregister<IStoryPresentation>(this);
    }
}
