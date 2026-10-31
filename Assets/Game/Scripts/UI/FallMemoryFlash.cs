using System;
using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    // Ten independent easter eggs, ordered by emotional depth, not a story sequence.
    // Progress uses the existing story save, never checkpoint spatial snapshots.
    public sealed class FallMemoryFlash : MonoBehaviour, IFallMemoryService
    {
        public static readonly string[] ImageIds = { "01", "03", "04", "06", "07", "08", "09", "10", "11", "12" };
        public static string SeenFlag(int index) => "FallMemory_v1_Seen_" + ImageIds[index];
        private GameSession session;
        private GameObject overlay;
        private RawImage backdrop, memory;
        private Image blackout;
        private Material backdropMaterial, memoryMaterial;
        private Texture2D capture;
        private bool initialized;
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public string DisplayedId { get; private set; }
        public bool OverlayVisible => overlay != null && overlay.activeInHierarchy;
        public int NextIndex
        {
            get
            {
                if (session == null) return ImageIds.Length;
                for (int i = 0; i < ImageIds.Length; i++)
                    if (!session.StoryFlags.HasFlag(SeenFlag(i))) return i;
                return ImageIds.Length;
            }
        }
        public bool HasNext => !IsPlaying && NextIndex < ImageIds.Length;

        public void Initialize(GameSession owner)
        {
            if (initialized) return;
            session = owner;
            initialized = true;
            session.State.PhaseChanged += PhaseChanged;
            session.State.SubStateChanged += SubStateChanged;
            ServiceRegistry.Register<IFallMemoryService>(this);
        }

        private void PhaseChanged(GamePhase before, GamePhase after)
        {
            if (after != GamePhase.Playing) Cancel();
        }
        private void SubStateChanged(PlayingSubState before, PlayingSubState after)
        {
            if (IsPlaying && overlay != null) overlay.SetActive(after == PlayingSubState.Respawning);
        }

        private RawImage MakeLayer(string name, Material material)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(overlay.transform, false);
            var image = go.GetComponent<RawImage>();
            Stretch(image.rectTransform);
            image.raycastTarget = false;
            image.material = material;
            return image;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void EnsureOverlay()
        {
            if (overlay != null) return;
            overlay = new GameObject("FallMemoryOverlay", typeof(RectTransform), typeof(Canvas));
            overlay.transform.SetParent(transform, false);
            Stretch((RectTransform)overlay.transform);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 31000;
            var shader = Resources.Load<Shader>("FallMemories/FallMemorySoft");
            if (shader != null)
            {
                backdropMaterial = new Material(shader);
                memoryMaterial = new Material(shader);
            }
            backdrop = MakeLayer("FrozenFall", backdropMaterial);
            memory = MakeLayer("Memory", memoryMaterial);
            var go = new GameObject("ReturnAtBlack", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(overlay.transform, false);
            blackout = go.GetComponent<Image>(); Stretch(blackout.rectTransform);
            blackout.raycastTarget = false;
            overlay.SetActive(false);
        }
        private static void Alpha(Graphic image, float alpha) => image.color = new Color(1, 1, 1, alpha);
        private void Black(float alpha) => blackout.color = new Color(.01f, .015f, .02f, alpha);
        private static void Set(Material mat, string property, float value)
        {
            if (mat != null) mat.SetFloat(property, value);
        }
        private static Rect Cover(Texture texture)
        {
            float imageRatio = (float)texture.width / texture.height;
            float viewRatio = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            if (imageRatio > viewRatio) { float w = viewRatio / imageRatio; return new Rect((1-w)*.5f, 0, w, 1); }
            float h = imageRatio / viewRatio; return new Rect(0, (1-h)*.5f, 1, h);
        }

        public IEnumerator Play(Action restoreAtBlack)
        {
            int index = NextIndex;
            if (!HasNext || session.State.Phase != GamePhase.Playing) yield break;
            var texture = Resources.Load<Texture2D>("FallMemories/CG_Fall_" + ImageIds[index]);
            // Missing art still restores the player, without consuming unseen memory.
            if (texture == null) { restoreAtBlack?.Invoke(); yield break; }
            EnsureOverlay();
            IsPlaying = true; Elapsed = 0; DisplayedId = ImageIds[index];
            memory.texture = texture; memory.uvRect = Cover(texture); Alpha(memory, 0); Black(0);
            yield return new WaitForEndOfFrame();
            if (!IsPlaying) yield break;
            capture = ScreenCapture.CaptureScreenshotAsTexture();
            backdrop.texture = capture != null ? capture : Texture2D.blackTexture;
            Alpha(backdrop, 1);
            overlay.SetActive(!session.State.IsPaused);
            float hold = index >= 6 ? .60f : .52f;
            float fadeAt = .30f + hold, restoreAt = fadeAt + .20f, duration = restoreAt + .15f;
            bool marked = false, restored = false;
            try
            {
                while (Elapsed < duration && IsPlaying && session.State.Phase == GamePhase.Playing)
                {
                    if (session.State.IsPaused) { yield return null; continue; }
                    float t = Elapsed, alpha = 0, blur = 14;
                    if (t < .12f)
                    {
                        float p = Smooth(t / .12f);
                        Set(backdropMaterial, "_Blur", p * 5); Set(backdropMaterial, "_Dim", 1-p*.68f);
                        Set(backdropMaterial, "_Drop", p*.017f); Set(backdropMaterial, "_Zoom", 1.045f);
                    }
                    else if (t < .30f) { float p = Smooth((t-.12f)/.18f); alpha = p; blur = Mathf.Lerp(14, 1.2f, p); }
                    else if (t < fadeAt) { alpha = 1; blur = 1.2f; }
                    else if (t < restoreAt) { float p = Smooth((t-fadeAt)/.20f); alpha = 1-p; blur = 1.2f+14*p; Black(p); }
                    else
                    {
                        Black(1);
                        Alpha(backdrop, 0);
                        if (!restored) { restored = true; restoreAtBlack?.Invoke(); }
                        Black(1-Smooth((t-restoreAt)/.15f));
                    }
                    if (!marked && t >= .30f)
                    {
                        marked = true;
                        session.StoryFlags.SetFlag(SeenFlag(index));
                    }
                    Alpha(memory, alpha);
                    Set(memoryMaterial, "_Blur", blur); Set(memoryMaterial, "_Dim", 1);
                    Set(memoryMaterial, "_Zoom", 1.025f + Mathf.Clamp01((t-.12f)/(restoreAt-.12f))*.01f);
                    Set(memoryMaterial, "_Grain", .025f); Set(memoryMaterial, "_Vignette", .28f);
                    Set(memoryMaterial, "_Ghost", t < .30f || t >= fadeAt ? alpha*(1-alpha)*.10f : 0);
                    yield return null;
                    if (!session.State.IsPaused) Elapsed += Time.unscaledDeltaTime;
                }
                if (!restored && IsPlaying && session.State.Phase == GamePhase.Playing) restoreAtBlack?.Invoke();
            }
            finally { Cancel(); }
        }
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t*t*(3-2*t); }
        public void Cancel()
        {
            IsPlaying = false;
            if (overlay != null) overlay.SetActive(false);
            if (backdrop != null) backdrop.texture = null;
            if (capture != null) Destroy(capture);
            capture = null;
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (session != null)
            {
                session.State.PhaseChanged -= PhaseChanged;
                session.State.SubStateChanged -= SubStateChanged;
            }
            ServiceRegistry.Unregister<IFallMemoryService>(this);
            if (backdropMaterial != null) Destroy(backdropMaterial);
            if (memoryMaterial != null) Destroy(memoryMaterial);
        }
    }
}
