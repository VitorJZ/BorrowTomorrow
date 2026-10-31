using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 开场磁带（新游戏进入 L00）：黑屏下播放两句童年录音，播完再淡入院门、空场停留、人物走入，最后交还操作。
    /// 黑屏不使用 UI 黑图，而是让主相机直接清屏为黑色，避免画布缩放取整后
    /// 屏幕边缘露出游戏画面；淡入使用相机空间的黑幕，同样覆盖整个视口。
    /// 对白内容、配音与进度标记仍由 DialoguePlayer 与 DialogueEntry 驱动；
    /// 场景中的 Auto_DLG_L00_INTRO_01 保留为兜底触发器。
    /// </summary>
    public sealed class OpeningTapeIntro : MonoBehaviour
    {
        [Tooltip("开场第一句（下一句通过 nextId 串联）")]
        [SerializeField] private DialogueEntry firstLine;

        [SerializeField, Min(0f)] private float sceneHoldSeconds = 1f;
        [SerializeField, Min(.1f)] private float entranceSpeed = 2.4f;
        [SerializeField, Min(0f)] private float arrivalHoldSeconds = .45f;

        public bool EntranceComplete { get; private set; }
        private bool entranceStarted;
        private float entranceOffset;
        private ICharacterArt entranceArt;
        private bool started;
        private bool revealStarted;

        private Camera blackedCamera;
        private CameraClearFlags savedClearFlags;
        private Color savedBackgroundColor;
        private int savedCullingMask;
        private bool cameraBlackedOut;

        private GameObject blackoutObject;
        private SpriteRenderer blackoutRenderer;
        private Texture2D blackoutTexture;
        private Sprite blackoutSprite;

        private void Update()
        {
            if (!started)
            {
                TryBegin();
                return;
            }
            UpdateReveal();
        }

        private void LateUpdate()
        {
            UpdateBlackoutSize();
        }

        private void TryBegin()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing)
            {
                return;
            }
            if (session.StoryFlags.HasFlag(StoryFlags.IntroPlayed))
            {
                started = true;
                revealStarted = true;
                EntranceComplete = true;
                return;
            }
            if (!ServiceRegistry.TryGet<IDialogueService>(out var dialogue) || dialogue.IsPlaying)
            {
                return;
            }
            started = true;
            if (firstLine == null)
            {
                Debug.LogWarning("[OpeningTapeIntro] 未配置首句，跳过黑屏开场。");
                BeginReveal(0.6f, session);
                return;
            }
            BlackoutCamera();
            PrepareEntrance();
            dialogue.Play(firstLine, DialoguePriority.Story, () => BeginReveal(0.6f, session));
            if (!dialogue.IsPlaying)
            {
                // 前置标记等原因导致未播放时不要把人锁在黑屏里。
                BeginReveal(0.6f, session);
            }
        }

        private void UpdateReveal()
        {
            if (revealStarted)
            {
                return;
            }
            GameSession session = GameSession.Instance;
            if (session == null)
            {
                return;
            }
            if (!ServiceRegistry.TryGet<IDialogueService>(out var dialogue) || !dialogue.IsPlaying
                || session.StoryFlags.HasFlag(StoryFlags.IntroPlayed))
            {
                BeginReveal(0.6f, session);
                return;
            }

        }

        private void BlackoutCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            blackedCamera = cam;
            savedClearFlags = cam.clearFlags;
            savedBackgroundColor = cam.backgroundColor;
            savedCullingMask = cam.cullingMask;
            // 真正的全屏黑：相机清屏为黑色且不渲染任何世界物体，UI 叠层不受影响。
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cameraBlackedOut = true;
        }

        private void RestoreCamera()
        {
            if (!cameraBlackedOut)
            {
                return;
            }
            if (blackedCamera != null)
            {
                blackedCamera.clearFlags = savedClearFlags;
                blackedCamera.backgroundColor = savedBackgroundColor;
                blackedCamera.cullingMask = savedCullingMask;
            }
            cameraBlackedOut = false;
        }

        private void BeginReveal(float seconds, GameSession session)
        {
            if (revealStarted)
            {
                return;
            }
            revealStarted = true;
            PrepareEntrance(true);
            if (session != null) session.State.SetSubState(PlayingSubState.Cutscene);

            if (cameraBlackedOut)
            {
                // 先铺好相机空间黑幕再恢复相机渲染，避免中间一帧闪出关卡。
                CreateBlackoutQuad();
                RestoreCamera();
                // 黑幕由相机层负责，UI 淡入层直接透明，避免两层黑叠加造成边缘不均。
                if (ServiceRegistry.TryGet<IHudService>(out var hud))
                {
                    hud.FadeIn(0f);
                }
                StartCoroutine(FadeBlackoutQuad(Mathf.Max(0.01f, seconds)));
            }
            else if (ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                // 没有主相机时的兜底：退回原来的 UI 淡入。
                hud.FadeIn(0.6f);
            }

            if (!entranceStarted)
            {
                entranceStarted = true;
                StartCoroutine(StageEntrance(Mathf.Max(.01f, seconds), session));
            }
        }

        private void PrepareEntrance(bool refresh = false)
        {
            if (entranceArt != null && !refresh) return;
            if (!ServiceRegistry.TryGet<ICharacterArt>(out entranceArt)) return;
            if (!ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsPresent) return;
            Camera cam = Camera.main;
            float left = cam != null ? cam.ViewportToWorldPoint(new Vector3(0f, .5f,
                Mathf.Abs(cam.transform.position.z))).x : player.Position.x - 5f;
            // 留足人物宽度：第一帧淡入时整个人都在屏幕外。
            entranceOffset = Mathf.Min(-2f, left - player.Position.x - Mathf.Max(1f, entranceArt.EntranceRightExtent) - .5f);
            entranceArt.SetEntranceOffset(entranceOffset, false);
        }

        private IEnumerator StageEntrance(float revealSeconds, GameSession session)
        {
            yield return HoldStage(revealSeconds + sceneHoldSeconds, session);
            while (entranceArt != null && entranceOffset < 0f)
            {
                if (session == null || session.State.Phase != GamePhase.Playing) yield break;
                if (!session.State.IsPaused)
                {
                    entranceOffset = Mathf.MoveTowards(entranceOffset, 0f, entranceSpeed * Time.deltaTime);
                    entranceArt.SetEntranceOffset(entranceOffset, entranceOffset < 0f);
                }
                yield return null;
            }
            entranceArt?.SetEntranceOffset(0f, false);
            yield return HoldStage(arrivalHoldSeconds, session);
            EntranceComplete = true;
            if (session != null && session.State.SubState == PlayingSubState.Cutscene)
                session.State.SetSubState(PlayingSubState.Free);
        }

        private static IEnumerator HoldStage(float seconds, GameSession session)
        {
            while (seconds > 0f && session != null && session.State.Phase == GamePhase.Playing)
            {
                if (!session.State.IsPaused) seconds -= Time.deltaTime;
                yield return null;
            }
        }

        private void CreateBlackoutQuad()
        {
            if (blackedCamera == null)
            {
                return;
            }
            blackoutTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            blackoutTexture.SetPixel(0, 0, Color.white);
            blackoutTexture.Apply();
            blackoutSprite = Sprite.Create(blackoutTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            blackoutObject = new GameObject("OpeningTapeBlackout");
            blackoutObject.transform.SetParent(blackedCamera.transform, false);
            blackoutObject.transform.localPosition = new Vector3(0f, 0f, 1f);
            blackoutRenderer = blackoutObject.AddComponent<SpriteRenderer>();
            blackoutRenderer.sprite = blackoutSprite;
            blackoutRenderer.color = Color.black;
            // BoundaryFx 在本工程里 uniqueID 为 0，会被当成 Default 而压不住背景层；
            // Foreground 是当前有效的最高排序层，用它在相机内覆盖整个世界。
            blackoutRenderer.sortingLayerName = "Foreground";
            blackoutRenderer.sortingOrder = 32000;
            UpdateBlackoutSize();
        }

        private void UpdateBlackoutSize()
        {
            if (blackedCamera == null || blackoutObject == null)
            {
                return;
            }
            // 留出余量，覆盖相机正交尺寸变化与画布取整误差。
            float height = blackedCamera.orthographicSize * 2f + 8f;
            float width = height * Mathf.Max(0.1f, blackedCamera.aspect) + 8f;
            blackoutObject.transform.localScale = new Vector3(width, height, 1f);
        }

        private IEnumerator FadeBlackoutQuad(float seconds)
        {
            float t = 0f;
            Color color = blackoutRenderer != null ? blackoutRenderer.color : Color.clear;
            while (t < seconds)
            {
                if (GameSession.Instance != null && GameSession.Instance.State.IsPaused)
                {
                    yield return null;
                    continue;
                }
                t += Time.deltaTime;
                if (blackoutRenderer == null)
                {
                    break;
                }
                color.a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / seconds));
                blackoutRenderer.color = color;
                yield return null;
            }
            DestroyBlackoutQuad();
        }

        private void DestroyBlackoutQuad()
        {
            if (blackoutObject != null)
            {
                Destroy(blackoutObject);
            }
            blackoutObject = null;
            blackoutRenderer = null;
            blackedCamera = null;
            if (blackoutSprite != null)
            {
                Destroy(blackoutSprite);
            }
            blackoutSprite = null;
            if (blackoutTexture != null)
            {
                Destroy(blackoutTexture);
            }
            blackoutTexture = null;
        }

        private void OnDestroy()
        {
            entranceArt?.SetEntranceOffset(0f, false);
            // 组件比相机先销毁时也要还原清屏设置，避免相机停留在黑屏状态。
            RestoreCamera();
            if (blackoutSprite != null)
            {
                Destroy(blackoutSprite);
            }
            if (blackoutTexture != null)
            {
                Destroy(blackoutTexture);
            }
        }
    }
}
