using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    // The original pre-contest loose-floor idea, confined to the old deck's end.
    // No maintenance pit, lever, lower route or new character animation.
    public sealed class CorridorLooseFloor : MonoBehaviour,IInteractable,ITemporalDependencyGuard
    {
        public const string InspectedFlag="Mechanism_L03_FloorInspected_v12";
        public TemporalObject floor;
        public Transform hinge;
        public BoxCollider2D looseDeck,supportedDeck;
        public GameObject pastGap,pastEdges;
        public AudioClip hollowSound,creakSound,solidSound,breakFallSound;
        public float warningSeconds=.35f,fallSeconds=.65f,fallDistance=3.8f;
        public enum PanelPhase{Intact,Warning,Falling,Collapsed}
        public PanelPhase Phase{get;private set;}
        public float PanelAngle=>hinge!=null?Mathf.DeltaAngle(0,hinge.eulerAngles.z):0;
        private LevelRoot room;
        private IInputSource input;
        private AudioSource audioSource;
        private ParticleSystem dust;
        private Material dustMaterial;
        private GameStateMachine state;
        private float timer,soundCooldown;
        private bool creaked,wasOnSupported,audioPaused;
        private Vector3 hingeHome;
        public float PanelDrop=>hinge!=null?hingeHome.y-hinge.localPosition.y:0;
        private Bounds closedBounds;
        public string InteractId=>"L03_loose_floor_probe";
        public string InteractPrompt=>"试探裂纹木板";
        public bool CanInteract
        {
            get
            {
                if(!Ready(out var p)||floor.CurrentEra!=Era.Past||Phase!=PanelPhase.Intact)return false;
                return p.IsGrounded&&p.Position.x>=8.4f-1.3f&&p.Position.x<=8.4f-.05f;
            }
        }
        private void Awake()
        {
            room=GetComponentInParent<LevelRoot>();
            if(hinge!=null)hingeHome=hinge.localPosition;
            closedBounds=new Bounds(new Vector3(9.2f,2.86f,0),new Vector3(1.6f,.28f,.3f));
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.loop=false;audioSource.spatialBlend=0;audioSource.volume=.55f;
            MakeDust();
        }
        private void OnEnable(){if(floor!=null)floor.EraApplied+=EraChanged;}
        private void Start()
        {
            ServiceRegistry.TryGet(out input);
            if(GameSession.Instance!=null){state=GameSession.Instance.State;state.SubStateChanged+=SubStateChanged;}
            ResetPanel();
        }
        private void OnDisable(){if(floor!=null)floor.EraApplied-=EraChanged;if(audioSource!=null)audioSource.Stop();}
        private void OnDestroy(){if(state!=null)state.SubStateChanged-=SubStateChanged;if(dustMaterial!=null)Destroy(dustMaterial);}
        private void SubStateChanged(PlayingSubState before,PlayingSubState after){if(after==PlayingSubState.Respawning)ResetPanel();}
        private void EraChanged(TemporalObject obj,Era era){ResetPanel();}
        private bool Ready(out IPlayerContext player)
        {
            player=null;var s=GameSession.Instance;
            return isActiveAndEnabled&&room!=null&&room.IsActiveRoom&&s!=null&&s.State.Phase==GamePhase.Playing
                &&s.State.SubState==PlayingSubState.Free&&!s.State.IsPaused&&ServiceRegistry.TryGet(out player)&&player.IsPresent;
        }
        public bool Interact()
        {
            if(!CanInteract)return false;
            BeginWarning(true);return true;
        }
        private void BeginWarning(bool probe)
        {
            if(Phase!=PanelPhase.Intact)return;
            Phase=PanelPhase.Warning;timer=0;creaked=false;
            if(probe&&hollowSound!=null)audioSource.PlayOneShot(hollowSound,.85f);
            if(probe&&ServiceRegistry.TryGet<ICharacterArt>(out var art))art.PlayGesture("Interact");
            GameSession.Instance?.StoryFlags.SetFlag(InspectedFlag);
        }
        private void Update()
        {
            var session=GameSession.Instance;
            if(session!=null&&session.State.IsPaused)
            {if(!audioPaused&&audioSource!=null){audioSource.Pause();audioPaused=true;}return;}
            if(audioPaused&&audioSource!=null){audioSource.UnPause();audioPaused=false;}
            if(session==null||room==null||!room.IsActiveRoom||session.State.Phase!=GamePhase.Playing)return;
            if(floor.CurrentEra!=Era.Past)return;
            if(!Ready(out var player))return;
            if(input==null)ServiceRegistry.TryGet(out input);
            var pointer=input as IPointerInputSource;
            bool click=pointer!=null?pointer.PrimaryPressedThisFrame:Input.GetMouseButtonDown(0);
            Vector2 screen=pointer!=null?pointer.PointerPosition:(Vector2)Input.mousePosition;
            if(click&&CanInteract&&Camera.main!=null)
            {
                Vector3 world=Camera.main.ScreenToWorldPoint(screen);
                if(world.x>=8.15f&&world.x<=10.2f&&world.y>=2.55f&&world.y<=3.45f)Interact();
            }
            bool onSupported=player.IsGrounded&&player.GroundCollider==supportedDeck;
            if(onSupported&&!wasOnSupported&&Time.unscaledTime>soundCooldown&&solidSound!=null)
            {audioSource.PlayOneShot(solidSound,.35f);soundCooldown=Time.unscaledTime+.5f;}
            wasOnSupported=onSupported;
            if(Phase==PanelPhase.Intact&&player.IsGrounded&&player.GroundCollider==looseDeck)BeginWarning(false);
            if(Phase==PanelPhase.Warning)
            {
                timer+=Time.deltaTime;
                if(!creaked&&timer>=.08f){creaked=true;if(creakSound!=null)audioSource.PlayOneShot(creakSound,.85f);}
                float t=Mathf.Clamp01(timer/warningSeconds);
                hinge.localRotation=Quaternion.Euler(0,0,-2.5f*t-2*Mathf.Abs(Mathf.Sin(t*Mathf.PI*3)));
                if(t>=1)
                {
                    Phase=PanelPhase.Falling;timer=0;looseDeck.enabled=false;
                    // The board detaches completely. It must not stay hanging from its old hinge.
                    hinge.localRotation=Quaternion.identity;
                    pastGap.SetActive(true);pastEdges.SetActive(true);
                    if(breakFallSound!=null)audioSource.PlayOneShot(breakFallSound,.9f);
                    if(dust!=null)dust.Emit(16);
                }
            }
            else if(Phase==PanelPhase.Falling)
            {
                timer+=Time.deltaTime;float t=Mathf.Clamp01(timer/fallSeconds);
                hinge.localPosition=hingeHome+Vector3.down*(fallDistance*t*t);
                if(t>=1){Phase=PanelPhase.Collapsed;timer=0;hinge.gameObject.SetActive(false);}
            }
        }
        private void ResetPanel()
        {
            Phase=PanelPhase.Intact;timer=0;creaked=wasOnSupported=audioPaused=false;
            if(audioSource!=null)audioSource.Stop();
            if(hinge!=null){hinge.localPosition=hingeHome;hinge.localRotation=Quaternion.identity;hinge.gameObject.SetActive(true);}
            if(looseDeck!=null)looseDeck.enabled=true;
            if(pastGap!=null)pastGap.SetActive(false);if(pastEdges!=null)pastEdges.SetActive(false);
            if(dust!=null)dust.Clear();
        }
        public bool AllowsChange(string[] ids,Era target,IPlayerContext player)
        {
            bool changes=false;foreach(string id in ids)if(id==floor.ObjectId)changes=true;
            if(!changes||target!=Era.Past)return true;
            // A collapsed collider is disabled. Protect its restored flat position explicitly,
            // rather than letting the generic target-collider query miss it.
            Bounds body=player.BodyBounds;body.Expand(new Vector3(-.025f,-.025f,0));
            return !closedBounds.Intersects(body);
        }
        private void MakeDust()
        {
            var go=new GameObject("LooseFloorDust");go.transform.SetParent(transform,false);go.transform.position=new Vector3(9.1f,2.96f,0);
            dust=go.AddComponent<ParticleSystem>();dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=dust.main;main.playOnAwake=false;main.loop=false;main.startLifetime=.5f;main.startSpeed=.35f;main.startSize=.035f;main.startColor=new Color(.64f,.49f,.31f,.6f);main.gravityModifier=.4f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=24;
            var emission=dust.emission;emission.enabled=false;var shape=dust.shape;shape.enabled=false;
            var renderer=dust.GetComponent<ParticleSystemRenderer>();renderer.sortingLayerName="EraPlatform";renderer.sortingOrder=6;
            dustMaterial=new Material(Shader.Find("Sprites/Default"));renderer.sharedMaterial=dustMaterial;
        }
    }
}
