using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>Past-only folding rack. The latch, not a hanging rope, supports the deck.</summary>
    public sealed class FoldingDryingRack : MonoBehaviour, IInteractable
    {
        public const string RaisedFlag = "Mechanism_L01B_RackRaised";
        [SerializeField] private TemporalObject rackObject;
        [SerializeField] private Transform leaf;
        [SerializeField] private BoxCollider2D deck;
        [SerializeField] private Transform brace;
        [SerializeField] private Transform braceFoot;
        [SerializeField] private Transform braceEye;
        [SerializeField] private float braceSpriteLength = 1f;
        [SerializeField] private SpriteRenderer latch;
        [SerializeField] private Sprite latchOpen, latchClosed;
        [SerializeField] private LineRenderer liftRope, pullRope;
        [SerializeField] private Transform guide, ropeEye, pullEnd;
        [SerializeField] private AudioClip pullSound, creakSound, lockSound, brokenSound;
        [SerializeField] private float raisingSeconds = 2.4f;
        private LevelRoot room;
        private AudioSource sound;
        private bool raised, operating, ownsControl, creaked;
        private float elapsed;

        public bool IsRaised => raised;
        public bool IsOperating => operating;
        public string InteractId => "L01B_drying_rack";
        private bool IsPast => rackObject != null && rackObject.CurrentEra == Era.Past;
        private bool Saved => GameSession.Instance != null
            && (GameSession.Instance.StoryFlags.HasFlag(RaisedFlag)
                || GameSession.Instance.StoryFlags.HasFlag("HintPassed_L01B_Climb"));
        public string InteractPrompt => IsPast ? "拉绳升起晾晒架" : "检查损坏的晾晒架";
        public bool CanInteract => isActiveAndEnabled && room != null && room.IsActiveRoom
            && !operating && (!IsPast || !raised);

        private void Awake()
        {
            room = GetComponentInParent<LevelRoot>();
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.spatialBlend = 0; sound.volume = .65f;
            RestorePose();
        }
        private void OnEnable()
        {
            if (rackObject != null) rackObject.EraApplied += OnEraApplied;
            RestorePose();
        }
        private void OnDisable()
        {
            if (rackObject != null) rackObject.EraApplied -= OnEraApplied;
            Cancel();
        }
        private void OnEraApplied(TemporalObject obj, Era era) { Cancel(); RestorePose(); }
        private void RestorePose()
        {
            raised = Saved;
            Pose(raised ? 1 : 0);
            if (deck != null) deck.enabled = raised;
        }
        public bool Interact()
        {
            var session = GameSession.Instance;
            if (!CanInteract || session == null || !session.State.IsPlayerControlAllowed) return false;
            if (!ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsGrounded || player.IsClimbingLadder)
            {
                if (ServiceRegistry.TryGet<IHudService>(out var h)) h.ShowHintText("先站到墙边平台，再拉绳。");
                return true;
            }
            if (!IsPast)
            {
                Play(brokenSound);
                if (ServiceRegistry.TryGet<IHudService>(out var h)) h.ShowHintText("绳子断了，架子也坏了。过去还能用。");
                return true;
            }
            operating = ownsControl = true; elapsed = 0; creaked = false;
            session.State.SetSubState(PlayingSubState.Interacting);
            deck.enabled = false;
            Play(pullSound);
            return true;
        }
        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null) return;
            if (!room.IsActiveRoom || session.State.Phase != GamePhase.Playing)
            {
                if (operating) { Cancel(); RestorePose(); }
                return;
            }
            if (session.State.IsPaused) return;
            if (operating && session.State.SubState != PlayingSubState.Interacting) { Cancel(); RestorePose(); return; }
            if (!operating) { if (Saved != raised) RestorePose(); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / raisingSeconds);
            Pose(Mathf.SmoothStep(0, 1, t));
            if (!creaked && t > .2f) { creaked = true; Play(creakSound); }
            if (t < 1) return;
            operating = false; raised = true;
            Pose(1); deck.enabled = true; Physics2D.SyncTransforms();
            Play(lockSound);
            session.StoryFlags.SetFlag(RaisedFlag);
            Release();
            session.SaveProgressNow();
        }
        private void Pose(float t)
        {
            if (leaf == null) return;
            leaf.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-78, 0, t));
            if (latch != null)
            {
                latch.sprite = t >= 1 ? latchClosed : latchOpen;
                // Sprite pivots are registered at each asset's central bolt.
            }
            if (brace != null && braceFoot != null && braceEye != null)
            {
                Vector2 delta = braceEye.position - braceFoot.position;
                brace.position = braceFoot.position;
                brace.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 39.3f);
                float scale = delta.magnitude / Mathf.Max(.01f, braceSpriteLength);
                brace.localScale = new Vector3(scale, scale, 1);
                brace.gameObject.SetActive(t > .72f);
            }
            if (liftRope != null && guide != null && ropeEye != null)
            {
                liftRope.positionCount = 13;
                for (int i = 0; i < 13; i++)
                {
                    float u = i / 12f;
                    Vector3 p = Vector3.Lerp(guide.position, ropeEye.position, u);
                    p.y -= Mathf.Sin(u * Mathf.PI) * Mathf.Lerp(.32f, .025f, t);
                    liftRope.SetPosition(i, p);
                }
            }
            if (pullRope != null && guide != null && pullEnd != null)
            {
                pullRope.positionCount = 2;
                pullRope.SetPosition(0, guide.position);
                pullRope.SetPosition(1, pullEnd.position + Vector3.down * (.22f * Mathf.Sin(t * Mathf.PI)));
            }
        }
        private void Play(AudioClip clip) { if (sound != null && clip != null) sound.PlayOneShot(clip); }
        private void Release()
        {
            if (ownsControl && GameSession.Instance != null && GameSession.Instance.State.SubState == PlayingSubState.Interacting)
                GameSession.Instance.State.SetSubState(PlayingSubState.Free);
            ownsControl = false;
        }
        private void Cancel() { operating = false; if (sound != null) sound.Stop(); Release(); }
    }
}
