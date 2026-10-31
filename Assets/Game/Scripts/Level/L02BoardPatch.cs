using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// L02 外台补板（策划 7.4 增补），两段都只在单一年代里完成：
    /// 1994 年：木梯完整；玩家在砖台上把墙边旧木板搬到砖台（外婆“留着以后用”）。
    /// 2024 年：维修架已建好但缺一块板；抓住砖台上的旧木板，用 A/D 推进缺口，再按 E 落下。
    /// 木板必须两端同时搭在维修架左段与砖台上才算成功，推过头或没到位都会滑回重试。
    /// </summary>
    public sealed class L02BoardPatch : MonoBehaviour, IInteractable
    {
        public const string PlankMovedFlag = "Mechanism_L02_PlankMoved";
        public const string BoardPlacedFlag = "Mechanism_L02_BoardPlaced";
        private const string MaintenanceId = "L02_maintenance";

        private const float GapCenterX = 6.9f;
        private const float GapCenterY = 2.95f;
        private const float BenchCenterX = 9.6f;
        private const float BenchCenterY = 3.2f;
        private const float SnapTolerance = 0.2f;
        private const float DragMinX = 5.1f;
        private const float DragMaxX = 11.6f;
        private const float DragSpeed = 2.1f;
        private const float ReturnSeconds = .45f;

        [Header("引用（留空时按场景结构自动解析）")]
        [SerializeField] private TemporalObject boardObject;
        [SerializeField] private TemporalObject maintenanceObject;
        [SerializeField] private Transform boardVisual;
        [SerializeField] private GameObject benchBoard;
        [SerializeField] private GameObject gapBoard;

        [Header("表现")]
        [SerializeField] private AudioClip liftSound;
        [SerializeField] private AudioClip settleSound;
        [SerializeField] private float patchSeconds = 1.2f;

        private LevelRoot room;
        private AudioSource sound;
        private IInputSource input;
        private bool busy, ownsControl;
        private Transform moving;
        private Vector3 startPosition, targetPosition;
        private Quaternion startRotation;
        private float elapsed;

        private bool dragging, returnPending, needNewPress;
        private float boardX, returnElapsed;
        private Vector3 returnFrom;
        private Quaternion returnRotation;

        public string InteractId => "L02_board_patch";

        private bool Moved => GameSession.Instance != null
            && GameSession.Instance.StoryFlags.HasFlag(PlankMovedFlag);
        private bool Placed => GameSession.Instance != null
            && GameSession.Instance.StoryFlags.HasFlag(BoardPlacedFlag);

        /// <summary>1994 年：墙边木板可见，可以搬到砖台上。</summary>
        private bool CanMovePlank => !Moved && !Placed
            && boardObject != null && boardObject.CurrentEra == Era.Past
            && boardVisual != null && benchBoard != null;

        /// <summary>2024 年：砖台上的旧木板与缺板的维修架同时存在，可以推进缺口。</summary>
        private bool CanPlaceBoard => Moved && !Placed
            && maintenanceObject != null && maintenanceObject.CurrentEra == Era.Present
            && benchBoard != null && gapBoard != null;

        public bool CanInteract => isActiveAndEnabled && !busy && !dragging && !returnPending
            && room != null && room.IsActiveRoom && (CanMovePlank || CanPlaceBoard);

        public string InteractPrompt => CanPlaceBoard
            ? "抓住旧木板（A/D 推，E 放下）"
            : "把木板搬到砖台上";

        private void Awake()
        {
            room = GetComponentInParent<LevelRoot>();
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.volume = .7f;
            ResolveReferences();
        }

        private void Start()
        {
            ApplyCompleted();
        }

        private void ResolveReferences()
        {
            if (boardObject == null) boardObject = GetComponent<TemporalObject>();
            if (boardVisual == null) boardVisual = transform.Find("Past/ArtSkin");
            if (room == null) room = GetComponentInParent<LevelRoot>();
            if (maintenanceObject == null && room != null)
            {
                foreach (var temporal in room.GetComponentsInChildren<TemporalObject>(true))
                {
                    if (temporal.ObjectId != MaintenanceId) continue;
                    maintenanceObject = temporal;
                    break;
                }
            }
            if (gapBoard == null && maintenanceObject != null)
            {
                var gap = maintenanceObject.transform.Find("Present/GapBoard");
                if (gap != null) gapBoard = gap.gameObject;
            }
            if (benchBoard == null && room != null)
            {
                foreach (var candidate in room.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name != "BenchBoard") continue;
                    benchBoard = candidate.gameObject;
                    break;
                }
            }
        }

        public bool Interact()
        {
            var session = GameSession.Instance;
            if (session == null) return false;
            if (dragging)
            {
                ReleaseDrag();
                return true;
            }
            if (!session.State.IsPlayerControlAllowed || !CanInteract) return false;
            if (!ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsGrounded || player.IsClimbingLadder)
                return false;
            ResolveReferences();
            if (CanPlaceBoard) return BeginDrag(session);
            if (boardVisual == null) return false;

            // 1994：把墙边木板搬到砖台上。
            moving = boardVisual;
            targetPosition = benchBoard.transform.position;
            startPosition = moving.position;
            startRotation = moving.rotation;
            busy = ownsControl = true;
            elapsed = 0f;
            session.State.SetSubState(PlayingSubState.Interacting);
            if (ServiceRegistry.TryGet<ICharacterArt>(out var character)) character.PlayGesture("Interact");
            Play(liftSound);
            return true;
        }

        private bool BeginDrag(GameSession session)
        {
            if (benchBoard == null) return false;
            dragging = true;
            ownsControl = true;
            needNewPress = true;
            boardX = benchBoard.transform.position.x;
            benchBoard.SetActive(true);
            session.State.SetSubState(PlayingSubState.Interacting);
            if (ServiceRegistry.TryGet<ICharacterArt>(out var character)) character.PlayGesture("Interact");
            Play(liftSound);
            return true;
        }

        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null || room == null) return;
            if (dragging)
            {
                UpdateDrag(session);
                return;
            }
            if (returnPending)
            {
                UpdateReturn(session);
                return;
            }
            if (!busy || moving == null) return;
            if (!room.IsActiveRoom || session.State.Phase != GamePhase.Playing
                || session.State.SubState != PlayingSubState.Interacting)
            {
                Cancel();
                return;
            }
            if (session.State.IsPaused) return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / patchSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            moving.position = Vector3.Lerp(startPosition, targetPosition, eased)
                + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .18f);
            moving.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, eased);
            if (t < 1f) return;
            busy = false;
            Play(settleSound);
            if (boardVisual != null) boardVisual.gameObject.SetActive(false);
            if (benchBoard != null) benchBoard.SetActive(true);
            session.StoryFlags.SetFlag(PlankMovedFlag);
            moving = null;
            Release();
            session.SaveProgressNow();
        }

        private void UpdateDrag(GameSession session)
        {
            if (!room.IsActiveRoom || session.State.Phase != GamePhase.Playing
                || session.State.SubState != PlayingSubState.Interacting)
            {
                CancelDrag();
                return;
            }
            if (session.State.IsPaused) return;
            if (input == null) ServiceRegistry.TryGet<IInputSource>(out input);
            if (needNewPress)
            {
                if (input == null || !input.InteractPressedThisFrame) needNewPress = false;
            }
            else if (input != null && input.ConsumeInteract())
            {
                ReleaseDrag();
                return;
            }
            float move = input != null ? input.MoveAxis : 0f;
            boardX = Mathf.Clamp(boardX + move * DragSpeed * Time.deltaTime, DragMinX, DragMaxX);
            ApplyDragPose();
        }

        private void ApplyDragPose()
        {
            if (benchBoard == null) return;
            float offset = boardX - GapCenterX;
            float tilt = Mathf.Clamp(offset * 4f, -10f, 10f);
            float height = Mathf.Lerp(GapCenterY, BenchCenterY, Mathf.Clamp01(Mathf.Abs(offset) / 2.7f));
            benchBoard.transform.position = new Vector3(boardX, height, 0f);
            benchBoard.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void ReleaseDrag()
        {
            if (!dragging) return;
            dragging = false;
            if (benchBoard == null || Mathf.Abs(boardX - GapCenterX) > SnapTolerance)
            {
                // 没对准：木板滑回砖台，可重试。
                returnFrom = benchBoard != null ? benchBoard.transform.position : Vector3.zero;
                returnRotation = benchBoard != null ? benchBoard.transform.rotation : Quaternion.identity;
                returnElapsed = 0f;
                returnPending = true;
                Play(liftSound);
                return;
            }
            benchBoard.transform.position = new Vector3(GapCenterX, GapCenterY, 0f);
            benchBoard.transform.rotation = Quaternion.identity;
            benchBoard.SetActive(false);
            if (gapBoard != null) gapBoard.SetActive(true);
            Play(settleSound);
            var session = GameSession.Instance;
            if (session != null) session.StoryFlags.SetFlag(BoardPlacedFlag);
            Release();
            if (session != null) session.SaveProgressNow();
        }

        private void UpdateReturn(GameSession session)
        {
            if (!room.IsActiveRoom || session.State.Phase != GamePhase.Playing
                || session.State.SubState != PlayingSubState.Interacting)
            {
                CancelDrag();
                return;
            }
            if (session.State.IsPaused) return;
            returnElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(returnElapsed / ReturnSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            if (benchBoard != null)
            {
                var target = new Vector3(BenchCenterX, BenchCenterY, 0f);
                benchBoard.transform.position = Vector3.Lerp(returnFrom, target, eased);
                benchBoard.transform.rotation = Quaternion.Slerp(returnRotation, Quaternion.identity, eased);
            }
            if (t < 1f) return;
            returnPending = false;
            Release();
        }

        private void CancelDrag()
        {
            dragging = false;
            returnPending = false;
            if (benchBoard != null)
            {
                benchBoard.transform.position = new Vector3(BenchCenterX, BenchCenterY, 0f);
                benchBoard.transform.rotation = Quaternion.identity;
            }
            Release();
        }

        private void ApplyCompleted()
        {
            if (Placed)
            {
                if (boardVisual != null) boardVisual.gameObject.SetActive(false);
                if (benchBoard != null) benchBoard.SetActive(false);
                if (gapBoard != null) gapBoard.SetActive(true);
            }
            else if (Moved)
            {
                if (boardVisual != null) boardVisual.gameObject.SetActive(false);
                if (benchBoard != null) benchBoard.SetActive(true);
            }
        }

        private void Cancel()
        {
            busy = false;
            if (moving != null)
            {
                moving.position = startPosition;
                moving.rotation = startRotation;
            }
            moving = null;
            Release();
        }

        private void Release()
        {
            if (ownsControl && GameSession.Instance != null
                && GameSession.Instance.State.SubState == PlayingSubState.Interacting)
            {
                GameSession.Instance.State.SetSubState(PlayingSubState.Free);
            }
            ownsControl = false;
        }

        private void Play(AudioClip clip)
        {
            if (sound != null && clip != null) sound.PlayOneShot(clip);
        }
    }
}
