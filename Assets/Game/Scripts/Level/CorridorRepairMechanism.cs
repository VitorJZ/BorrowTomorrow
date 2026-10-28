using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace BorrowTomorrow.Level
{
    [DefaultExecutionOrder(40)]
    public sealed class CorridorRepairMechanism : MonoBehaviour, ITemporalDependencyGuard
    {
        public const string ExpandedFlag="Mechanism_L03_InspectionExpanded_v2";
        public const string PlacedFlag="Mechanism_L03_BoardLowered_v2";
        public TemporalObject floor,walkway;
        public Transform plank,tip,lever,grip,shoe;
        public Transform[] modules;
        public BoxCollider2D plankDeck,metalDeck;
        public LineRenderer linkage;
        public Font labelFont;
        public AudioClip creakSound,slideSound,lockSound;
        private LevelRoot room;
        private IInputSource input;
        private AudioSource audioSource;
        private enum DragMode {None,Lever,Plank}
        private DragMode mode;
        private bool ownsControl,needRelease,releaseAfterPause;
        private float extension,angle=110,bounce,sayAfter;
        private int hint;
        private GUIStyle label;
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        private IPointerInputSource Pointer=>input as IPointerInputSource;
        private Vector2 PointerPosition=>Pointer!=null?Pointer.PointerPosition:(Vector2)Input.mousePosition;
        private bool Pressed=>Pointer!=null?Pointer.PrimaryPressedThisFrame:Input.GetMouseButtonDown(0);
        private bool Held=>Pointer!=null?Pointer.PrimaryHeld:Input.GetMouseButton(0);
        private bool Cancel=>Pointer!=null?Pointer.SecondaryPressedThisFrame:Input.GetMouseButtonDown(1);
        private bool Flag(string id)=>GameSession.Instance!=null&&GameSession.Instance.StoryFlags.HasFlag(id);
        public bool Expanded=>Flag(ExpandedFlag);
        public bool Placed=>Flag(PlacedFlag);
        public bool Combination=>floor.CurrentEra==Era.Past&&walkway.CurrentEra==Era.Present;
        public bool Supported=>Combination&&Expanded&&Placed;
        public Vector3 GripPosition=>grip.position;
        private void Awake()
        {
            room=GetComponentInParent<LevelRoot>();audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.volume=.55f;
        }
        private void OnEnable(){floor.EraApplied+=Changed;walkway.EraApplied+=Changed;}
        private void OnDisable(){floor.EraApplied-=Changed;walkway.EraApplied-=Changed;StopDrag();}
        private void OnApplicationFocus(bool value){if(!value){StopDrag();needRelease=true;}}
        private void Changed(TemporalObject obj,Era era){StopDrag();extension=Expanded?1:0;angle=Supported?0:110;bounce=0;Paint();}
        private bool Ready(out IPlayerContext player)
        {
            player=null;var s=GameSession.Instance;
            return room!=null&&room.IsActiveRoom&&s!=null&&s.State.Phase==GamePhase.Playing&&!s.State.IsPaused
                &&s.State.SubState==PlayingSubState.Free&&ServiceRegistry.TryGet(out player)&&player.IsPresent&&player.IsGrounded&&!player.IsClimbingLadder;
        }
        private void Say(string text){if(Time.unscaledTime<sayAfter)return;sayAfter=Time.unscaledTime+1.5f;if(ServiceRegistry.TryGet<IHudService>(out var hud))hud.ShowBoundaryBlocked(text);}
        private void Sound(AudioClip clip){if(clip!=null)audioSource.PlayOneShot(clip);}
        private bool OverUI()
        {
            if(EventSystem.current==null)return false;
            hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=PointerPosition},hits);return hits.Count>0;
        }
        private bool Near(Vector3 point,float radius=.4f)
        {
            if(Camera.main==null)return false;
            Vector2 a=Camera.main.WorldToScreenPoint(point),b=Camera.main.WorldToScreenPoint(point+Vector3.right*radius);
            return Vector2.Distance(PointerPosition,a)<Mathf.Max(16,Vector2.Distance(a,b));
        }
        private bool OverPlank()
        {
            if(Camera.main==null)return false;
            Vector2 a=Camera.main.WorldToScreenPoint(plank.position),b=Camera.main.WorldToScreenPoint(tip.position),axis=b-a;
            float t=axis.sqrMagnitude>.01f?Mathf.Clamp01(Vector2.Dot(PointerPosition-a,axis)/axis.sqrMagnitude):0;
            return Vector2.Distance(PointerPosition,Vector2.Lerp(a,b,t))<16;
        }
        private void Begin(DragMode drag)
        {
            mode=drag;ownsControl=true;GameSession.Instance.State.SetSubState(PlayingSubState.Interacting);
            if(ServiceRegistry.TryGet<ICameraRig>(out var camera))camera.SetFollowFrozen(true);
            Sound(drag==DragMode.Lever?slideSound:creakSound);
        }
        private void StopDrag()
        {
            mode=DragMode.None;
            if(ownsControl&&GameSession.Instance!=null&&GameSession.Instance.State.IsPaused){releaseAfterPause=true;return;}
            if(ownsControl&&GameSession.Instance!=null&&GameSession.Instance.State.SubState==PlayingSubState.Interacting)GameSession.Instance.State.SetSubState(PlayingSubState.Free);
            if(ownsControl&&ServiceRegistry.TryGet<ICameraRig>(out var camera))camera.SetFollowFrozen(false);
            ownsControl=false;releaseAfterPause=false;
        }
        private void Commit(string id){GameSession.Instance.StoryFlags.SetFlag(id);hint=0;Sound(lockSound);StopDrag();needRelease=true;Paint();Physics2D.SyncTransforms();GameSession.Instance.SaveProgressNow();}
        private Bounds FlatBoardBounds=>new Bounds(plank.position+new Vector3(2.75f,0,0),new Vector3(5.5f,.24f,.1f));
        private void Update()
        {
            if(input==null)ServiceRegistry.TryGet(out input);
            var s=GameSession.Instance;if(s==null||room==null)return;
            if(!room.IsActiveRoom||s.State.Phase!=GamePhase.Playing){StopDrag();return;}
            if(s.State.IsPaused){if(mode!=DragMode.None&&!Held)needRelease=true;return;}
            if(releaseAfterPause)StopDrag();
            if(mode!=DragMode.None&&s.State.SubState!=PlayingSubState.Interacting)StopDrag();
            if(Flag("IdentityKnown")&&!Expanded){s.StoryFlags.SetFlag(ExpandedFlag);s.StoryFlags.SetFlag(PlacedFlag);}
            if((Pointer!=null?Pointer.HintPressedThisFrame:Input.GetKeyDown(KeyCode.H))&&s.State.SubState==PlayingSubState.Free)
            {
                string[] steps=!Expanded?new[]{"看看木板右边少了什么。","现在的地板下面，露出了维修设施。","从左侧梯口下去，拉动把手展开检修架。"}
                    :!Placed?new[]{"展开的接托，可以接住旧木板。","保留过去木板和现在支架。","把边界停在木板和支架之间，再拖下木板。"}
                    :new[]{"先走到不会变化的石门槛。","前面的砖墙在过去还没塌。","站稳后，把边界推到砖墙右边，再去看照片。"};
                sayAfter=0;Say(steps[Mathf.Min(hint++,2)]);
            }
            if(needRelease&&!Held)needRelease=false;
            Vector3 mouse=Camera.main!=null?Camera.main.ScreenToWorldPoint(PointerPosition):Vector3.zero;mouse.z=0;
            if(mode==DragMode.None&&!needRelease&&Pressed&&Ready(out var player)&&!OverUI())
            {
                if(walkway.CurrentEra==Era.Present&&!Expanded&&Near(grip.position,.55f))
                {
                    if(player.Position.y>1.2f||Vector2.Distance(player.Position,lever.position)>2.1f)Say("得先下到检修台旁边。");
                    else Begin(DragMode.Lever);
                }
                else if(floor.CurrentEra==Era.Past&&!Supported&&OverPlank())Begin(DragMode.Plank);
            }
            if(mode!=DragMode.None)
            {
                if(Cancel){StopDrag();needRelease=true;}
                else if(mode==DragMode.Lever)
                {
                    Vector3 d=mouse-lever.position;
                    float rotation=Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg-90;
                    extension=Mathf.InverseLerp(-20,-100,rotation);
                    if(extension>=.94f){extension=1;Commit(ExpandedFlag);}
                    else if(!Held)StopDrag();
                }
                else if(mode==DragMode.Plank)
                {
                    Vector3 d=mouse-plank.position;angle=Mathf.Clamp(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,0,110);
                    if(angle<=10)
                    {
                        if(Expanded&&Combination)
                        {
                            if(ServiceRegistry.TryGet<IPlayerContext>(out var p)&&FlatBoardBounds.Intersects(p.BodyBounds))Say("先离开木板落下的位置。");
                            else{angle=0;Commit(PlacedFlag);}
                        }
                        else{Say("右边没托住，木板放不稳。");Sound(creakSound);bounce=.7f;StopDrag();needRelease=true;}
                    }
                    if(!Held)StopDrag();
                }
            }
            if(mode==DragMode.None)
            {
                extension=Expanded?1:Mathf.MoveTowards(extension,0,Time.deltaTime*1.6f);
                if(bounce>0){bounce=Mathf.Max(0,bounce-Time.deltaTime);float t=1-bounce/.7f;angle=t<.25f?Mathf.Lerp(0,-8,t/.25f):Mathf.Lerp(-8,110,Mathf.SmoothStep(0,1,(t-.25f)/.75f));}
                else angle=Supported?0:Mathf.MoveTowards(angle,110,Time.deltaTime*180);
            }
            Paint();
        }
        private void Paint()
        {
            if(plank==null)return;
            plank.rotation=Quaternion.Euler(0,0,Supported?0:angle);
            plankDeck.enabled=Supported;
            metalDeck.enabled=Expanded&&walkway.CurrentEra==Era.Present;
            lever.rotation=Quaternion.Euler(0,0,Mathf.Lerp(-20,-100,extension));
            const float width=8f/3;
            for(int i=0;i<modules.Length;i++)modules[i].position=new Vector3(Mathf.Lerp(18-width*.5f,10+width*(i+.5f),extension),2.82f+(1-extension)*i*.055f,0);
            shoe.position=new Vector3(Mathf.Lerp(15.33f,10.12f,extension),2.55f,0);
            if(linkage!=null){linkage.positionCount=3;linkage.SetPosition(0,lever.position+new Vector3(.1f,0,0));linkage.SetPosition(1,new Vector3(8.75f,1.3f,0));linkage.SetPosition(2,shoe.position+new Vector3(.15f,-.1f,0));}
        }
        private void OnGUI()
        {
            if(mode!=DragMode.None||!Ready(out var player)||Camera.main==null)return;
            string text=null;Vector3 point=Vector3.zero;
            if(!Expanded&&walkway.CurrentEra==Era.Present&&(Near(grip.position,.7f)||Vector2.Distance(player.Position,lever.position)<2)){text="拖动把手";point=grip.position;}
            else if(!Supported&&floor.CurrentEra==Era.Past&&OverPlank()){text="拖下木板";point=Vector3.Lerp(plank.position,tip.position,.35f);}
            if(text==null)return;
            if(label==null){label=new GUIStyle(GUI.skin.label){font=labelFont,fontSize=19,alignment=TextAnchor.MiddleLeft};label.normal.textColor=new Color(1,.93f,.75f);}
            Vector3 screen=Camera.main.WorldToScreenPoint(point);GUI.Label(new Rect(screen.x+20,Screen.height-screen.y-16,170,36),text,label);
        }
        public bool AllowsChange(string[] ids,Era target,IPlayerContext player)
        {
            bool past=floor.CurrentEra==Era.Past,present=walkway.CurrentEra==Era.Present;
            foreach(var id in ids){if(id==floor.ObjectId)past=target==Era.Past;if(id==walkway.ObjectId)present=target==Era.Present;}
            bool next=past&&present&&Expanded&&Placed;
            if(plankDeck.enabled&&!next&&player.IsGrounded&&player.GroundCollider==plankDeck)return false;
            if(metalDeck.enabled&&!present&&player.IsGrounded&&player.GroundCollider==metalDeck)return false;
            if(next&&!plankDeck.enabled&&FlatBoardBounds.Intersects(player.BodyBounds))return false;
            return true;
        }
    }
}
