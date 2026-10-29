using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 开场磁带（新游戏进入 L00）：黑屏下播放两句童年录音，播完再淡入院门。
    /// 黑屏不使用 UI 黑图，而是让主相机直接清屏为黑色，避免画布缩放取整后
    /// 屏幕边缘露出游戏画面；淡入使用相机空间的黑幕，同样覆盖整个视口。
    /// 对白内容、配音与进度标记仍由 DialoguePlayer 与 DialogueEntry 驱动；
    /// 场景中的 Auto_DLG_L00_INTRO_01 保留为兜底触发器。
    /// </summary>
    public sealed class OpeningTapeIntro : MonoBehaviour
    {
        [Tooltip("开场第一句（下一句通过 nextId 串联）")]
        [SerializeField] private DialogueEntry firstLine;

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
            dialogue.Play(firstLine, DialoguePriority.Story);
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
            // 第三句开始时淡入关卡（该句在数据里标记 fadeInOnStart）。
            if (dialogue is DialoguePlayer player && player.Current != null && player.Current.fadeInOnStart)
            {
                BeginReveal(player.SceneRevealFadeSeconds, session);
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

            // 第三句仍在播放时继续保持过场锁定；只有开场失败/提前结束时才放开操作。
            bool dialogueStillPlaying = ServiceRegistry.TryGet<IDialogueService>(out var playing) && playing.IsPlaying;
            if (!dialogueStillPlaying && session != null && session.State.SubState == PlayingSubState.Cutscene)
            {
                session.State.SetSubState(PlayingSubState.Free);
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
                t += Time.unscaledDeltaTime;
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
