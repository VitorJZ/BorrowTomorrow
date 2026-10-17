using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    // Builds its own responsive overlay; the existing gameplay HUD remains intact.
    public sealed class NarrativePresentation : MonoBehaviour, INarrativePresentation
    {
        public Font font;
        private Canvas canvas;
        private GameObject panel;
        private Image backdrop;
        private RawImage picture;
        private Text speaker;
        private Text body;
        private Text title;
        private Text help;
        private GameObject next;
        private GameObject skip;
        private bool blocking;
        private bool credits;
        private void Awake()
        {
            ServiceRegistry.Register<INarrativePresentation>(this);
            var go = new GameObject("NarrativeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            panel = new GameObject("Story", typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            RectTransform rt = panel.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            backdrop = Node<Image>("Backdrop", panel.transform, Vector2.zero, Vector2.zero, true);
            picture = Node<RawImage>("Illustration", panel.transform, new Vector2(0,110), new Vector2(1380,720));
            picture.raycastTarget = false;
            var box = Node<Image>("ReadingPanel", panel.transform, new Vector2(0,-405), new Vector2(1780,230));
            box.color = new Color(0.075f,0.065f,0.055f,0.96f); box.raycastTarget = false;
            speaker = Label("Speaker",new Vector2(-10,-323),new Vector2(1640,45),27,TextAnchor.MiddleLeft);
            body = Label("Subtitle",new Vector2(-10,-407),new Vector2(1640,112),34,TextAnchor.UpperLeft);
            title = Label("Title",Vector2.zero,new Vector2(1200,160),52,TextAnchor.MiddleCenter);
            help = Label("ContinueHint",new Vector2(-300,-493),new Vector2(1000,34),22,TextAnchor.MiddleLeft);
            next = Button("继续",new Vector2(520,-493),() => { if (ServiceRegistry.TryGet<IDialogueService>(out var d)) d.Advance(); });
            skip = Button("跳过本段",new Vector2(740,-493),() => { if (ServiceRegistry.TryGet<IDialogueService>(out var d)) d.SkipCurrent(); });
            panel.SetActive(false);
        }
        private void OnDestroy() => ServiceRegistry.Unregister<INarrativePresentation>(this);
        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null) return;
            canvas.enabled = !session.State.IsPaused;
            if (session.State.Phase == GamePhase.Credits && !credits)
            {
                credits = true;
                panel.SetActive(true); backdrop.color = Color.black; backdrop.raycastTarget = true;
                picture.gameObject.SetActive(false); speaker.gameObject.SetActive(false); body.gameObject.SetActive(false);
                next.SetActive(false); skip.SetActive(false); help.gameObject.SetActive(false); title.gameObject.SetActive(true);
                title.text = "借一点明天\n\n谢谢你陪外婆走过这段回忆。";
            }
            else if (credits && session.State.Phase != GamePhase.Credits) { credits = false; Close(); }
        }
        public void Present(DialogueEntry entry, string text, bool locksControl, int page, int pages)
        {
            blocking = locksControl;
            panel.SetActive(true);
            bool image = entry.illustration != null;
            backdrop.color = new Color(0.025f,0.022f,0.02f,(image || entry.blackBackground || entry.centeredTitle) ? 1f : 0f);
            // Blocking background clicks also avoids dragging the time handle underneath an illustration.
            backdrop.raycastTarget = locksControl;
            picture.gameObject.SetActive(image);
            if (image)
            {
                picture.texture = entry.illustration;
                float ratio = (float)entry.illustration.width / entry.illustration.height;
                Vector2 size = ratio > 1380f/720f ? new Vector2(1380,1380/ratio) : new Vector2(720*ratio,720);
                picture.rectTransform.sizeDelta = size;
            }
            bool isTitle = entry.centeredTitle;
            title.gameObject.SetActive(isTitle); title.text = isTitle ? text : string.Empty;
            speaker.gameObject.SetActive(!isTitle); body.gameObject.SetActive(!isTitle);
            panel.transform.Find("ReadingPanel").gameObject.SetActive(!isTitle);
            speaker.text = entry.speakerLabel;
            speaker.color = entry.source == "Tape" ? new Color(0.96f,0.77f,0.43f) : entry.source == "Reality" ? new Color(0.65f,0.86f,0.94f) : Color.white;
            body.text = text;
            help.gameObject.SetActive(!isTitle);
            help.text = locksControl ? "E / 鼠标左键继续" + (pages > 1 ? "    " + page + " / " + pages : "") : string.Empty;
            next.SetActive(locksControl && entry.holdForConfirm && !isTitle);
            skip.SetActive(locksControl && !isTitle);
        }
        public void Close() { if (panel != null) panel.SetActive(false); }
        private T Node<T>(string name, Transform parent, Vector2 position, Vector2 size, bool stretch=false) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch) { rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero; }
            else { rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size; }
            return go.AddComponent<T>();
        }
        private Text Label(string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment)
        {
            Text t=Node<Text>(name,panel.transform,pos,size);t.font=font;t.fontSize=fontSize;t.alignment=alignment;t.color=Color.white;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }
        private GameObject Button(string label, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            Image image=Node<Image>(label,panel.transform,pos,new Vector2(190,42)); image.color=new Color(.22f,.19f,.15f,1);
            var b=image.gameObject.AddComponent<Button>();b.onClick.AddListener(action);
            var t=Node<Text>("Label",image.transform,Vector2.zero,new Vector2(180,40));t.font=font;t.fontSize=23;t.text=label;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;
            return image.gameObject;
        }
    }
}
