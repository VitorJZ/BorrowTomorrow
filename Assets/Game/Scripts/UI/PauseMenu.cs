using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 暂停菜单（策划 5.1 Esc：暂停、音量、字幕、返回主菜单）。
    /// 暂停时停止操作、剧情计时与对白推进，恢复后不丢句（策划 5.5）。
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Slider musicVolume;
        [SerializeField] private Slider voiceVolume;
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Toggle screenShakeToggle;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }
            root.SetActive(false);
        }

        private void Start()
        {
            GameSession session = GameSession.Instance;
            if (session != null)
            {
                session.State.SubStateChanged += HandleSubStateChanged;
            }
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Resume);
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
            if (masterVolume != null)
            {
                masterVolume.onValueChanged.AddListener(_ => ApplySettings());
            }
            if (musicVolume != null)
            {
                musicVolume.onValueChanged.AddListener(_ => ApplySettings());
            }
            if (voiceVolume != null)
            {
                voiceVolume.onValueChanged.AddListener(_ => ApplySettings());
            }
            if (subtitlesToggle != null)
            {
                subtitlesToggle.onValueChanged.AddListener(_ => ApplySettings());
            }
            if (screenShakeToggle != null)
            {
                screenShakeToggle.onValueChanged.AddListener(_ => ApplySettings());
            }
            LoadCurrentSettings();
        }

        private void OnDestroy()
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.State.SubStateChanged -= HandleSubStateChanged;
            }
        }

        private void HandleSubStateChanged(PlayingSubState previous, PlayingSubState next)
        {
            root.SetActive(next == PlayingSubState.Paused);
        }

        private void Resume()
        {
            GameSession.Instance?.State.ExitPause();
        }

        private void ReturnToMainMenu()
        {
            GameSession.Instance?.ReturnToMainMenu();
        }

        private void LoadCurrentSettings()
        {
            if (!ServiceRegistry.TryGet<ISaveService>(out var save))
            {
                return;
            }
            SaveSettings settings = save.LoadSettings();
            if (masterVolume != null)
            {
                masterVolume.value = settings.volumeMaster;
            }
            if (musicVolume != null)
            {
                musicVolume.value = settings.volumeMusic;
            }
            if (voiceVolume != null)
            {
                voiceVolume.value = settings.volumeVoice;
            }
            if (subtitlesToggle != null)
            {
                subtitlesToggle.isOn = settings.subtitlesEnabled;
            }
            if (screenShakeToggle != null)
            {
                screenShakeToggle.isOn = settings.screenShakeEnabled;
            }
        }

        /// <summary>音量/字幕即时生效并持久化（重新开始不重置偏好，策划 12.3）。</summary>
        private void ApplySettings()
        {
            if (!ServiceRegistry.TryGet<ISaveService>(out var save))
            {
                return;
            }
            SaveSettings settings = save.LoadSettings();
            if (masterVolume != null)
            {
                settings.volumeMaster = masterVolume.value;
            }
            if (musicVolume != null)
            {
                settings.volumeMusic = musicVolume.value;
            }
            if (voiceVolume != null)
            {
                settings.volumeVoice = voiceVolume.value;
            }
            if (subtitlesToggle != null)
            {
                settings.subtitlesEnabled = subtitlesToggle.isOn;
            }
            if (screenShakeToggle != null)
            {
                settings.screenShakeEnabled = screenShakeToggle.isOn;
            }
            save.SaveSettings(settings);
            if (ServiceRegistry.TryGet<IAudioService>(out var audio))
            {
                audio.ApplyVolumeSettings(settings);
            }
        }
    }
}
