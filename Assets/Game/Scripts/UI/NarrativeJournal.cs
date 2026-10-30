using System.Text;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using UnityEngine;
using UnityEngine.UI;
namespace BorrowTomorrow.UI
{
    public sealed class NarrativeJournal : MonoBehaviour
    {
        public Font font;
        public Sprite journalSkin;
        [SerializeField, HideInInspector] private Canvas canvas;
        [SerializeField, HideInInspector] private GameObject panel;
        [SerializeField, HideInInspector] private Text text;
        [SerializeField, HideInInspector] private Text hint;
        [SerializeField, HideInInspector] private ScrollRect scroll;
        public bool IsOpen => panel!=null && panel.activeSelf;
        private void Start()
        {
            var go=new GameObject("JournalCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            hint=Label(go.transform,"J  查看已读对白",Vector2.zero,new Vector2(300,36),20);
            // Bottom-right corner on every aspect ratio, clear of the dialogue buttons.
            var hr=hint.rectTransform;hr.anchorMin=hr.anchorMax=hr.pivot=new Vector2(1,0);hr.anchoredPosition=new Vector2(-36,22);
            hint.alignment=TextAnchor.MiddleRight;hint.color=new Color(.95f,.9f,.8f,.7f);
            var shadow=hint.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.6f);shadow.effectDistance=new Vector2(1.5f,-1.5f);
            panel=new GameObject("Records",typeof(RectTransform));panel.transform.SetParent(go.transform,false);var rt=panel.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var bg=panel.AddComponent<Image>();bg.color=new Color(.06f,.055f,.045f,.98f);
            if(journalSkin!=null){bg.sprite=journalSkin;bg.color=Color.white;}
            Label(panel.transform,"已读对白    ·    滚轮或拖动翻阅",new Vector2(-140,455),new Vector2(1360,70),34);
            var closeRect=Rect(panel.transform,"CloseButton",new Vector2(700,455),new Vector2(220,70));
            var closeLabel=Label(closeRect,"J  关闭 ×",Vector2.zero,new Vector2(220,70),34);
            closeLabel.alignment=TextAnchor.MiddleCenter;
            var closeSurface=closeRect.gameObject.AddComponent<Image>();closeSurface.color=Color.clear;
            var closeButton=closeRect.gameObject.AddComponent<Button>();closeButton.targetGraphic=closeSurface;closeButton.onClick.AddListener(Close);
            var view=Rect(panel.transform,"Viewport",new Vector2(0,-10),new Vector2(1640,790));view.gameObject.AddComponent<RectMask2D>();
            // The text does not receive pointer events. Give the viewport a raycast
            // surface so wheel/drag events reach ScrollRect instead of the book background.
            var surface=view.gameObject.AddComponent<Image>();surface.color=Color.clear;
            scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.horizontal=false;scroll.scrollSensitivity=45f;
            var content=Rect(view,"Content",Vector2.zero,new Vector2(1600,790));content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(-64,790);content.anchoredPosition=new Vector2(-12,0);
            text=content.gameObject.AddComponent<Text>();text.font=font;text.fontSize=28;text.lineSpacing=1.35f;text.color=new Color(.94f,.9f,.82f);text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;
            scroll.content=content;
            CreateScrollbar(view);
            panel.SetActive(false);
            if(journalSkin!=null){text.color=new Color(.2f,.12f,.07f);foreach(var heading in panel.GetComponentsInChildren<Text>(true))heading.color=new Color(.2f,.12f,.07f);}
        }
        private void OnDestroy() { if(canvas!=null) Destroy(canvas.gameObject); }
        private void Update()
        {
            // Closing must still work if a script reload has cleared the session singleton.
            var session=GameSession.Instance;
            if(Input.GetKeyDown(KeyCode.J))Toggle();
            if(session==null||canvas==null)return;
            canvas.enabled=!session.State.IsPaused;
            hint.enabled=session.State.Phase==GamePhase.Playing && session.State.SubState==PlayingSubState.Free
                && !(ServiceRegistry.TryGet<IDialogueService>(out var playing) && playing.IsPlaying);
            if(session.State.Phase!=GamePhase.Playing)Close();
        }
        public void Toggle()
        {
            var session=GameSession.Instance;
            if(session!=null && session.State.IsPaused)return;
            if(IsOpen) { Close();return; }
            if(session==null||canvas==null||session.State.Phase!=GamePhase.Playing
                ||session.State.SubState!=PlayingSubState.Free
                ||!ServiceRegistry.TryGet<IDialogueService>(out var dialogue)||dialogue.IsPlaying)return;
            session.State.SetSubState(PlayingSubState.Cutscene);
            text.text=BuildRecords(session);
            panel.SetActive(true);Canvas.ForceUpdateCanvases();text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(790,text.preferredHeight+40));scroll.verticalNormalizedPosition=1;
        }
        public void Close()
        {
            var session=GameSession.Instance;
            if(!IsOpen || (session!=null && session.State.IsPaused))return;
            panel.SetActive(false);
            if(session!=null && session.State.SubState==PlayingSubState.Cutscene
                && !(ServiceRegistry.TryGet<IDialogueService>(out var dialogue) && dialogue.IsPlaying))
                session.State.SetSubState(PlayingSubState.Free);
        }
        private static string BuildRecords(GameSession session)
        {
            var lines=new StringBuilder();
            if(ServiceRegistry.TryGet<DialogueLibrary>(out var library))foreach(var id in session.ReadDialogueIds)
            {
                var entry=library.Find(id);
                if(entry!=null)lines.Append(entry.speakerLabel).Append("\n").Append(entry.source=="Tape"?"「"+entry.subtitle+"」":entry.subtitle).Append("\n\n");
            }
            return lines.Length>0?lines.ToString():"还没有读过的对白。";
        }
        private void CreateScrollbar(RectTransform view)
        {
            var track=Rect(view,"Scrollbar",Vector2.zero,new Vector2(18,0));
            track.anchorMin=new Vector2(1,0);track.anchorMax=new Vector2(1,1);track.pivot=new Vector2(1,.5f);track.anchoredPosition=new Vector2(-4,0);
            var background=track.gameObject.AddComponent<Image>();background.color=new Color(.4f,.3f,.2f,.16f);
            var handle=Rect(track,"Handle",Vector2.zero,Vector2.zero);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;
            var grip=handle.gameObject.AddComponent<Image>();grip.color=new Color(.4f,.3f,.2f,.75f);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=grip;bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        private RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;return r;
        }
        private Text Label(Transform parent,string value,Vector2 pos,Vector2 size,int fontSize)
        {
            var r=Rect(parent,value,pos,size);var t=r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=fontSize;t.color=Color.white;t.text=value;t.raycastTarget=false;return t;
        }
    }
}
