using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 阿黄生活回声（剧情 1.4 §3.2）：过去态趴卧，录音播放时抬头并摇尾；
    /// 现在态只留空狗窝与收好的食盆，不触发回声。动作全部脚本化，透明逐帧素材，不用抠白着色器。
    /// </summary>
    public sealed class NarrativeDogEcho : MonoBehaviour
    {
        public Sprite[] sleep;
        public Sprite[] alert;
        public Sprite[] wag;
        [Tooltip("现在态的空狗窝与食盆（过去态隐藏）")]
        public SpriteRenderer presentProps;
        public float anchorX = 14f;
        private SpriteRenderer renderer2d;
        private DialoguePlayer player;
        private float echoTimer;
        private const float EchoSeconds = 4.5f;

        // 与触发条件共用已提交的年代，避免渲染更新顺序让消失的阿黄仍触发对白。
        public bool IsVisibleInPast => isActiveAndEnabled
            && ServiceRegistry.TryGet<IBoundaryContext>(out var boundary) && boundary.EraAtAnchor(anchorX) == Era.Past;

        private void Start()
        {
            renderer2d = gameObject.GetComponent<SpriteRenderer>();
            if (renderer2d == null)
            {
                renderer2d = gameObject.AddComponent<SpriteRenderer>();
                renderer2d.sortingLayerName = "EraPlatform";
                renderer2d.sortingOrder = 5;
            }
            if (ServiceRegistry.TryGet<IDialogueService>(out var d)) player = d as DialoguePlayer;
            if (player != null) player.LineStarted += OnLine;
        }

        private void OnLine(DialogueEntry entry)
        {
            if (entry.entryId != "DLG_L00_DOG_01" || !IsVisibleInPast) return;
            echoTimer = EchoSeconds;
            if (ServiceRegistry.TryGet<IAudioService>(out var audio)) audio.PlaySfx("sfx_dog_soft");
        }

        private void Update()
        {
            if (renderer2d == null) return;
            if (GameSession.Instance != null && GameSession.Instance.State.IsPaused) return;
            echoTimer = Mathf.Max(0, echoTimer - Time.deltaTime);
            bool past = IsVisibleInPast;
            renderer2d.enabled = past;
            if (presentProps != null) presentProps.enabled = !past;
            if (!past) return;
            float elapsed = EchoSeconds - echoTimer;
            Sprite[] frames; float fps;
            if (echoTimer <= 0f) { frames = sleep; fps = 2f; elapsed = Time.time; }
            else if (elapsed < 1.1f) { frames = alert; fps = 6f; }
            else { frames = wag; fps = 8f; }
            if (frames != null && frames.Length > 0)
                renderer2d.sprite = frames[(int)(elapsed * fps) % frames.Length];
        }

        private void OnDestroy()
        {
            if (player != null) player.LineStarted -= OnLine;
        }
    }
}
