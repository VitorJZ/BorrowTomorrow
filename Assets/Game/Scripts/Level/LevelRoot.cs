using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 房间根（策划 10.3 LevelRoot）：提供 PlayerSpawn、CameraBounds、
    /// BoundaryRange、Interactables、Checkpoints、Exit。
    /// 一个关卡场景可含多个房间（如 L01 水渠房 / 树房），每间房保存自己的边界位置（策划 4.2）。
    /// 激活房间时向系统注册 ILevelRoot / ILevelContext / IBoundaryContext / ICameraRig。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelRoot : MonoBehaviour, ILevelRoot
    {
        [Header("关卡数据")]
        [SerializeField] private LevelDefinition definition;

        [Tooltip("房间稳定 id（如 L01A、L01B；单房间关卡与 levelId 相同）")]
        [SerializeField] private string roomId = "room";

        [Header("房间范围（覆盖 LevelDefinition 的默认边界配置）")]
        [SerializeField] private bool overrideBoundaryRange = false;
        [SerializeField] private float boundaryMin = 0f;
        [SerializeField] private float boundaryMax = 32f;
        [SerializeField] private float initialBoundary = 32f;

        [Header("空间")]
        [Tooltip("相机可活动范围（世界坐标矩形）")]
        [SerializeField] private Rect cameraBounds = new Rect(0f, -6f, 32f, 12f);

        [Header("装配")]
        [SerializeField] private BoundaryController boundaryController;
        [SerializeField] private TemporalVisualController visualController;
        [SerializeField] private SpawnPoint[] spawnPoints = new SpawnPoint[0];

        [Tooltip("玩家预制体（仅场景中尚无玩家时实例化）")]
        [SerializeField] private GameObject playerPrefab;

        [Tooltip("进入场景时本房间是否为活动房间")]
        [SerializeField] private bool initiallyActive = true;

        public LevelDefinition Definition => definition;

        public string RoomId => roomId;

        public bool IsActiveRoom { get; private set; }

        private void Awake()
        {
            IsActiveRoom = false;
            if (initiallyActive)
            {
                ActivateRoom();
            }
        }

        private void OnDestroy()
        {
            if (ServiceRegistry.TryGet<ILevelRoot>(out var registered) && ReferenceEquals(registered, this))
            {
                ServiceRegistry.Unregister<ILevelRoot>(this);
            }
        }

        /// <summary>激活房间：注册服务、配置边界与相机（由 GameSession 转场时调用）。</summary>
        public void ActivateRoom()
        {
            IsActiveRoom = true;
            ServiceRegistry.Register<ILevelRoot>(this);

            float min = overrideBoundaryRange ? boundaryMin : (definition != null ? definition.boundaryMin : 0f);
            float max = overrideBoundaryRange ? boundaryMax : (definition != null ? definition.boundaryMax : 32f);
            float initial = overrideBoundaryRange ? initialBoundary
                : (definition != null ? definition.initialBoundary : 32f);

            if (boundaryController != null)
            {
                boundaryController.RegisterAsActive();
                boundaryController.ConfigureRoom(min, max, initial, roomId);
            }
            if (visualController != null)
            {
                visualController.Bind(boundaryController);
            }
            if (ServiceRegistry.TryGet<ICameraRig>(out var cameraRig))
            {
                cameraRig.SetRoomBounds(cameraBounds);
            }
            // 过门转场后重新标清两侧年代（策划 4.2）：按房间中线相对边界的年代给角标。
            if (ServiceRegistry.TryGet<IHudService>(out var hud) && boundaryController != null)
            {
                float midX = (min + max) * 0.5f;
                hud.FlashEraBadge(boundaryController.EraAtAnchor(midX));
            }
        }

        /// <summary>停用房间：注销服务（玩家与系统对象随之离开本房间）。</summary>
        public void DeactivateRoom()
        {
            IsActiveRoom = false;
            if (ServiceRegistry.TryGet<ILevelRoot>(out var registered) && ReferenceEquals(registered, this))
            {
                ServiceRegistry.Unregister<ILevelRoot>(this);
            }
        }

        /// <summary>按出生点 id 放置玩家（策划 12.2：环境恢复完成后再放置角色）。</summary>
        public void PlacePlayer(string spawnId)
        {
            SpawnPoint target = FindSpawnInternal(spawnId);
            if (target == null)
            {
                Debug.LogWarning($"[LevelRoot] 房间 {roomId} 中未找到出生点 {spawnId}，使用第一个。");
                target = spawnPoints.Length > 0 ? spawnPoints[0] : null;
            }
            if (target == null)
            {
                Debug.LogError($"[LevelRoot] 房间 {roomId} 没有任何出生点。");
                return;
            }

            if (!ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsPresent)
            {
                if (playerPrefab == null)
                {
                    Debug.LogError($"[LevelRoot] 场景中无玩家且未配置玩家预制体，无法放置。");
                    return;
                }
                Instantiate(playerPrefab);
            }

            if (ServiceRegistry.TryGet<IPlayerContext>(out var placed) && placed.IsPresent)
            {
                placed.TeleportTo(target);
            }
        }

        /// <summary>按出生点 id 查找出生点；不存在返回 null（接口成员）。</summary>
        public Core.Interfaces.ISpawnPoint FindSpawn(string spawnId)
        {
            return FindSpawnInternal(spawnId);
        }

        private SpawnPoint FindSpawnInternal(string spawnId)
        {
            foreach (SpawnPoint point in spawnPoints)
            {
                if (point != null && point.SpawnId == spawnId)
                {
                    return point;
                }
            }
            return null;
        }

        /// <summary>本房间当前的边界位置（供检查点快照采集）。</summary>
        public float CurrentBoundary => boundaryController != null ? boundaryController.CommittedBoundary : 0f;

        /// <summary>存档恢复用：按稳定 objectId 应用年代物体状态（策划 12.2）。</summary>
        public void ApplyMechanismState(string objectId, Core.Data.Era era)
        {
            TemporalObject obj = FindRegistry()?.FindById(objectId);
            if (obj != null)
            {
                obj.ApplyEra(era, immediate: true);
            }
        }

        /// <summary>采集本房间全部年代物体当前状态（检查点快照，策划 12.2）。</summary>
        public System.Collections.Generic.IReadOnlyList<Core.Data.MechanismStateSnapshot> CollectMechanismStates()
        {
            var result = new System.Collections.Generic.List<Core.Data.MechanismStateSnapshot>();
            TemporalObjectRegistry registry = FindRegistry();
            if (registry == null)
            {
                return result;
            }
            foreach (TemporalObject obj in registry.Objects)
            {
                result.Add(new Core.Data.MechanismStateSnapshot(obj.ObjectId, obj.CurrentEra));
            }
            return result;
        }

        private TemporalObjectRegistry FindRegistry()
        {
            return registryCache ?? (registryCache = GetComponentInChildren<TemporalObjectRegistry>());
        }

        private TemporalObjectRegistry registryCache;

        public Core.Interfaces.ISpawnPoint FindSpawnById(string spawnId)
        {
            return FindSpawn(spawnId);
        }
    }
}
