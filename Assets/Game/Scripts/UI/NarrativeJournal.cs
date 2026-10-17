using System.Linq;
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
        private Canvas canvas;
        private GameObject panel;
        private Text text;
        private Text hint;
        private bool opened;
        private ScrollRect scroll;
        private void Start()
        {
            var go=new GameObject("JournalCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            hint=Label(go.transform,"J 查看已读对白",new Vector2(700,-495),new Vector2(400,40),22);
            panel=new GameObject("Records",typeof(RectTransform));panel.transform.SetParent(go.transform,false);var rt=panel.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var bg=panel.AddComponent<Image>();bg.color=new Color(.06f,.055f,.045f,.98f);
            Label(panel.transform,"已读对白    ·    J 关闭",new Vector2(0,455),new Vector2(1640,70),34);
            var view=Rect(panel.transform,"Viewport",new Vector2(0,-10),new Vector2(1640,790));view.gameObject.AddComponent<RectMask2D>();
            scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.horizontal=false;
            var content=Rect(view,"Content",Vector2.zero,new Vector2(1600,790));content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);
            text=content.gameObject.AddComponent<Text>();text.font=font;text.fontSize=28;text.lineSpacing=1.35f;text.color=new Color(.94f,.9f,.82f);text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;
            scroll.content=content;panel.SetActive(false);
        }
        private void Update()
        {
            var session=GameSession.Instance;if(session==null||canvas==null)return;
            canvas.enabled=!session.State.IsPaused;
            hint.enabled=session.State.Phase==GamePhase.Playing && session.State.SubState==PlayingSubState.Free;
            if(session.State.Phase!=GamePhase.Playing) { opened=false;panel.SetActive(false);return; }
            if(Input.GetKeyDown(KeyCode.J) && !session.State.IsPaused)
            {
                if(opened) { opened=false;panel.SetActive(false);session.State.SetSubState(PlayingSubState.Free);return; }
                if(session.State.SubState!=PlayingSubState.Free || !ServiceRegistry.TryGet<IDialogueService>(out var dialogue) || dialogue.IsPlaying)return;
                opened=true;session.State.SetSubState(PlayingSubState.Cutscene);
                var lines=new StringBuilder();
                if(ServiceRegistry.TryGet<DialogueLibrary>(out var library))foreach(var entry in library.Entries)
                {
                    if(entry!=null&&session.ReadDialogueIds.Contains(entry.entryId))lines.Append(entry.speakerLabel).Append("\n").Append(entry.source=="Tape"?"「"+entry.subtitle+"」":entry.subtitle).Append("\n\n");
                }
                text.text=lines.Length>0?lines.ToString():"还没有读过的对白。";
                panel.SetActive(true);Canvas.ForceUpdateCanvases();text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(790,text.preferredHeight+40));scroll.verticalNormalizedPosition=1;
            }
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
