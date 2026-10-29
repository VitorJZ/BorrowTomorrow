using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 主菜单（策划 7.7）：首次流程提供“继续游戏”和“重新开始”；
    /// 章节选择在通关后开放。开场不显示任何剧透信息（策划 3.2）。
    /// </summary>
    public sealed class MainMenuUi : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject corruptedWarning;
        [SerializeField] private AudioClip clickClip;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.45f;

        private void Start()
        {
            if (continueButton != null)
            {
                continueButton.interactable = ServiceRegistry.TryGet<ISaveService>(out var save) && save.HasProgressSave();
                continueButton.onClick.AddListener(PlayClick);
                continueButton.onClick.AddListener(OnContinue);
            }
            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(PlayClick);
                newGameButton.onClick.AddListener(OnNewGame);
            }
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(PlayClick);
                quitButton.onClick.AddListener(QuitGame);
            }
            if (corruptedWarning != null)
            {
                corruptedWarning.SetActive(false);
            }
        }

        /// <summary>继续游戏（公开方法：供主菜单按钮 onClick 绑定）。</summary>
        public void OnContinue()
        {
            if (ServiceRegistry.TryGet<ISaveService>(out var save))
            {
                // 策划 12.3：无法解析时不静默覆盖唯一记录——损坏时提示后由玩家选择重新开始。
                SaveLoadResult result = save.TryLoadProgress(out _);
                if (result == SaveLoadResult.Corrupted)
                {
                    ShowCorruptedWarning();
                    if (continueButton != null)
                    {
                        continueButton.interactable = false;
                    }
                    return;
                }
            }
            GameSession.Instance?.ContinueGame();
        }

        private void ShowCorruptedWarning()
        {
            if (corruptedWarning == null)
            {
                corruptedWarning = new GameObject("CorruptedSaveWarning", typeof(RectTransform), typeof(Text));
                var rect = (RectTransform)corruptedWarning.transform;
                // 跟随 16:9 舞台缩放，位置在三个按钮下方，不遮挡按钮。
                Transform stage = transform.Find("MenuStage");
                rect.SetParent(stage != null ? stage : transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -500f);
                rect.sizeDelta = new Vector2(900f, 70f);
                var text = corruptedWarning.GetComponent<Text>();
                var label = newGameButton != null ? newGameButton.GetComponentInChildren<Text>() : null;
                text.font = label != null ? label.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(1f, 0.9f, 0.75f);
                text.raycastTarget = false;
                text.text = "存档无法读取，且无可用备份。原文件已保留。\n请选择“开始新游戏”重新开始。";
            }
            corruptedWarning.SetActive(true);
        }

        /// <summary>开始新游戏（公开方法：供主菜单按钮 onClick 绑定）。</summary>
        public void OnNewGame()
        {
            GameSession.Instance?.StartNewGame();
        }

        /// <summary>
        /// 主菜单按钮的木质点击声。挂在常驻对象上，
        /// 这样切场景/退出时点击声不会被随场景销毁的 AudioSource 截断。
        /// </summary>
        private void PlayClick()
        {
            if (clickClip == null)
            {
                return;
            }
            var player = new GameObject("MainMenuClick");
            player.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(player);
            var source = player.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = clickClip;
            source.volume = clickVolume;
            source.Play();
            Destroy(player, clickClip.length + 0.15f);
        }

        /// <summary>退出游戏（公开方法：供主菜单按钮 onClick 绑定）。</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
