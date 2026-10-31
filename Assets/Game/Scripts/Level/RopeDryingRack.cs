using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BorrowTomorrow.Level
{
    /// One continuous rope, with a fixed material length after routing over the fork.
    /// Save only stable puzzle steps; the travelling leaf is never a walking surface.
    public sealed class RopeDryingRack : MonoBehaviour, IInteractable, ITemporalDependencyGuard
    {
        public const string ReleasedFlag = "Mechanism_L01B_RopeReleased_v7";
        public const string RoutedFlag = "Mechanism_L01B_RopeRouted_v7";
        public const string SecuredFlag = "Mechanism_L01B_RopeSecured_v7";
        public TemporalObject rack, tree;
        public Transform leaf, eye, fork, hook, rest, knot;
        public Transform poleStart, poleEnd;
        public BoxCollider2D deck;
        public SpriteRenderer knotImage, tailImage, hookImage;
        public Sprite tiedKnot, releasedKnot;
        public LineRenderer rope;
        public LineRenderer ropeOutline, treeWrapBack, treeWrapFront, tiedTail, cleatWrap;
        public SpriteRenderer slackCoil, forkImage;
        public SpriteRenderer youngTreeImage;
        public Sprite youngTreeTied, youngTreeFree;
        public Transform tiedLead;
        public AudioClip pullSound, lockSound, creakSound, brokenSound;
        public float foldedAngle = -22f;
        private LevelRoot room;
        private AudioSource audioSource;
        private bool dragging, ownsControl;
        private float lift, unlockTime, hintTime;
        private Vector3 tail, freeEnd, dragPointerOffset;
        private Vector3 guideContact, routeSlideFrom;
        private float routeSlideRemaining, tailLabelUntil;
        private bool hookAttempted;
        private const float RouteSlideSeconds = .65f;
        private Vector3 VisualGuide => routeSlideRemaining > 0 ? guideContact : fork.position;
        private bool wasReleased, wasRouted, wasSecured;
        private IInputSource input;
        private bool needNewPress;
        private bool releaseAfterPause;
        private GUIStyle ropeLabelStyle, ropeLabelShadow;
        public Font labelFont;
        private int hintLevel;
        private Vector3 knotScale, tailScale;
        private IPointerInputSource Pointer => input as IPointerInputSource;
        private Vector2 PointerPosition => Pointer != null ? Pointer.PointerPosition : (Vector2)Input.mousePosition;
        private bool PrimaryPressed => Pointer != null ? Pointer.PrimaryPressedThisFrame : Input.GetMouseButtonDown(0);
        private bool PrimaryHeld => Pointer != null ? Pointer.PrimaryHeld : Input.GetMouseButton(0);
        private bool SecondaryPressed => Pointer != null ? Pointer.SecondaryPressedThisFrame : Input.GetMouseButtonDown(1);
        private bool HintPressed => Pointer != null ? Pointer.HintPressedThisFrame : Input.GetKeyDown(KeyCode.H);
        private readonly System.Collections.Generic.List<RaycastResult> uiHits = new System.Collections.Generic.List<RaycastResult>();

        private bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = PointerPosition };
            uiHits.Clear(); EventSystem.current.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }

        public bool Released => Flag(ReleasedFlag);
        public bool Routed => Flag(RoutedFlag);
        public bool Secured => Flag(SecuredFlag);
        public bool Combination => rack.CurrentEra == Era.Past && tree.CurrentEra == Era.Present;
        public bool SupportValid => Combination && Released && Routed && Secured;
        public string InteractId => "L01B_rope_v7";
        public string InteractPrompt => string.Empty;
        public bool CanInteract => false; // The nearby knot owns E; this root is not an extra interactable.
        public bool CanInspectHook => !Released && rack.CurrentEra == Era.Past && Ready(out _);
        public bool TryInspectHook()
        {
            if (!CanInspectHook) return false;
            hintTime = 0;
            Say("找根绳子，或许能把这段木梯吊起来。");
            return true;
        }
        public bool CanReleaseKnot => !Released && tree.CurrentEra == Era.Past && Ready(out _);
        private bool Flag(string id) => GameSession.Instance != null && GameSession.Instance.StoryFlags.HasFlag(id);
        private void Mark(string id) { GameSession.Instance.StoryFlags.SetFlag(id); }
        private void Awake()
        {
            room = GetComponentInParent<LevelRoot>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.volume = .6f;
            freeEnd = rest.position; tail = rest.position;
            knotScale = knotImage.transform.localScale; tailScale = tailImage.transform.localScale;
        }
        private void OnEnable()
        {
            rack.EraApplied += EraChanged; tree.EraApplied += EraChanged;
        }
        private void OnDisable()
        {
            rack.EraApplied -= EraChanged; tree.EraApplied -= EraChanged;
            StopDrag();
        }
        private void OnApplicationFocus(bool focus) { if (!focus) { StopDrag(); needNewPress = true; } }
        private void EraChanged(TemporalObject obj, Era era)
        {
            routeSlideRemaining=0; guideContact=fork.position;
            StopDrag(); lift = SupportValid ? 1 : 0;
            freeEnd = rest.position;
            tail = Routed && Combination ? (Secured ? hook.position : HangingEnd) : rest.position;
            Paint();
        }
        private Vector3 EyeAt(float amount)
        {
            return leaf.position + Quaternion.Euler(0, 0, Mathf.Lerp(foldedAngle, 0, amount)) * eye.localPosition;
        }
        private float RopeLength => Vector3.Distance(EyeAt(1), fork.position) + Vector3.Distance(fork.position, hook.position);
        private Vector3 HangingEnd => fork.position + Vector3.down * (RopeLength - Vector3.Distance(EyeAt(0), fork.position));
        private bool Ready(out IPlayerContext player)
        {
            player = null;
            return room != null && room.IsActiveRoom && GameSession.Instance != null
                && GameSession.Instance.State.Phase == GamePhase.Playing && !GameSession.Instance.State.IsPaused
                && GameSession.Instance.State.SubState == PlayingSubState.Free
                && ServiceRegistry.TryGet(out player) && player.IsPresent && player.IsGrounded && !player.IsClimbingLadder;
        }
        
        private void Say(string message)
        {
            if (Time.unscaledTime < hintTime) return;
            hintTime = Time.unscaledTime + 1.2f;
            if (ServiceRegistry.TryGet<IHudService>(out var h)) h.ShowBoundaryBlocked(message);
        }
        private void Play(AudioClip clip) { if (clip != null) audioSource.PlayOneShot(clip); }
        // Screen-space padding keeps the visible rope usable in the fixed whole-room view.
        // Clicking the rope is direct manipulation; avatar distance is not an extra prerequisite.
        private bool OverKnot(Vector2 screen)
        {
            return knot != null && OverPoint(screen, knot.position);
        }
        private bool OverPoint(Vector2 screen, Vector3 point)
        {
            if (Camera.main == null) return false;
            Vector2 centre = Camera.main.WorldToScreenPoint(point);
            Vector2 extent = Camera.main.WorldToScreenPoint(point + Vector3.right * .65f);
            return Vector2.Distance(screen, centre) <= Mathf.Max(22f, Vector2.Distance(centre, extent));
        }
        private Rect TailLabelRect()
        {
            Vector3 p = Camera.main.WorldToScreenPoint(tailImage.transform.position);
            return new Rect(Mathf.Clamp(p.x + 22, 8, Mathf.Max(8, Screen.width - 155)),
                Screen.height - p.y - 18, 150, 36);
        }
        private bool TailLabelVisible(Vector2 screen)
        {
            if (room == null || !room.IsActiveRoom || GameSession.Instance == null || Camera.main == null
                || GameSession.Instance.State.Phase != GamePhase.Playing || GameSession.Instance.State.IsPaused
                || GameSession.Instance.State.SubState != PlayingSubState.Free || dragging || !Released || Secured
                || rack.CurrentEra != Era.Past || unlockTime > 0 || routeSlideRemaining > 0 || tailImage == null)
                return false;
            // Keep the label visible when the pointer travels from the rope onto the words.
            return !Routed || Time.time <= tailLabelUntil || OverPoint(screen, tailImage.transform.position)
                || TailLabelRect().Contains(new Vector2(screen.x, Screen.height - screen.y));
        }
        private bool OverTailHandle(Vector2 screen)
        {
            return tailImage != null && (OverPoint(screen, tailImage.transform.position)
                || (TailLabelVisible(screen)
                    && TailLabelRect().Contains(new Vector2(screen.x, Screen.height - screen.y))));
        }
        private bool RopeEndOverPoint(Vector3 point)
        {
            return Camera.main != null && OverPoint(Camera.main.WorldToScreenPoint(tail), point);
        }
        private bool TryRopePoleContact(out Vector3 contact)
        {
            contact = fork.position;
            return Camera.main != null && TryPoleContact(Camera.main.WorldToScreenPoint(tail), out contact);
        }
        private bool CanSecureTail()
        {
            return Released && Routed && !Secured && Combination
                && Vector2.Distance(tail, hook.position) < .5f && lift > .95f;
        }
        private void ReleaseKnot()
        {
            Debug.Log("[L01B Rope] Knot released; a free rope end is now draggable.");
            Mark(ReleasedFlag); unlockTime = .5f; freeEnd = ReleaseOrigin;
            Play(pullSound); GameSession.Instance.SaveProgressNow();
        }
        public bool Interact() => false;
        private Vector3 ReleaseOrigin => youngTreeImage!=null ? knot.position : knot.position+Vector3.right*.23f;
        private void BeginDrag()
        {
            Debug.Log("[L01B Rope] Grabbed rope end with pointer.");
            hookAttempted=false;
            // The label and the padded rope hit area are handles for the same visible end.
            // Preserve the grab offset: starting on the words must not teleport the rope.
            tail = tailImage.transform.position;
            Vector3 pointer = Camera.main != null ? Camera.main.ScreenToWorldPoint(PointerPosition) : tail;
            pointer.z = 0;
            dragPointerOffset = tail - pointer;
            dragPointerOffset.z = 0;
            dragging = ownsControl = true;
            GameSession.Instance.State.SetSubState(PlayingSubState.Interacting);
            if (ServiceRegistry.TryGet<ICameraRig>(out var camera)) camera.SetFollowFrozen(true);
            Play(pullSound);
        }
        private void StopDrag()
        {
            dragging = false; hookAttempted=false; dragPointerOffset = Vector3.zero;
            if (ownsControl && GameSession.Instance != null && GameSession.Instance.State.IsPaused)
            { releaseAfterPause = true; return; }
            if (ownsControl && GameSession.Instance != null && GameSession.Instance.State.SubState == PlayingSubState.Interacting)
                GameSession.Instance.State.SetSubState(PlayingSubState.Free);
            if (ownsControl && ServiceRegistry.TryGet<ICameraRig>(out var c)) c.SetFollowFrozen(false);
            ownsControl = false;
            releaseAfterPause = false;
        }
        private bool TryPoleContact(Vector2 screen, out Vector3 contact)
        {
            contact=fork.position;
            if(Camera.main==null)return false;
            if(poleStart==null||poleEnd==null)return OverPoint(screen,contact);
            Vector2 a=Camera.main.WorldToScreenPoint(poleStart.position);
            Vector2 b=Camera.main.WorldToScreenPoint(poleEnd.position);
            Vector2 axis=b-a;
            float t=axis.sqrMagnitude>.01f?Mathf.Clamp01(Vector2.Dot(screen-a,axis)/axis.sqrMagnitude):0;
            Vector2 closest=Vector2.Lerp(a,b,t);
            Vector2 padded=Camera.main.WorldToScreenPoint(poleStart.position+Vector3.up*.28f);
            if(Vector2.Distance(screen,closest)>Mathf.Max(14f,Vector2.Distance(a,padded)))return false;
            contact=Vector3.Lerp(poleStart.position,poleEnd.position,t);
            return Vector3.Distance(eye.position,contact)<=RopeLength-.08f;
        }
        private void Route(Vector3 contact)
        {
            Mark(RoutedFlag);
            routeSlideFrom=guideContact=contact; routeSlideRemaining=RouteSlideSeconds;
            tail=contact+Vector3.down*Mathf.Max(.08f,RopeLength-Vector3.Distance(eye.position,contact));
            Play(pullSound); StopDrag(); needNewPress=true;
            GameSession.Instance.SaveProgressNow();
        }
        private void AnimatePoleContact()
        {
            if(routeSlideRemaining<=0)return;
            routeSlideRemaining=Mathf.Max(0,routeSlideRemaining-Time.deltaTime);
            float t=Mathf.SmoothStep(0,1,1-routeSlideRemaining/RouteSlideSeconds);
            guideContact=Vector3.Lerp(routeSlideFrom,fork.position,t);
            // The contact slides along the pole; released length hangs below it.
            tail=guideContact+Vector3.down*Mathf.Max(.08f,RopeLength-Vector3.Distance(eye.position,guideContact));
            if(routeSlideRemaining==0)tailLabelUntil=Time.time+3;
        }
        private void Secure()
        {
            lift = 1; tail = hook.position;
            Mark(SecuredFlag); Mark(FoldingDryingRack.RaisedFlag);
            Play(lockSound); StopDrag(); Paint(); Physics2D.SyncTransforms();
            GameSession.Instance.SaveProgressNow();
        }
        private void PullTo(Vector3 pointer)
        {
            Vector3 d = pointer - fork.position;
            float maximum = RopeLength - Vector3.Distance(EyeAt(1), fork.position);
            d = Vector3.ClampMagnitude(d, maximum);
            tail = fork.position + d;
            float available = RopeLength - d.magnitude;
            // Distance eye-to-fork decreases monotonically along the chosen -22..0 arc.
            float lo = 0, hi = 1;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * .5f;
                if (Vector3.Distance(EyeAt(mid), fork.position) > available) lo = mid; else hi = mid;
            }
            lift = available >= Vector3.Distance(EyeAt(0), fork.position) ? 0 : (lo + hi) * .5f;
        }
        private void Update()
        {
            if (input == null) ServiceRegistry.TryGet(out input);
            if (GameSession.Instance == null) return;
            if (Released != wasReleased || Routed != wasRouted || Secured != wasSecured)
            {
                wasReleased = Released; wasRouted = Routed; wasSecured = Secured; hintLevel=0;
                if (!Released) { lift = 0; unlockTime = 0; freeEnd = rest.position; StopDrag(); }
            }
            if (!room.IsActiveRoom || GameSession.Instance.State.Phase != GamePhase.Playing) { StopDrag(); return; }
            if (GameSession.Instance.State.IsPaused)
            {
                // A released pointer while paused must not remain captured on resume.
                if (dragging && !PrimaryHeld) needNewPress = true;
                return;
            }
            if (releaseAfterPause) StopDrag();
            if (dragging && GameSession.Instance.State.SubState != PlayingSubState.Interacting) StopDrag();
            if (HintPressed && GameSession.Instance.State.SubState == PlayingSubState.Free)
            {
                string[] hints = !Released ? new[]{"观察绳子两端。","小树上绕着一圈绳子。","切到过去，点击小树上露出的绳结。"}
                    : !Routed ? new[]{"找找高处能绕过绳子的地方。","过去的晾架完好，现在树上多了一根晾衣杆。","保留过去的晾架，把绳尾搭到现在的晾衣杆上。"}
                    : !Secured ? new[]{"拉动垂下的绳尾，看看架子怎样变化。","墙上完好的绕绳钩能留住绳尾。","把绳尾向左拉到墙钩处，架子升平后就能挂住。"}
                    : new[]{"架子已经固定，可以往右走了。","保留过去的晾架与现在的树枝。","沿架面走到成年树枝，再登上右侧阳台。"};
                hintTime=0;Say(hints[Mathf.Min(hintLevel++,2)]);
            }
            AnimatePoleContact();
            if (needNewPress) { StopDrag(); if (!PrimaryHeld) needNewPress = false; Paint(); return; }
            if (unlockTime > 0)
            {
                unlockTime = Mathf.Max(0, unlockTime - Time.deltaTime);
                freeEnd = Vector3.Lerp(ReleaseOrigin, rest.position, Mathf.SmoothStep(0, 1, 1-unlockTime/.5f));
            }
            Vector3 mouse = Camera.main != null ? Camera.main.ScreenToWorldPoint(PointerPosition) : Vector3.zero; mouse.z = 0;
            bool ui = PrimaryPressed && PointerOverUI();
            if (!dragging && Ready(out var player) && !ui)
            {
                if (!Released && tree.CurrentEra == Era.Past && PrimaryPressed
                    && OverKnot(PointerPosition))
                    ReleaseKnot();
                else if (CanInspectHook && PrimaryPressed && OverPoint(PointerPosition, hook.position))
                    TryInspectHook();
                else if (!Secured && Released && rack.CurrentEra == Era.Past && unlockTime <= 0 && routeSlideRemaining <= 0 && PrimaryPressed
                    && OverTailHandle(PointerPosition))
                {
                    BeginDrag();
                }
            }
            if (dragging)
            {
                if (SecondaryPressed) { StopDrag(); needNewPress = true; }
                else if (!Routed || !Combination)
                {
                    // Loose stock remains in a visible sag; the end cannot stretch past its real material length.
                    Vector3 target = mouse + dragPointerOffset;
                    tail = eye.position + Vector3.ClampMagnitude(target-eye.position, RopeLength);
                    bool overHook=RopeEndOverPoint(hook.position);
                    if(overHook && !hookAttempted)
                    {
                        Say("架子抬不起来，得先把绳子绕到高处。");
                        Play(creakSound);
                    }
                    hookAttempted=overHook;
                    if (Combination && TryRopePoleContact(out var contact)) { Route(contact); }
                    if (!PrimaryHeld) StopDrag();
                }
                else if (Combination)
                {
                    PullTo(mouse + dragPointerOffset);
                    if (CanSecureTail()) Secure();
                    else if (!PrimaryHeld) StopDrag();
                }
            }
            if (!dragging && !SupportValid && routeSlideRemaining <= 0)
            {
                lift = Mathf.MoveTowards(lift, 0, Time.deltaTime * .7f);
                if (Routed && Combination && !Secured)
                {
                    float length = RopeLength - Vector3.Distance(EyeAt(lift), fork.position);
                    Vector3 direction = tail - fork.position;
                    if (direction.sqrMagnitude < .001f) direction = Vector3.down;
                    tail = fork.position + Vector3.Slerp(direction.normalized, Vector3.down, Time.deltaTime * 3f) * length;
                }
            }
            if (SupportValid) lift = 1;
            Paint();
        }
        public bool TryReleaseKnot()
        {
            if (!Ready(out var player) || Released || tree.CurrentEra != Era.Past) return false;
            ReleaseKnot(); return true;
        }
        private void Paint()
        {
            if (leaf == null || rope == null) return;
            leaf.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(foldedAngle, 0, lift));
            deck.enabled = SupportValid;
            bool combinedTree=youngTreeImage!=null&&youngTreeTied!=null&&youngTreeFree!=null;
            if(combinedTree)youngTreeImage.sprite=Released?youngTreeFree:youngTreeTied;
            knotImage.gameObject.SetActive(!combinedTree&&!Released && tree.CurrentEra == Era.Past);
            knotImage.sprite = tiedKnot;
            tailImage.gameObject.SetActive(Released && rack.CurrentEra == Era.Past && !Secured);
            bool routed = Routed && Combination;
            Vector3 tiedEnd=combinedTree&&tree.CurrentEra==Era.Past&&tiedLead!=null?tiedLead.position:knot.position;
            Vector3 end = !Released ? tiedEnd : (dragging || routed) ? tail : freeEnd;
            if (Secured) end = hook.position;
            tailImage.transform.position = end;
            rope.enabled = rack.CurrentEra == Era.Past;
            rope.sortingOrder = Released ? 15 : 7;
            int segments = 24;
            rope.positionCount = routed ? segments*2+1 : segments+1;
            Vector3 a = eye.position, b = routed ? VisualGuide : end;
            float slack = routed ? Mathf.Max(0, RopeLength-Vector3.Distance(a,b)-Vector3.Distance(b,end)) : .5f;
            for (int i=0; i<=segments; i++)
            {
                float t=i/(float)segments;
                var p=Vector3.Lerp(a,b,t); p.y-=Mathf.Sin(t*Mathf.PI)*(Secured ? .015f : Mathf.Min(.6f,slack*.25f));
                rope.SetPosition(i,p);
            }
            if (routed) for (int i=1;i<=segments;i++)
            {
                float t=i/(float)segments;
                var p=Vector3.Lerp(b,end,t);p.y-=Mathf.Sin(t*Mathf.PI)*Mathf.Min(.4f,slack*.2f);
                rope.SetPosition(segments+i,p);
            }
            if (ropeOutline != null)
            {
                ropeOutline.enabled=rope.enabled;ropeOutline.sortingOrder=rope.sortingOrder-1;
                ropeOutline.positionCount=rope.positionCount;
                for(int i=0;i<rope.positionCount;i++)ropeOutline.SetPosition(i,rope.GetPosition(i));
            }
            bool tied = !combinedTree&&!Released && tree.CurrentEra==Era.Past;
            if(treeWrapBack!=null)treeWrapBack.enabled=tied;
            if(treeWrapFront!=null)treeWrapFront.enabled=tied;
            if(tiedTail!=null)
            {
                tiedTail.enabled=tied;tiedTail.positionCount=3;
                Vector3 top=knot.position+Vector3.right*.23f;
                tiedTail.SetPosition(0,top);tiedTail.SetPosition(1,top+new Vector3(.025f,-.24f,0));
                tiedTail.SetPosition(2,top+new Vector3(Mathf.Sin(Time.time*1.7f)*.025f,-.48f,0));
            }
            if(slackCoil!=null)slackCoil.gameObject.SetActive(Released&&(!Routed||!Combination)&&!Secured&&rack.CurrentEra==Era.Past);
            if(cleatWrap!=null)cleatWrap.enabled=Secured&&rack.CurrentEra==Era.Past;
            bool hoverKnot=!Released&&tree.CurrentEra==Era.Past&&OverKnot(PointerPosition);
            if(youngTreeImage!=null)youngTreeImage.color=hoverKnot?new Color(1f,.95f,.81f):Color.white;
            bool hoverTail=Released&&!Secured&&OverTailHandle(PointerPosition);
            tailImage.color=hoverTail?new Color(1f,.94f,.72f):Color.white;
            knotImage.transform.localScale=knotScale*(hoverKnot?1.06f:1f);
            tailImage.transform.localScale=tailScale*(hoverTail?1.12f:1f);
            if(forkImage!=null)forkImage.color=dragging&&!Routed&&Combination&&TryRopePoleContact(out _)
                ? new Color(1f,.91f,.72f) : Color.white;
            hookImage.color=dragging&&(!Routed||!Combination)&&RopeEndOverPoint(hook.position)
                ? new Color(1f,.72f,.55f)
                : dragging&&CanSecureTail()
                    ? new Color(1f,.91f,.72f) : Color.white;
        }
        // A local affordance beside the loose end, never a puzzle-solution panel.
        private void OnGUI()
        {
            if (!TailLabelVisible(PointerPosition)) return;
            if (ropeLabelStyle == null)
            {
                ropeLabelStyle = new GUIStyle(GUI.skin.label) { font=labelFont, fontSize=20, alignment=TextAnchor.MiddleLeft };
                ropeLabelStyle.normal.textColor = new Color(1f,.94f,.78f);
                ropeLabelShadow = new GUIStyle(ropeLabelStyle);
                ropeLabelShadow.normal.textColor = new Color(.16f,.10f,.055f,.95f);
            }
            Rect area=TailLabelRect();
            GUI.Label(new Rect(area.x+1,area.y+1,area.width,area.height),"拖动绳尾",ropeLabelShadow);
            GUI.Label(area,"拖动绳尾",ropeLabelStyle);
        }
        public bool AllowsChange(string[] ids, Era targetEra, IPlayerContext player)
        {
            bool past = rack.CurrentEra == Era.Past, grown = tree.CurrentEra == Era.Present;
            foreach (string id in ids) { if (id==rack.ObjectId) past=targetEra==Era.Past; if(id==tree.ObjectId) grown=targetEra==Era.Present; }
            bool next = past && grown && Released && Routed && Secured;
            if (SupportValid && !next && player.IsGrounded && player.GroundCollider == deck) return false;
            if (next && !deck.enabled)
            {
                var bounds = new Bounds(leaf.position+new Vector3(deck.offset.x,deck.offset.y,0),new Vector3(deck.size.x-.04f,deck.size.y-.04f,1));
                if (bounds.Intersects(player.BodyBounds)) return false;
            }
            return true;
        }
    }
}
