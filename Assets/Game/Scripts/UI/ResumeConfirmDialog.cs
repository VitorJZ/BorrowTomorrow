using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 恢复确认框（策划 4.6 / 5.1）：按 Backspace 显示确认，再次确认恢复本关最近检查点；
    /// 恢复操作必须经过确认。
    /// </summary>
    public sealed class ResumeConfirmDialog : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }
            root.SetActive(false);
        }

        private void Update()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing)
            {
                root.SetActive(false);
                return;
            }
            if (!ServiceRegistry.TryGet<IInputSource>(out var input))
            {
                return;
            }
            if (session.State.IsPaused)
            {
                return; // 暂停中不处理恢复
            }

            if (input.RespawnPressedThisFrame)
            {
                if (root.activeSelf)
                {
                    // 第二次 Backspace：确认恢复（策划 4.6）。
                    root.SetActive(false);
                    session.ConfirmCheckpointRestore();
                }
                else
                {
                    root.SetActive(true);
                }
            }
            else if (root.activeSelf)
            {
                // Esc 或交互取消确认。
                if (input.PausePressedThisFrame || input.InteractPressedThisFrame)
                {
                    root.SetActive(false);
                }
            }
        }
    }
}
