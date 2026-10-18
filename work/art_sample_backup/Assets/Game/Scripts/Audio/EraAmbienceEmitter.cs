using UnityEngine;

namespace BorrowTomorrow.Audio
{
    /// <summary>
    /// 年代环境声发射器（策划 11.4 / 09）：
    /// 挂在年代物体（TemporalObject）同物体或其子物体上，
    /// 该物体年代状态变化时按声源所属年代启停（如缝纫机声只在 1994 侧发声）。
    /// 策划 09：环境声按具体物件年代切换，不能仅因屏幕过去面积大就让
    /// 已消失的缝纫机继续发声。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EraAmbienceEmitter : MonoBehaviour
    {
        [Tooltip("本声源属于哪个年代")]
        [SerializeField] private Core.Data.Era ownerEra = Core.Data.Era.Past;

        private AudioSource source;
        private TimeBoundary.TemporalObject hostObject;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            hostObject = GetComponentInParent<TimeBoundary.TemporalObject>();
            if (hostObject != null)
            {
                hostObject.EraApplied += HandleEraApplied;
                HandleEraApplied(hostObject, hostObject.CurrentEra);
            }
            else
            {
                source.Play(); // 不属于年代物体：常驻环境声（风、鸟等恒定层）
            }
        }

        private void OnDestroy()
        {
            if (hostObject != null)
            {
                hostObject.EraApplied -= HandleEraApplied;
            }
        }

        private void HandleEraApplied(TimeBoundary.TemporalObject obj, Core.Data.Era era)
        {
            bool shouldPlay = era == ownerEra;
            if (shouldPlay && !source.isPlaying)
            {
                source.Play();
            }
            else if (!shouldPlay && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
