using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Audio
{
    /// <summary>
    /// 音频服务（策划 10.4 AudioService / 09 音乐音效）：
    /// 一条主旋律的两个轻配器版本按过去面积比例平滑混合；
    /// 对白期间轻度压低其他声部；年代切换音效按稳定 id 播放。
    /// 策划 09：环境声按具体物件年代切换——由 EraAmbienceEmitter 负责，
    /// 不因屏幕过去面积大就让已消失的声源继续发声。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioService : MonoBehaviour, IAudioService, IBootInitializable
    {
        [Header("音乐（策划 09：一条主旋律，过去/现在两个轻配器版本 + 尾声版本）")]
        [SerializeField] private AudioClip musicPast;
        [SerializeField] private AudioClip musicPresent;
        [SerializeField] private AudioClip musicEpilogue;

        [Header("混音")]
        [SerializeField] private float crossfadeSpeed = 1.5f;
        [SerializeField, Range(0f, 1f)] private float duckedVolumeScale = 0.45f;

        [Header("音效")]
        [SerializeField] private SfxLibrary sfxLibrary;

        private AudioSource musicPastSource;
        private AudioSource musicPresentSource;
        private AudioSource sfxSource;

        private float pastMixTarget = 1f;
        private bool ducking;
        private SaveSettings settings = new SaveSettings();

        public int InitializeOrder => -40;

        private void Awake()
        {
            EnsureSources();
            ApplyMusicVolumes();
        }

        public void OnBootInitialize()
        {
            ServiceRegistry.Register<IAudioService>(this);
            ApplyVolumeSettings(ServiceRegistry.TryGet<ISaveService>(out var save)
                ? save.LoadSettings()
                : new SaveSettings());
            if (musicPast != null)
            {
                musicPastSource.clip = musicPast;
                musicPastSource.loop = true;
                musicPastSource.Play();
            }
            if (musicPresent != null)
            {
                musicPresentSource.clip = musicPresent;
                musicPresentSource.loop = true;
                musicPresentSource.volume = 0f;
                musicPresentSource.Play();
            }
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<IAudioService>(this);
        }

        private void EnsureSources()
        {
            AudioSource[] existing = GetComponents<AudioSource>();
            if (existing.Length >= 3)
            {
                musicPastSource = existing[0];
                musicPresentSource = existing[1];
                sfxSource = existing[2];
            }
            else
            {
                musicPastSource = gameObject.AddComponent<AudioSource>();
                musicPresentSource = gameObject.AddComponent<AudioSource>();
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
            musicPastSource.playOnAwake = false;
            musicPresentSource.playOnAwake = false;
            sfxSource.playOnAwake = false;
            musicPastSource.loop = true;
            musicPresentSource.loop = true;
            sfxSource.loop = false;
            musicPastSource.spatialBlend = 0f;
            musicPresentSource.spatialBlend = 0f;
            sfxSource.spatialBlend = 0f;
        }

        private void Update()
        {
            // 平滑过渡双年代配器（策划 09）。
            float duckScale = ducking ? duckedVolumeScale : 1f;
            float pastTarget = pastMixTarget * settings.volumeMusic * duckScale;
            float presentTarget = (1f - pastMixTarget) * settings.volumeMusic * duckScale;
            if (musicPastSource != null)
            {
                musicPastSource.volume = Mathf.MoveTowards(musicPastSource.volume, pastTarget, crossfadeSpeed * Time.deltaTime);
            }
            if (musicPresentSource != null)
            {
                musicPresentSource.volume = Mathf.MoveTowards(musicPresentSource.volume, presentTarget, crossfadeSpeed * Time.deltaTime);
            }
        }

        /// <summary>按画面过去面积比例（0~1）混合双年代配器。</summary>
        public void SetEraMix(float pastAreaRatio)
        {
            pastMixTarget = Mathf.Clamp01(pastAreaRatio);
        }

        public void PlaySfx(string sfxId, Vector2? atWorldPosition = null)
        {
            if (sfxLibrary == null || string.IsNullOrEmpty(sfxId))
            {
                return;
            }
            if (!sfxLibrary.TryGetClip(sfxId, out AudioClip clip, out float volume))
            {
                Debug.LogWarning($"[AudioService] 未知音效 id：{sfxId}");
                return;
            }
            float finalVolume = volume * settings.volumeMaster;
            if (atWorldPosition.HasValue && Camera.main != null)
            {
                // 简单距离衰减（2D 横版无需 3D 空间化）。
                float distance = Vector2.Distance(atWorldPosition.Value, Camera.main.transform.position);
                finalVolume *= Mathf.Clamp01(1f - distance * 0.02f);
            }
            sfxSource.PlayOneShot(clip, finalVolume);
        }

        /// <summary>对白期间轻度压低其他声部（策划 09）。</summary>
        public void SetDialogueDucking(bool active)
        {
            ducking = active;
        }

        public void ApplyVolumeSettings(SaveSettings newSettings)
        {
            if (newSettings == null)
            {
                return;
            }
            settings = newSettings;
            AudioListener.volume = settings.volumeMaster;
            ApplyMusicVolumes();
        }

        private void ApplyMusicVolumes()
        {
            // 实际数值在 Update 中持续逼近目标；此处只保证初始不为满值。
        }

        /// <summary>尾声（两天后病房）切换到尾声配乐（策划 09）。</summary>
        public void SwitchToEpilogueMusic()
        {
            if (musicEpilogue == null || musicPastSource == null)
            {
                return;
            }
            musicPastSource.clip = musicEpilogue;
            musicPastSource.volume = settings.volumeMusic;
            musicPastSource.Play();
            if (musicPresentSource != null)
            {
                musicPresentSource.Stop();
            }
            pastMixTarget = 1f;
        }
    }
}
