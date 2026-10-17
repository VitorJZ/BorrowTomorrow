using System.Collections.Generic;
using System.Linq;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowTomorrow.Core
{
    /// <summary>
    /// 启动引导（策划 10.3）：Bootstrap 场景唯一入口。
    /// 收集系统根下全部 IBootInitializable 并按 InitializeOrder 升序初始化，
    /// 随后进入主菜单。系统根常驻，重开关卡不会重复创建音频、输入与事件监听器。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            if (GameSession.Instance != null)
            {
                // 系统根已存在（例如从关卡场景误回到了 Bootstrap 场景），保持唯一实例。
                Debug.LogWarning("[Bootstrap] 系统根已存在，销毁重复对象。");
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);

            // 全场景一次性收集系统组件（仅启动时，不做每帧查找，策划 10.3）。
            var found = new List<IBootInitializable>();
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is IBootInitializable initializable)
                {
                    found.Add(initializable);
                }
            }
            foreach (IBootInitializable item in found.OrderBy(i => i.InitializeOrder))
            {
                item.OnBootInitialize();
            }
        }

        private void Start()
        {
            GameSession.Instance.State.SetPhase(GamePhase.MainMenu);
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
