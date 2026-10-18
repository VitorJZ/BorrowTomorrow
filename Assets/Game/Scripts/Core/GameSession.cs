using System.Collections;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;
// 本类的 StoryFlags 属性（StoryFlagStore 类型）遮蔽了同名的常量类，
// 类内引用常量时用本别名（如 StoryFlagIds.IdentityKnown）。
using StoryFlagIds = BorrowTomorrow.Core.StoryFlags;

namespace BorrowTomorrow.Core
{
    /// <summary>
    /// 全局游戏会话（唯一实例，策划 10.4）：
    /// 持有当前关卡、运行状态与剧情进度，协调加载与保存。
    /// 挂在 Bootstrap 场景的系统根上，随根常驻（DontDestroyOnLoad）。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class GameSession : MonoBehaviour, IBootInitializable
    {
        public static GameSession Instance { get; private set; }

        [Header("关卡目录（按通关顺序排列）")]
        [Tooltip("从 L00 到 Epilogue 的完整关卡顺序，供“下一关”推进使用")]
        [SerializeField] private LevelDefinition[] levelCatalog = new LevelDefinition[0];

        [Header("场景")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        public GameStateMachine State { get; } = new GameStateMachine();

        public StoryFlagStore StoryFlags { get; } = new StoryFlagStore();

        /// <summary>当前关卡定义；主菜单或加载中为 null。</summary>
        public LevelDefinition CurrentLevel { get; private set; }

        /// <summary>当前活动房间根。</summary>
        public ILevelRoot ActiveRoom { get; private set; }

        private readonly System.Collections.Generic.List<string> readDialogueIds = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.IReadOnlyList<string> ReadDialogueIds => readDialogueIds;

        public void RecordDialogue(string id)
        {
            if (!string.IsNullOrEmpty(id) && !readDialogueIds.Contains(id)) readDialogueIds.Add(id);
        }

        public string CurrentObjective { get; private set; }

        /// <summary>关卡完成（进下一关或尾声）。</summary>
        public event System.Action<LevelDefinition> LevelCompleted;

        public int InitializeOrder => -100;

        private bool loadRoutineRunning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameSession] 检测到重复实例，已销毁。系统根只应存在于 Bootstrap 场景。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void OnBootInitialize()
        {
            // 订阅剧情标记：关键标记写入后触发即时保存（策划 12.3：重要演出结束保存）。
            StoryFlags.FlagAdded += OnStoryFlagAdded;
            State.SubStateChanged += HandleTimingState;
            State.PhaseChanged += HandleTimingPhase;
        }

        private void HandleTimingState(PlayingSubState previous, PlayingSubState next)
        {
            Time.timeScale = State.IsPaused ? 0f : 1f;
            AudioListener.pause = State.IsPaused;
        }

        private void HandleTimingPhase(GamePhase previous, GamePhase next)
        {
            Time.timeScale = State.IsPaused ? 0f : 1f;
            AudioListener.pause = State.IsPaused;
        }

        private void OnStoryFlagAdded(string flagId)
        {
            // 身份揭示完成后切换任务文本（策划 3.6：任务改为“找到铁盒的位置，告诉晓晓。”）。
            if (flagId == StoryFlagIds.IdentityKnown)
            {
                SetObjective(Objectives.TellXiaoxaoBoxLocation);
            }
            if (State.Phase == GamePhase.Playing)
            {
                SaveProgressNow();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                StoryFlags.FlagAdded -= OnStoryFlagAdded;
                State.SubStateChanged -= HandleTimingState;
                State.PhaseChanged -= HandleTimingPhase;
                Time.timeScale = 1f;
                AudioListener.pause = false;
                Instance = null;
            }
        }

        private void Update()
        {
            // 暂停与恢复检查点入口统一走输入意图（策划 5.1：Esc 暂停 / Backspace 恢复确认）。
            if (State.Phase != GamePhase.Playing || !ServiceRegistry.TryGet<IInputSource>(out var input))
            {
                return;
            }
            if (input.PausePressedThisFrame)
            {
                if (State.IsPaused)
                {
                    State.ExitPause(); // Esc 再按一次恢复（灰盒期无暂停菜单按钮时的退路）
                }
                else
                {
                    State.EnterPause();
                }
            }
            // 恢复确认框由 UI 呈现并再次确认（策划 4.6），此处只负责状态受理。
        }

        /// <summary>开始新游戏：清空剧情进度并从第一关开始。</summary>
        public void StartNewGame()
        {
            if (loadRoutineRunning) return;
            StoryFlags.Clear();
            readDialogueIds.Clear();
            if (ServiceRegistry.TryGet<ICheckpointService>(out var checkpoints))
            {
                checkpoints.RestoreFrom(null);
            }
            if (levelCatalog.Length == 0)
            {
                Debug.LogError("[GameSession] 关卡目录为空，无法开始游戏。");
                return;
            }
            LoadLevel(levelCatalog[0]);
        }

        /// <summary>继续游戏：读取存档并恢复到对应关卡与检查点。</summary>
        public void ContinueGame()
        {
            if (loadRoutineRunning) return;
            if (!ServiceRegistry.TryGet<ISaveService>(out var save))
            {
                Debug.LogError("[GameSession] SaveService 未装配。");
                return;
            }
            SaveLoadResult result = save.TryLoadProgress(out SaveData data);
            switch (result)
            {
                case SaveLoadResult.Loaded:
                case SaveLoadResult.UsedBackup:
                    RestoreFromSave(data, result == SaveLoadResult.UsedBackup);
                    break;
                case SaveLoadResult.Corrupted:
                    Debug.LogWarning("[GameSession] 存档损坏且无可用备份，等待玩家选择重新开始。");
                    break;
                default:
                    // 存档缺失或不可用：从新游戏开始（策划 12.3）；损坏情况由菜单 UI 提供选择。
                    StartNewGame();
                    break;
            }
        }

        private void RestoreFromSave(SaveData data, bool usedBackup)
        {
            if (data == null)
            {
                StartNewGame();
                return;
            }
            StoryFlags.RestoreFrom(data.triggeredStoryIds);
            readDialogueIds.Clear();
            if (data.readDialogueIds != null)
                foreach (string id in data.readDialogueIds) RecordDialogue(id);
            // Before the sewing-room split, its checkpoint belonged to L02 and
            // used world coordinates 40..58. Keep existing interior saves usable.
            if (data.currentLevelId == "L02" && data.checkpointRoomId == "L02Indoor")
            {
                data.currentLevelId = "L02Indoor";
                data.roomBoundaries.RemoveAll(r => r.roomId != "L02Indoor");
                foreach (var room in data.roomBoundaries) room.boundaryX -= 40f;
                if (data.hasCheckpointPosition) data.checkpointPosition.x -= 40f;
                data.mechanismStates.Clear();
            }
            LevelDefinition target = FindLevel(data.currentLevelId);
            // Preserve v1.3 handover when resuming between the final dialogue and scene transition.
            if (target != null && target.levelId == "L04" && StoryFlags.HasFlag(StoryFlagIds.BoxLocated))
            {
                target = FindLevel("Epilogue");
                data.currentLevelId = "Epilogue";
                data.checkpointId = null;
                data.checkpointRoomId = null;
                data.hasCheckpointPosition = false;
                data.roomBoundaries.Clear();
                data.mechanismStates.Clear();
                data.currentObjective = null;
            }

            if (target == null)
            {
                Debug.LogError($"[GameSession] 存档引用了未知关卡 {data.currentLevelId}，改为新开局。");
                StartNewGame();
                return;
            }
            if (ServiceRegistry.TryGet<ICheckpointService>(out var checkpoints))
            {
                checkpoints.RestoreFrom(data);
            }
            if (usedBackup)
            {
                Debug.LogWarning("[GameSession] 主存档损坏，已从备份恢复。");
            }
            CurrentObjective = string.IsNullOrEmpty(data.currentObjective) ? target.initialObjective : data.currentObjective;
            LoadLevel(target, restoreCheckpoint: true);
        }

        /// <summary>加载关卡。restoreCheckpoint=true 时在关卡就绪后应用检查点快照。</summary>
        public void LoadLevel(LevelDefinition definition, bool restoreCheckpoint = false)
        {
            if (loadRoutineRunning)
            {
                Debug.LogWarning("[GameSession] 已有加载流程进行中，忽略重复请求。");
                return;
            }
            StartCoroutine(LoadLevelRoutine(definition, restoreCheckpoint));
        }

        private IEnumerator LoadLevelRoutine(LevelDefinition definition, bool restoreCheckpoint)
        {
            loadRoutineRunning = true;
            ActiveRoom = null;
            State.SetPhase(GamePhase.Loading);

            // 转场淡出（FadeScreen 由 HUD 提供，策划 4.6 转场共用的淡入淡出层）。
            if (ServiceRegistry.TryGet<IHudService>(out var fadeHud))
            {
                fadeHud.FadeOut(0.35f);
            }
            yield return new WaitForSeconds(0.35f);

            AsyncOperation op = SceneManager.LoadSceneAsync(definition.sceneName);
            if (op == null)
            {
                Debug.LogError($"[GameSession] 无法加载场景 {definition.sceneName}。");
                State.SetPhase(GamePhase.MainMenu);
                loadRoutineRunning = false;
                yield break;
            }
            while (!op.isDone)
            {
                yield return null;
            }

            CurrentLevel = definition;
            // Old-scene triggers may still run during fade-out; clear only after unloading it.
            if (!restoreCheckpoint && ServiceRegistry.TryGet<ICheckpointService>(out var previousCheckpoints))
            {
                previousCheckpoints.RestoreFrom(null);
            }


            // 等待房间根注册（LevelRoot 在 Awake/Start 中注册自身）。
            float timeout = 5f;
            while (!ServiceRegistry.TryGet<ILevelRoot>(out _) && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (!ServiceRegistry.TryGet<ILevelRoot>(out ILevelRoot root))
            {
                Debug.LogError($"[GameSession] 场景 {definition.sceneName} 中未找到 LevelRoot。");
                loadRoutineRunning = false;
                yield break;
            }

            // 加载顺序遵循策划 12.2：场景 → 边界与年代状态 → 机关 → 角色 → 目标 → 允许输入。
            ActiveRoom = root;
            if (restoreCheckpoint && ServiceRegistry.TryGet<ICheckpointService>(out var checkpoints)
                && checkpoints.HasCheckpoint)
            {
                checkpoints.RestoreLatest();
            }
            else
            {
                root.PlacePlayer(definition.spawnId);
                SetObjective(definition.initialObjective);
                if (ServiceRegistry.TryGet<ICheckpointService>(out var entryCheckpoint))
                    entryCheckpoint.Capture(definition.spawnId);
            }

            if (ServiceRegistry.TryGet<IHudService>(out var fadeInHud))
            {
                fadeInHud.FadeIn(0.5f);
            }
            State.SetPhase(GamePhase.Playing);
            loadRoutineRunning = false;
        }

        /// <summary>同场景房间转场（如 L01 水渠房 → 树房）。策划 7.7：出口经 E 确认转场。</summary>
        public void TransitionToRoom(ILevelRoot targetRoom, string spawnId)
        {
            if (State.IsPlayerControlAllowed)
                StartCoroutine(TransitionRoutine(targetRoom, spawnId));
        }

        private IEnumerator TransitionRoutine(ILevelRoot targetRoom, string spawnId)
        {
            if (targetRoom == null || targetRoom.IsActiveRoom)
            {
                yield break;
            }
            State.SetSubState(PlayingSubState.Cutscene); // 转场期间暂停主动操作
            if (ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                hud.FadeOut(0.25f);
            }
            yield return new WaitForSeconds(0.25f);

            if (ActiveRoom != null && ServiceRegistry.TryGet<ICheckpointService>(out var history))
                history.RememberRoomBoundary(ActiveRoom.RoomId, ActiveRoom.CurrentBoundary);
            ActiveRoom?.DeactivateRoom();
            ActiveRoom = targetRoom;
            targetRoom.ActivateRoom();
            targetRoom.PlacePlayer(spawnId);
            if (ServiceRegistry.TryGet<ICheckpointService>(out var roomCheckpoint))
                roomCheckpoint.Capture(spawnId);

            yield return new WaitForSeconds(0.1f); // 等物理与相机稳定
            if (hud != null)
            {
                hud.FadeIn(0.25f);
            }
            State.SetSubState(PlayingSubState.Free);
        }

        /// <summary>恢复空间快照前同步房间服务、相机和会话身份。</summary>
        public ILevelRoot ActivateRoomForRestore(string roomId)
        {
            foreach (MonoBehaviour component in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (!(component is ILevelRoot room) || room.RoomId != roomId
                    || component.gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (!ReferenceEquals(ActiveRoom, room))
                {
                    ActiveRoom?.DeactivateRoom();
                    ActiveRoom = room;
                    room.ActivateRoom();
                }
                return room;
            }
            return string.IsNullOrEmpty(roomId) ? ActiveRoom : null;
        }

        /// <summary>关卡完成：保存进度并推进到下一关（末关进入尾声）。</summary>
        public void CompleteLevel()
        {
            LevelDefinition next = FindNextLevel(CurrentLevel);
            SaveProgressNow(); // 策划 12.3：离开关卡时保存
            LevelCompleted?.Invoke(CurrentLevel);
            if (next != null)
            {
                LoadLevel(next);
            }
            else
            {
                // 末关（尾声）完成：进入制作名单，短暂停留后回到主菜单（策划 10.6）。
                Debug.Log("[GameSession] 已完成最后一个关卡，进入制作名单。");
                State.SetPhase(GamePhase.Credits);
                StartCoroutine(ReturnFromCreditsRoutine());
            }
        }

        private IEnumerator ReturnFromCreditsRoutine()
        {
            yield return new WaitForSeconds(4f);
            if (State.Phase != GamePhase.Credits)
            {
                yield break; // 期间被暂停菜单等其他流程打断则不覆盖
            }
            CurrentLevel = null;
            ActiveRoom = null;
            CurrentObjective = null;
            State.SetPhase(GamePhase.MainMenu);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>设置当前目标并广播到 HUD（策划 5.4：左上一行目标）。</summary>
        public void SetObjective(string objective)
        {
            CurrentObjective = objective;
            if (ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                if (string.IsNullOrEmpty(objective))
                {
                    hud.HideObjective();
                }
                else
                {
                    hud.ShowObjective(objective);
                }
            }
        }

        /// <summary>跌落恢复请求（KillZone 调用）。策划 4.6：淡出约 0.3 秒回到最近检查点。</summary>
        public void RequestRespawnFromFall()
        {
            if (State.Phase != GamePhase.Playing || State.SubState == PlayingSubState.Respawning)
            {
                return;
            }
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            State.SetSubState(PlayingSubState.Respawning);
            if (ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                hud.HideSubtitle();
                hud.SetInteractionPrompt(false);
                hud.FadeOut(0.3f); // 策划 4.6：跌落淡出约 0.3 秒
            }
            yield return new WaitForSeconds(0.3f);
            if (ServiceRegistry.TryGet<ICheckpointService>(out var checkpoints))
            {
                checkpoints.RestoreLatest();
            }
            else
            {
                Debug.LogError("[GameSession] CheckpointService 未装配，无法恢复。");
            }
            if (hud != null)
            {
                hud.FadeIn(0.2f); // 淡入缓冲
            }
            yield return new WaitForSeconds(0.2f);
            // 已读剧情标记保留，不重复播放（策划 4.6）。
            State.SetSubState(PlayingSubState.Free);
        }

        /// <summary>
        /// 玩家确认恢复检查点（ResumeConfirmDialog 第二次确认后调用，策划 5.1/4.6）。
        /// </summary>
        public void ConfirmCheckpointRestore()
        {
            if (!State.IsPlayerControlAllowed)
            {
                return;
            }
            StartCoroutine(RespawnRoutine());
        }

        /// <summary>回主菜单（暂停菜单调用）。保存后再离开。</summary>
        public void ReturnToMainMenu()
        {
            StopAllCoroutines();
            loadRoutineRunning = false;
            SaveProgressNow();
            CurrentLevel = null;
            ActiveRoom = null;
            CurrentObjective = null;
            State.SetPhase(GamePhase.MainMenu);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        [ContextMenu("Debug/Save Now")]
        public void SaveProgressNow()
        {
            if (CurrentLevel == null || !ServiceRegistry.TryGet<ISaveService>(out var save))
            {
                return;
            }
            var data = new SaveData
            {
                currentLevelId = CurrentLevel.levelId,
                currentObjective = CurrentObjective,
            };
            if (ServiceRegistry.TryGet<ICheckpointService>(out var checkpoints))
            {
                checkpoints.WriteTo(data);
            }
            data.triggeredStoryIds.AddRange(StoryFlags.AllFlags);
            data.readDialogueIds.AddRange(readDialogueIds);
            if (ServiceRegistry.TryGet<ISaveService>(out var saveService))
            {
                saveService.SaveProgress(data);
            }
        }

        public LevelDefinition FindLevel(string levelId)
        {
            foreach (LevelDefinition def in levelCatalog)
            {
                if (def != null && def.levelId == levelId)
                {
                    return def;
                }
            }
            return null;
        }

        private LevelDefinition FindNextLevel(LevelDefinition current)
        {
            if (current == null)
            {
                return levelCatalog.Length > 0 ? levelCatalog[0] : null;
            }
            for (int i = 0; i < levelCatalog.Length; i++)
            {
                if (levelCatalog[i] == current && i + 1 < levelCatalog.Length)
                {
                    return levelCatalog[i + 1];
                }
            }
            return null;
        }
    }
}
