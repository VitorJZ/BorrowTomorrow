using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 全屏淡入淡出层：跌落恢复（0.3 秒）、场景转场共用（策划 4.6）。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class FadeScreen : MonoBehaviour
    {
        [Tooltip("淡出色（默认纯黑）")]
        [SerializeField] private Color fadeColor = Color.black;

        private Image image;
        private Coroutine running;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.raycastTarget = false; // 不遮挡交互
            SetAlpha(0f);
        }

        public void FadeOut(float seconds)
        {
            Restart(image.color.a, 1f, seconds);
        }

        public void FadeIn(float seconds)
        {
            Restart(image.color.a, 0f, seconds);
        }

        private void Restart(float from, float to, float seconds)
        {
            if (running != null)
            {
                StopCoroutine(running);
            }
            running = null;
            if (seconds <= 0f)
            {
                SetAlpha(to);
                return;
            }
            running = StartCoroutine(FadeRoutine(from, to, seconds));
        }

        private IEnumerator FadeRoutine(float from, float to, float seconds)
        {
            float t = 0f;
            SetAlpha(from);
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetAlpha(to);
            running = null;
        }

        private void SetAlpha(float alpha)
        {
            Color c = fadeColor;
            c.a = Mathf.Clamp01(alpha);
            image.color = c;
            image.enabled = c.a > 0.001f;
        }
    }
}
