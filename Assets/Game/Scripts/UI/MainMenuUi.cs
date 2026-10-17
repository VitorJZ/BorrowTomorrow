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

        private void Start()
        {
            if (continueButton != null)
            {
                continueButton.interactable = ServiceRegistry.TryGet<ISaveService>(out var save) && save.HasProgressSave();
                continueButton.onClick.AddListener(OnContinue);
            }
            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(OnNewGame);
            }
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
            if (corruptedWarning != null)
            {
                corruptedWarning.SetActive(false);
            }
        }

        private void OnContinue()
        {
            if (ServiceRegistry.TryGet<ISaveService>(out var save))
            {
                // 策划 12.3：无法解析时不静默覆盖唯一记录——损坏时提示后由玩家选择重新开始。
                SaveLoadResult result = save.TryLoadProgress(out _);
                if (result == SaveLoadResult.Corrupted && corruptedWarning != null)
                {
                    corruptedWarning.SetActive(true);
                    if (continueButton != null)
                    {
                        continueButton.interactable = false;
                    }
                    return;
                }
            }
            GameSession.Instance?.ContinueGame();
        }

        private void OnNewGame()
        {
            GameSession.Instance?.StartNewGame();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
