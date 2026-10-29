using System;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Narrative;
using UnityEngine;
using UnityEngine.UI;
namespace BorrowTomorrow.UI
{
    // Direct manipulation: a click turns paper; a drag moves one fragment.
    // Placed fragments share ONE transform and turn as one incomplete photograph.
    public sealed class PhotoReconstructionPuzzle : NarrativeInteractionGate
    {
        public const string CompletionFlag="L03_PhotoReconstructed_v10";
        public Texture2D workbench,originalFront,originalBack,comparisonFront,comparisonBack;
        public Font font;
        [HideInInspector] public Sprite buttonSkin;
        private static readonly Rect PhotoRect=new Rect(120,175,1000,750),BackRect=new Rect(24,20,82,76);
        private static readonly Vector2[] Tray={new Vector2(1330,300),new Vector2(1660,570),new Vector2(1370,820),new Vector2(1650,280),new Vector2(1330,565)};
        private readonly PhotoReconstructionState state=new PhotoReconstructionState();
        private readonly bool[] looseBack=new bool[5];
        private readonly Vector2[] loosePositions=(Vector2[])Tray.Clone();
        private readonly PhotoFragmentGraphic[] graphics=new PhotoFragmentGraphic[6];
        private readonly Outline[] outlines=new Outline[5];
        private Canvas canvas;
        private RectTransform stage,assemblyRoot,looseRoot;
        private RawImage completedImage;
        private Text message,flipHint,title;
        private Action continueStory;
        private int pressed=-1,oldSlot=-1,beginFrame,hintLevel,flipPiece=-2;
        private bool opened,ownsControl,boardBack,needsRelease,dragging,finishing,flipMidpoint,completeAfterFlip,viewing;
        private Vector2 pressPoint,pressCenter,dragCenter;
        private float pressTime,finishTimer,flipTimer,solvedTimer,messageTimer,titleTimer;
        public bool IsOpen=>opened;
        public bool IsBack=>boardBack;
        public bool IsFlipping=>flipPiece!=-2;
        public bool IsDragging=>dragging;
        public bool IsViewingCompleted=>opened&&viewing;
        public PhotoReconstructionState PuzzleState=>state;
        public override bool ShouldOpen=>GameSession.Instance!=null&&!GameSession.Instance.StoryFlags.HasFlag(CompletionFlag);
        public override bool HasCompletedView=>GameSession.Instance!=null&&GameSession.Instance.StoryFlags.HasFlag("IdentityKnown")&&GameSession.Instance.StoryFlags.HasFlag("PhotoRevealed");
        public override string CompletedPrompt=>"查看合照";
        public override bool Open(Action onContinue)
        {return BeginView(onContinue,false);}
        public override bool OpenCompleted()
        {return BeginView(null,true);}
        private bool BeginView(Action onContinue,bool completed)
        {
            var s=GameSession.Instance;
            if(opened||s==null||s.State.Phase!=GamePhase.Playing||s.State.SubState!=PlayingSubState.Free||(completed?!HasCompletedView:!ShouldOpen))return false;
            if(workbench==null||originalFront==null||originalBack==null||comparisonFront==null||comparisonBack==null||font==null)
            {Debug.LogError("[PhotoPuzzle] Missing assigned art/font.");return false;}
            if(canvas==null)BuildView();
            continueStory=onContinue;opened=ownsControl=true;needsRelease=true;beginFrame=Time.frameCount;
            viewing=completed;if(completed)boardBack=false;
            finishing=dragging=completeAfterFlip=false;finishTimer=solvedTimer=0;pressed=-1;flipPiece=-2;
            assemblyRoot.localScale=Vector3.one;assemblyRoot.localRotation=Quaternion.identity;
            completedImage.rectTransform.localScale=Vector3.one;completedImage.rectTransform.localRotation=Quaternion.identity;
            completedImage.gameObject.SetActive(completed);canvas.gameObject.SetActive(true);
            title.text=completed?"旧合照":"把这张旧照片拼回去";
            titleTimer=5;title.enabled=true;flipHint.enabled=false;
            s.State.SetSubState(PlayingSubState.Cutscene);
            if(ServiceRegistry.TryGet<IHudService>(out var hud)){hud.HideSubtitle();hud.SetInteractionPrompt(false,null,Vector2.zero);}
            SetMessage(completed?"点照片翻面，左上角返回。":"点一下翻面，拖动来拼合。",6);Render();return true;
        }
        private void OnDisable(){if(opened)Close(false);}
        private void OnDestroy(){if(canvas!=null)Destroy(canvas.gameObject);}
        private void OnApplicationFocus(bool focus){if(!focus&&opened){CancelPointer();needsRelease=true;}}
        private void Update()
        {
            if(!opened)return;
            var s=GameSession.Instance;
            if(s==null||s.State.Phase!=GamePhase.Playing){Close(false);return;}
            canvas.enabled=!s.State.IsPaused;
            if(s.State.IsPaused){CancelPointer();needsRelease=true;return;}
            messageTimer-=Time.deltaTime;message.enabled=messageTimer>0;
            titleTimer-=Time.deltaTime;title.enabled=titleTimer>0;
            if(finishing){finishTimer+=Time.deltaTime;if(finishTimer>=.5f)Close(true);return;}
            if(IsFlipping){UpdateFlip();return;}
            ServiceRegistry.TryGet<IInputSource>(out var input);var pointer=input as IPointerInputSource;
            bool held=pointer!=null?pointer.PrimaryHeld:Input.GetMouseButton(0);
            bool down=pointer!=null?pointer.PrimaryPressedThisFrame:Input.GetMouseButtonDown(0);
            Vector2 screen=pointer!=null?pointer.PointerPosition:(Vector2)Input.mousePosition,point=ToStage(screen);
            if(input!=null)input.ConsumeInteract();
            if(Time.frameCount<=beginFrame||needsRelease){if(!held&&Time.frameCount>beginFrame)needsRelease=false;return;}
            if(pointer!=null?pointer.HintPressedThisFrame:Input.GetKeyDown(KeyCode.H))Hint();
            if(viewing){UpdateCompletedView(point,down,held);return;}
            UpdateHover(screen);
            if(down)
            {
                if(BackRect.Contains(point)){Close(false);return;}
                pressed=HitPaper(screen);
                if(pressed>=0)
                {
                    pressTime=Time.unscaledTime;pressPoint=point;pressCenter=VisualCenter(pressed);
                    oldSlot=pressed==5?3:state.SlotOf(pressed);solvedTimer=0;
                    graphics[pressed].transform.SetAsLastSibling();PlayPaper();Render();
                }
            }
            if(pressed>=0)
            {
                if(held)
                {
                    if(!dragging&&Vector2.Distance(pressPoint,point)>9)
                    {
                        if(pressed==5){pressed=-1;needsRelease=true;Render();return;}
                        dragging=true;looseBack[pressed]=oldSlot>=0?boardBack:looseBack[pressed];state.Remove(pressed);
                    }
                    if(dragging)
                    {
                        float initialScale=oldSlot>=0?1:.43f;
                        dragCenter=ClampPaper(point-(pressPoint-pressCenter)/initialScale,pressed,1);
                    }
                    Render();
                }
                else
                {
                    int released=pressed;
                    bool click=!dragging&&Vector2.Distance(pressPoint,point)<=9&&Time.unscaledTime-pressTime<.38f;
                    if(dragging)Drop();
                    pressed=-1;dragging=false;
                    if(click)StartFlip(released==5||state.SlotOf(released)>=0?-1:released);else Render();
                }
            }
            // Solve only after a stable correct arrangement and a released pointer.
            // Dialogue itself remains manually advanced.
            if(state.IsCorrect&&!held&&pressed<0&&!dragging&&!IsFlipping)
            {
                solvedTimer+=Time.deltaTime;
                if(solvedTimer>=.85f){if(boardBack){completeAfterFlip=true;StartFlip(-1);}else BeginComplete();}
            }
            else solvedTimer=0;
        }
        private void Drop()
        {
            int shape=PhotoReconstructionState.Shapes[pressed];
            Vector2 size=Vector2.Scale(PhotoFragmentGraphic.BoundsFor(shape).size,PhotoRect.size);
            // Wide, shape-based catch area, not a hidden pixel-sized drop point.
            bool fits=Vector2.Distance(dragCenter,SlotCenter(shape))<=Mathf.Min(size.x,size.y)*.5f+65;
            if(fits)
            {
                int ejected=state.PieceAt(shape);
                if(ejected>=0&&ejected!=pressed){loosePositions[ejected]=Tray[ejected];looseBack[ejected]=boardBack;}
                state.Place(pressed,shape);
                if(state.FilledSlots==4&&!state.IsCorrect)SetMessage("树枝和衣服好像没接上。",4.5f);else{message.enabled=false;messageTimer=0;}
            }
            else loosePositions[pressed]=Tray[pressed];
            solvedTimer=0;PlayPaper();
        }
        private void CancelPointer()
        {
            if(dragging&&pressed>=0&&pressed<5){if(oldSlot>=0)state.Place(pressed,oldSlot);else loosePositions[pressed]=Tray[pressed];}
            pressed=-1;dragging=false;if(stage!=null&&!finishing)Render();
        }
        // -1 is the entire assembled photograph; 0..4 is one loose fragment.
        private void StartFlip(int piece)
        {
            if(finishing||IsFlipping)return;
            flipPiece=piece;flipTimer=0;flipMidpoint=false;needsRelease=true;solvedTimer=0;
            flipHint.enabled=false;PlayPaper();Render();
        }
        private void UpdateFlip()
        {
            flipTimer+=Time.deltaTime;float t=Mathf.Clamp01(flipTimer/.42f);
            if(!flipMidpoint&&t>=.5f)
            {
                flipMidpoint=true;if(flipPiece==-1)boardBack=!boardBack;else looseBack[flipPiece]=!looseBack[flipPiece];Render();
            }
            float edge=Mathf.Max(.025f,Mathf.Abs(Mathf.Cos(t*Mathf.PI)));
            var target=viewing?completedImage.rectTransform:flipPiece==-1?assemblyRoot:graphics[flipPiece].rectTransform;
            target.localScale=new Vector3(edge,1-.025f*Mathf.Sin(t*Mathf.PI),1);
            target.localRotation=Quaternion.Euler(0,0,2*Mathf.Sin(t*Mathf.PI));
            if(t<1)return;
            target.localScale=Vector3.one;target.localRotation=Quaternion.identity;flipPiece=-2;Render();
            if(completeAfterFlip){completeAfterFlip=false;BeginComplete();}
        }
        private void Hint()
        {
            if(viewing){SetMessage("点照片可以翻看背面，左上角返回回廊。",5);return;}
            string[] hints={"看看校服、树枝和墙上的线条能不能接上。","混进了另一年的照片。留意树干的粗细和校服衣领。","点照片翻到背面，找能接成‘1994年秋’的字迹。"};
            SetMessage(hints[Mathf.Min(hintLevel++,hints.Length-1)],7);
        }
        private void BeginComplete()
        {
            finishing=true;pressed=-1;dragging=false;flipHint.enabled=false;
            GameSession.Instance.StoryFlags.SetFlag(CompletionFlag);
            completedImage.texture=originalFront;completedImage.gameObject.SetActive(true);completedImage.transform.SetAsLastSibling();
            foreach(var g in graphics)g.gameObject.SetActive(false);SetMessage("拼好了。",2);PlayPaper();
        }
        private void Close(bool advance)
        {
            if(!opened)return;
            opened=false;pressed=-1;dragging=false;flipPiece=-2;if(canvas!=null)canvas.gameObject.SetActive(false);
            var callback=continueStory;continueStory=null;bool controlled=ownsControl;ownsControl=false;if(advance)callback?.Invoke();
            if(controlled&&GameSession.Instance!=null&&GameSession.Instance.State.Phase==GamePhase.Playing&&GameSession.Instance.State.SubState==PlayingSubState.Cutscene
                &&(!ServiceRegistry.TryGet<IDialogueService>(out var d)||!d.IsPlaying))GameSession.Instance.State.SetSubState(PlayingSubState.Free);
        }
        private Vector2 ToStage(Vector2 screen)
        {RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,screen,null,out var p);return new Vector2(p.x+960,540-p.y);}
        public Vector2 StageToScreen(Vector2 p)=>RectTransformUtility.WorldToScreenPoint(null,stage.TransformPoint(new Vector3(p.x-960,540-p.y,0)));
        public Vector2 SlotScreenCenter(int slot)=>StageToScreen(SlotCenter(slot));
        public Vector2 PieceScreenCenter(int piece)=>StageToScreen(VisualCenter(piece));
        private Vector2 SlotCenter(int slot)
        {Vector2 uv=PhotoFragmentGraphic.CenterFor(slot);if(boardBack)uv.x=1-uv.x;return PhotoRect.position+Vector2.Scale(uv,PhotoRect.size);}
        private bool InAssembly(int i)=>i==5||state.SlotOf(i)>=0;
        private Vector2 VisualCenter(int i)
        {
            if(i==5)return SlotCenter(3);if(dragging&&pressed==i)return dragCenter;
            if(state.SlotOf(i)>=0)return SlotCenter(state.SlotOf(i));
            return pressed==i||flipPiece==i?ClampPaper(loosePositions[i],i,1):loosePositions[i];
        }
        private Vector2 ClampPaper(Vector2 center,int piece,float scale)
        {
            var size=Vector2.Scale(PhotoFragmentGraphic.BoundsFor(PhotoReconstructionState.Shapes[piece]).size,PhotoRect.size)*scale;
            center.x=Mathf.Clamp(center.x,size.x*.5f+22,1920-size.x*.5f-22);
            center.y=Mathf.Clamp(center.y,size.y*.5f+95,980-size.y*.5f);return center;
        }
        private bool PieceBack(int i)=>InAssembly(i)?boardBack:looseBack[i];
        private Texture2D Source(int i,bool back)=>i<3?(back?originalBack:originalFront):(back?comparisonBack:comparisonFront);
        private int HitPaper(Vector2 screen)
        {
            for(int layer=0;layer<2;layer++)
            {
                int best=-1,order=-1;
                for(int i=0;i<6;i++)
                {
                    if((layer==1)!=InAssembly(i))continue;
                    var g=graphics[i];if(!g.gameObject.activeInHierarchy)continue;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(g.rectTransform,screen,null,out var p);
                    if(g.rectTransform.rect.Contains(p)&&g.Contains(p)&&g.transform.GetSiblingIndex()>order){best=i;order=g.transform.GetSiblingIndex();}
                }
                if(best>=0)return best;
            }
            return -1;
        }
        private void UpdateHover(Vector2 screen)
        {
            if(pressed>=0||dragging){flipHint.enabled=false;return;}
            int hit=HitPaper(screen);flipHint.enabled=hit>=0;if(hit<0)return;
            bool assembled=InAssembly(hit);
            Vector2 center=assembled?new Vector2(PhotoRect.xMax-80,PhotoRect.yMin+30):VisualCenter(hit)+new Vector2(0,-PhotoFragmentGraphic.BoundsFor(PhotoReconstructionState.Shapes[hit]).height*PhotoRect.height*.43f*.5f-24);
            SetRect(flipHint.rectTransform,center,new Vector2(160,38));flipHint.text=assembled?"↶ 整张翻面":"↶ 翻面";
        }
        private void Render()
        {
            if(stage==null||finishing)return;
            if(viewing)
            {
                foreach(var g in graphics)g.gameObject.SetActive(false);
                completedImage.texture=boardBack?originalBack:originalFront;completedImage.gameObject.SetActive(true);
                completedImage.transform.SetAsLastSibling();flipHint.transform.SetAsLastSibling();return;
            }
            for(int i=0;i<6;i++)
            {
                bool assembled=InAssembly(i);var parent=assembled?assemblyRoot:looseRoot;
                if(graphics[i].transform.parent!=parent)graphics[i].transform.SetParent(parent,false);
                int shape=i==5?3:PhotoReconstructionState.Shapes[i];bool reverse=PieceBack(i);
                graphics[i].gameObject.SetActive(true);graphics[i].Set(i==5?(reverse?originalBack:originalFront):Source(i,reverse),shape,reverse);
                float scale=assembled||pressed==i||flipPiece==i?1:.43f;
                Vector2 center=VisualCenter(i),size=Vector2.Scale(PhotoFragmentGraphic.BoundsFor(shape).size,PhotoRect.size)*scale;
                SetRect(graphics[i].rectTransform,center,size);
                if(assembled)graphics[i].rectTransform.anchoredPosition=new Vector2(center.x-PhotoRect.center.x,PhotoRect.center.y-center.y);
                if(i<5)outlines[i].enabled=pressed==i&&dragging;
            }
        }
        private void UpdateCompletedView(Vector2 point,bool down,bool held)
        {
            flipHint.enabled=pressed<0&&PhotoRect.Contains(point);flipHint.text="↶ 翻面";
            SetRect(flipHint.rectTransform,new Vector2(PhotoRect.xMax-80,PhotoRect.yMin+30),new Vector2(160,38));
            if(down)
            {
                if(BackRect.Contains(point)){Close(false);return;}
                if(PhotoRect.Contains(point)){pressed=6;pressPoint=point;pressTime=Time.unscaledTime;}
            }
            if(pressed!=6)return;
            if(held){if(Vector2.Distance(point,pressPoint)>9){pressed=-1;needsRelease=true;}return;}
            bool click=Vector2.Distance(point,pressPoint)<=9&&Time.unscaledTime-pressTime<.38f;pressed=-1;
            if(click)StartFlip(-1);
        }
        private void SetMessage(string text,float duration){if(message!=null){message.text=text;message.enabled=true;messageTimer=duration;}}
        private static void PlayPaper(){if(ServiceRegistry.TryGet<IAudioService>(out var audio))audio.PlaySfx("sfx_cloth_paper");}
        private void BuildView()
        {
            var root=new GameObject("PhotoReconstructionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=55;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var matte=Node<Image>(root.transform,"Matte",new Vector2(960,540),new Vector2(10000,10000));matte.color=new Color(.025f,.02f,.015f,1);matte.raycastTarget=true;
            stage=Node<RectTransform>(root.transform,"Stage",new Vector2(960,540),new Vector2(1920,1080));stage.anchoredPosition=Vector2.zero;
            var bg=Node<RawImage>(stage,"Workbench",new Vector2(960,540),new Vector2(1920,1080));bg.texture=workbench;bg.raycastTarget=true;
            title=Label(stage,"Title","把这张旧照片拼回去",new Vector2(960,58),new Vector2(1500,55),32);
            assemblyRoot=Node<RectTransform>(stage,"AssembledPhoto",PhotoRect.center,PhotoRect.size);
            looseRoot=Node<RectTransform>(stage,"LoosePapers",new Vector2(960,540),new Vector2(1920,1080));
            for(int i=0;i<6;i++)
            {
                graphics[i]=Node<PhotoFragmentGraphic>(looseRoot,"Fragment_"+i,Vector2.zero,Vector2.one);graphics[i].raycastTarget=false;
                var shadow=graphics[i].gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.10f,.065f,.025f,.35f);shadow.effectDistance=new Vector2(3,-4);
                var fibers=graphics[i].gameObject.AddComponent<Outline>();fibers.effectColor=new Color(.94f,.87f,.73f,.65f);fibers.effectDistance=new Vector2(.65f,-.65f);
                if(i<5){outlines[i]=graphics[i].gameObject.AddComponent<Outline>();outlines[i].effectColor=new Color(.98f,.79f,.38f,.7f);outlines[i].effectDistance=new Vector2(1,-1);outlines[i].enabled=false;}
            }
            completedImage=Node<RawImage>(stage,"RestoredPhoto",PhotoRect.center,PhotoRect.size);completedImage.raycastTarget=false;completedImage.uvRect=new Rect(.06f,.06f,.88f,.84f);completedImage.gameObject.SetActive(false);
            message=Label(stage,"ContextHint","",new Vector2(960,1020),new Vector2(1600,48),25);
            Label(stage,"NavigationBack","‹",BackRect.center,BackRect.size,62);
            flipHint=Label(stage,"PaperFlipAffordance","↶ 翻面",Vector2.zero,new Vector2(160,38),22);flipHint.enabled=false;
            root.SetActive(false);
        }
        private T Node<T>(Transform parent,string name,Vector2 center,Vector2 size)where T:Component
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);SetRect(rt,center,size);return typeof(T)==typeof(RectTransform)?rt as T:go.AddComponent<T>();
        }
        private static void SetRect(RectTransform rt,Vector2 center,Vector2 size)
        {rt.sizeDelta=size;rt.anchoredPosition=new Vector2(center.x-960,540-center.y);rt.localScale=Vector3.one;}
        private Text Label(Transform parent,string name,string value,Vector2 center,Vector2 size,int fontSize)
        {
            var t=Node<Text>(parent,name,center,size);t.font=font;t.fontSize=fontSize;t.text=value;t.color=new Color(1,.96f,.86f);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            var shadow=t.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.08f,.04f,.015f,.9f);shadow.effectDistance=new Vector2(1,-2);return t;
        }
    }
}
