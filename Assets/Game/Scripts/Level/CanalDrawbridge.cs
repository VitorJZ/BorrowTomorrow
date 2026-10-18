using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>Two-era canal winch. The past bridge must be lowered by hand;
    /// the damaged present mechanism can only rattle. Completion survives rewind.</summary>
    public sealed class CanalDrawbridge : MonoBehaviour, IInteractable
    {
        public const string LoweredFlag = "Mechanism_L01A_BridgeLowered";
        private const string PassedFlag = "HintPassed_L01A_Canal";
        [SerializeField] private TemporalObject bridgeObject;
        [SerializeField] private Transform bridgePivot;
        [SerializeField] private BoxCollider2D deckCollider;
        [SerializeField] private Transform pastCrank;
        [SerializeField] private Transform presentCrank;
        [SerializeField] private LineRenderer pastRope;
        [SerializeField] private LineRenderer presentRope;
        [SerializeField] private Transform ropeGuide;
        [SerializeField] private Transform ropeEye;
        [SerializeField] private AudioClip turnLoop;
        [SerializeField] private AudioClip ropeTension;
        [SerializeField] private AudioClip bridgeLower;
        [SerializeField] private AudioClip bridgeSettle;
        [SerializeField] private AudioClip jammed;
        [SerializeField] private float loweringSeconds = 3.2f;

        private LevelRoot room;
        private AudioSource turning;
        private AudioSource feedback;
        private ParticleSystem rust;
        private Material rustMaterial;
        private bool lowered, lowering, jamming, ownsInteraction, creakPlayed;
        private float elapsed;

        public string InteractId => "L01A_canal_winch";
        private bool IsPast => bridgeObject != null && bridgeObject.CurrentEra == Era.Past;
        public string InteractPrompt => IsPast ? "转动摇柄，放下木桥" : "尝试转动损坏的摇柄";
        public bool CanInteract => isActiveAndEnabled && room != null && room.IsActiveRoom
            && !lowering && !jamming && (!IsPast || !lowered);

        private bool SavedLowered => GameSession.Instance != null
            && (GameSession.Instance.StoryFlags.HasFlag(LoweredFlag)
                || GameSession.Instance.StoryFlags.HasFlag(PassedFlag)); // Older saves already past this puzzle.

        private void Awake()
        {
            room = GetComponentInParent<LevelRoot>();
            turning = gameObject.AddComponent<AudioSource>();
            feedback = gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { turning, feedback })
            { source.playOnAwake = false; source.spatialBlend = 0f; source.volume = .65f; }
            turning.loop = true; turning.clip = turnLoop;
            CreateRust();
            ApplyRestPose();
        }

        private void OnEnable()
        {
            if (bridgeObject != null) bridgeObject.EraApplied += OnEraApplied;
            ApplyRestPose();
        }

        private void OnDisable()
        {
            if (bridgeObject != null) bridgeObject.EraApplied -= OnEraApplied;
            CancelOperation();
        }

        private void OnDestroy()
        {
            if (rustMaterial != null) Destroy(rustMaterial);
        }

        private void OnEraApplied(TemporalObject obj, Era era)
        {
            // Includes checkpoint restore: restore the persistent bridge state
            // before the player is placed, without replaying sound or motion.
            CancelOperation();
            ApplyRestPose();
        }

        private void ApplyRestPose()
        {
            lowered = SavedLowered;
            if (bridgePivot != null) bridgePivot.localRotation = Quaternion.Euler(0, 0, lowered ? 0 : 88);
            if (deckCollider != null) deckCollider.enabled = lowered;
            if (pastCrank != null) pastCrank.localRotation = Quaternion.identity;
            if (presentCrank != null) presentCrank.localRotation = Quaternion.identity;
            DrawRopes();
        }

        public bool Interact()
        {
            var session = GameSession.Instance;
            if (!CanInteract || session == null || !session.State.IsPlayerControlAllowed) return false;
            if (!ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsGrounded || player.IsClimbingLadder)
            {
                if (ServiceRegistry.TryGet<IHudService>(out var hud)) hud.ShowHintText("先站稳，再操作摇柄。");
                return true;
            }
            session.State.SetSubState(PlayingSubState.Interacting);
            ownsInteraction = true;
            elapsed = 0;
            if (IsPast)
            {
                lowering = true; creakPlayed = false;
                deckCollider.enabled = false;
                turning.Play();
                Play(ropeTension);
            }
            else
            {
                jamming = true;
                Play(jammed);
                if (rust != null) rust.Emit(14);
                if (ServiceRegistry.TryGet<IHudService>(out var hud))
                    hud.ShowHintText("握把断了，转轴也卡死了。");
            }
            return true;
        }

        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null) return;
            if (!ServiceRegistry.TryGet<ILevelRoot>(out var active) || !ReferenceEquals(active, room)
                || session.State.Phase != GamePhase.Playing)
            {
                if (lowering || jamming) { CancelOperation(); ApplyRestPose(); }
                return;
            }
            if (session.State.IsPaused) return;
            if ((lowering || jamming) && session.State.SubState != PlayingSubState.Interacting)
            { CancelOperation(); ApplyRestPose(); return; }
            if (!lowering && !jamming)
            {
                if (SavedLowered != lowered) ApplyRestPose();
                return;
            }
            elapsed += Time.deltaTime;
            if (jamming)
            {
                presentCrank.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 30) * 4 * Mathf.Clamp01(1-elapsed/.65f));
                if (elapsed >= .65f)
                { jamming = false; presentCrank.localRotation = Quaternion.identity; ReleaseControl(); }
                return;
            }
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.5f, loweringSeconds));
            bridgePivot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(88, 0, Mathf.SmoothStep(0, 1, t)));
            pastCrank.localRotation = Quaternion.Euler(0, 0, -720 * t);
            if (!creakPlayed && t >= .35f) { creakPlayed = true; Play(bridgeLower); }
            DrawRopes();
            if (t >= 1)
            {
                lowering = false; lowered = true;
                bridgePivot.localRotation = Quaternion.identity;
                pastCrank.localRotation = Quaternion.identity;
                deckCollider.enabled = true;
                Physics2D.SyncTransforms();
                turning.Stop(); Play(bridgeSettle);
                session.StoryFlags.SetFlag(LoweredFlag);
                ReleaseControl();
            }
        }

        private void DrawRopes()
        {
            if (pastRope != null && ropeGuide != null && ropeEye != null)
            {
                Vector3 start = ropeGuide.position, end = ropeEye.position;
                pastRope.positionCount = 9;
                for (int i=0;i<9;i++)
                {
                    float u=i/8f;
                    Vector3 p=Vector3.Lerp(start,end,u);
                    p.y-=Mathf.Sin(u*Mathf.PI)*(lowered?.10f:.025f);
                    pastRope.SetPosition(i,p);
                }
            }
            if (presentRope != null)
            {
                // The upper pulley is missing. Only a short broken rope remains.
                presentRope.positionCount=3;
                presentRope.SetPosition(0,new Vector3(9.12f,1.55f,0));
                presentRope.SetPosition(1,new Vector3(9.20f,1.29f,0));
                presentRope.SetPosition(2,new Vector3(9.10f,1.10f,0));
            }
        }

        private void Play(AudioClip clip) { if (clip != null && feedback != null) feedback.PlayOneShot(clip); }
        private void ReleaseControl()
        {
            if (ownsInteraction && GameSession.Instance != null
                && GameSession.Instance.State.SubState == PlayingSubState.Interacting)
                GameSession.Instance.State.SetSubState(PlayingSubState.Free);
            ownsInteraction=false;
        }
        private void CancelOperation()
        {
            lowering=false; jamming=false;
            if(turning!=null)turning.Stop();
            if(feedback!=null)feedback.Stop();
            ReleaseControl();
        }

        private void CreateRust()
        {
            var go=new GameObject("RustFlakes");go.transform.SetParent(transform,false);
            go.transform.position=new Vector3(8.78f,.96f,0);
            rust=go.AddComponent<ParticleSystem>();rust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=rust.main;main.playOnAwake=false;main.loop=false;main.duration=.6f;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.7f);
            main.startSize=new ParticleSystem.MinMaxCurve(.018f,.045f);
            main.startSpeed=.35f;main.gravityModifier=.55f;
            main.startColor=new Color(.45f,.22f,.09f);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=rust.emission;emission.enabled=false;
            var shape=rust.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.04f;
            var renderer=go.GetComponent<ParticleSystemRenderer>();
            rustMaterial=new Material(Shader.Find("Sprites/Default"));rustMaterial.mainTexture=Texture2D.whiteTexture;
            renderer.sharedMaterial=rustMaterial;renderer.sortingLayerName="EraPlatform";renderer.sortingOrder=12;
        }
    }
}
