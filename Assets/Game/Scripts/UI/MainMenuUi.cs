using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using BorrowTomorrow.Core.Data;
using UnityEngine.EventSystems;
#endif

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 开发调试：在主菜单依次按下该口令的按键打开快速选关。发布版不会编译这段功能。
        // 用 KeyCode 而不是 Input.inputString，避免中文输入法组合输入吞掉字母。
        private static readonly KeyCode[] LevelSelectKeys =
        {
            KeyCode.C, KeyCode.H, KeyCode.R, KeyCode.I, KeyCode.S, KeyCode.U,
        };
        private int codeProgress;
        private GameObject levelSelectRoot;
        private GameObject levelSelectNotice;
#endif

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 开发调试：在主菜单依次按下 C-H-R-I-S-U 打开快速选关。
        /// 只在编辑器与开发版构建中编译，避免玩家在发布版触发。
        /// </summary>
        private void Update()
        {
            if (levelSelectRoot != null && levelSelectRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseLevelSelect();
                }
                return;
            }

            for (int i = 0; i < LevelSelectKeys.Length; i++)
            {
                if (!Input.GetKeyDown(LevelSelectKeys[i]))
                {
                    continue;
                }
                // 按对下一个字母则前进；按错则重置（按的是 C 就重新开始）。
                codeProgress = i == codeProgress ? i + 1 : (i == 0 ? 1 : 0);
                break;
            }

            if (codeProgress >= LevelSelectKeys.Length)
            {
                codeProgress = 0;
                Debug.Log("[Dev] 已输入快速选关口令。");
                OpenLevelSelect();
            }
        }

        private void OpenLevelSelect()
        {
            GameSession session = GameSession.Instance;
            if (session == null)
            {
                ShowLevelSelectNotice("快速选关不可用：请从 Bootstrap 场景进入游戏（直接播放 MainMenu 时没有 GameSession）。");
                return;
            }
            if (session.State.Phase != GamePhase.MainMenu)
            {
                ShowLevelSelectNotice($"快速选关只能在主菜单打开（当前阶段：{session.State.Phase}）。");
                return;
            }

            if (levelSelectRoot == null)
            {
                BuildLevelSelect(session.LevelCatalog);
            }
            if (levelSelectRoot == null)
            {
                return;
            }

            levelSelectRoot.SetActive(true);
            levelSelectRoot.transform.SetAsLastSibling();
            if (EventSystem.current != null)
            {
                // 清空选中项，避免回车误触背后的主菜单按钮。
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void CloseLevelSelect()
        {
            if (levelSelectRoot != null)
            {
                levelSelectRoot.SetActive(false);
            }
        }

        /// <summary>口令输入成功但暂时无法打开选关时，给出屏幕提示而不是静默失败。</summary>
        private void ShowLevelSelectNotice(string message)
        {
            if (levelSelectNotice == null)
            {
                levelSelectNotice = new GameObject("LevelSelectNotice", typeof(RectTransform), typeof(Text));
                var rect = (RectTransform)levelSelectNotice.transform;
                Transform stage = transform.Find("MenuStage");
                rect.SetParent(stage != null ? stage : transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -500f);
                rect.sizeDelta = new Vector2(1400f, 90f);

                var text = levelSelectNotice.GetComponent<Text>();
                text.font = ResolveMenuFont();
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(1f, 0.82f, 0.6f, 1f);
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }

            levelSelectNotice.GetComponent<Text>().text = message;
            levelSelectNotice.SetActive(true);
            CancelInvoke(nameof(HideLevelSelectNotice));
            Invoke(nameof(HideLevelSelectNotice), 5f);
        }

        private void HideLevelSelectNotice()
        {
            if (levelSelectNotice != null)
            {
                levelSelectNotice.SetActive(false);
            }
        }

        private void JumpToLevel(LevelDefinition definition)
        {
            if (definition == null)
            {
                return;
            }
            CloseLevelSelect();
            GameSession session = GameSession.Instance;
            if (session != null && session.TryLoadLevel(definition.levelId))
            {
                Debug.Log($"[Dev] 快速选关 → {definition.levelId}（{definition.sceneName}）");
            }
        }

        private void BuildLevelSelect(IReadOnlyList<LevelDefinition> catalog)
        {
            if (catalog == null || catalog.Count == 0)
            {
                Debug.LogWarning("[MainMenuUi] 关卡目录为空，无法打开快速选关。");
                return;
            }

            Button template = newGameButton != null
                ? newGameButton
                : (continueButton != null ? continueButton : quitButton);
            if (template == null)
            {
                Debug.LogWarning("[MainMenuUi] 找不到可复用的菜单按钮，无法构建快速选关。");
                return;
            }

            // 跟随 16:9 舞台缩放，和三个主菜单按钮保持一致的对齐方式。
            Transform stage = transform.Find("MenuStage");
            Transform parent = stage != null ? stage : transform;

            levelSelectRoot = new GameObject("LevelSelectOverlay", typeof(RectTransform), typeof(Image));
            var overlayRect = (RectTransform)levelSelectRoot.transform;
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = Vector2.zero;
            var overlayImage = levelSelectRoot.GetComponent<Image>();
            overlayImage.color = new Color(0.06f, 0.05f, 0.04f, 0.96f);
            overlayImage.raycastTarget = true;

            Font font = ResolveMenuFont();
            CreateLabel(overlayRect, "Title", "快速选关", 430f, new Vector2(1000f, 80f), 54,
                new Color(1f, 0.92f, 0.76f, 1f), font);
            CreateLabel(overlayRect, "Hint", "开发调试功能 · Esc 返回", 358f, new Vector2(1000f, 36f), 24,
                new Color(0.82f, 0.76f, 0.66f, 0.85f), font);

            const int columns = 3;
            const float buttonWidth = 500f;
            const float buttonHeight = 150f;
            const float gapX = 60f;
            const float gapY = 30f;
            const float firstRowY = 185f;

            for (int i = 0; i < catalog.Count; i++)
            {
                LevelDefinition definition = catalog[i];
                if (definition == null)
                {
                    continue;
                }

                int row = i / columns;
                int column = i % columns;
                int countInRow = Mathf.Min(columns, catalog.Count - row * columns);
                float rowWidth = countInRow * buttonWidth + (countInRow - 1) * gapX;
                float x = -rowWidth * 0.5f + buttonWidth * 0.5f + column * (buttonWidth + gapX);
                float y = firstRowY - row * (buttonHeight + gapY);

                Button button = CreateLevelButton(template, overlayRect, definition,
                    new Vector2(x, y), new Vector2(buttonWidth, buttonHeight), 20, 40);
                if (button != null)
                {
                    LevelDefinition target = definition;
                    button.onClick.AddListener(PlayClick);
                    button.onClick.AddListener(() => JumpToLevel(target));
                }
            }

            Button closeButton = CreateLevelButton(template, overlayRect, null,
                new Vector2(0f, -385f), new Vector2(360f, 108f), 28, 46);
            if (closeButton != null)
            {
                var closeVisual = (RectTransform)closeButton.transform.Find("Visual");
                if (closeVisual != null)
                {
                    closeVisual.sizeDelta = new Vector2(346f, 102f);
                }
                closeButton.onClick.AddListener(PlayClick);
                closeButton.onClick.AddListener(CloseLevelSelect);
            }
        }

        private Button CreateLevelButton(Button template, Transform parent, LevelDefinition definition,
            Vector2 position, Vector2 size, int minFontSize, int maxFontSize)
        {
            Button button = Instantiate(template, parent);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            // 克隆只保留外观；点击回调由调用方重新绑定。
            button.onClick = new Button.ButtonClickedEvent();

            button.gameObject.name = definition != null ? "Button_" + definition.levelId : "Button_返回";
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = definition != null
                    ? definition.levelId + " · " + definition.displayName
                    : "返回";
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = minFontSize;
                label.resizeTextMaxSize = maxFontSize;
            }
            return button;
        }

        private void CreateLabel(Transform parent, string name, string content, float y, Vector2 size,
            int fontSize, Color color, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private Font ResolveMenuFont()
        {
            Button source = newGameButton != null
                ? newGameButton
                : (continueButton != null ? continueButton : quitButton);
            Text label = source != null ? source.GetComponentInChildren<Text>(true) : null;
            return label != null && label.font != null
                ? label.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
#endif

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
