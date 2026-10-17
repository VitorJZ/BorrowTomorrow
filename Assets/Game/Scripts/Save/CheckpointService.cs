using System.Collections.Generic;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Save
{
    /// <summary>
    /// 检查点服务（策划 10.4 CheckpointService / 12.2）：
    /// 采集并恢复空间快照。
    /// 恢复顺序严格遵循策划 12.2：
    /// 场景 → 边界与年代状态 → 机关 → 角色（先恢复环境再放人，防夹住）→ 目标 → 允许输入。
    /// 空间快照与剧情阅读进度分开：跌落恢复空间，不回滚已经看过的故事（策划 12.2）。
    /// </summary>
    public sealed class CheckpointService : MonoBehaviour, ICheckpointService, IBootInitializable
    {
        /// <summary>空间快照（普通可序列化结构，策划 10.5）。</summary>
        [System.Serializable]
        private sealed class Snapshot
        {
            public string checkpointId;
            public string levelId;
            public string roomId;
            public float boundaryX;
            public string objective;
            public List<MechanismStateSnapshot> mechanisms = new List<MechanismStateSnapshot>();
            public List<RoomBoundarySnapshot> roomBoundaries = new List<RoomBoundarySnapshot>();
            public Vector2 fallbackPlayerPosition;
        }

        private Snapshot latest;
        private Snapshot pendingRestore;

        /// <summary>跨房间边界历史（策划 4.2：每间房保存自己的边界位置）。</summary>
        private readonly Dictionary<string, float> roomBoundaryHistory = new Dictionary<string, float>();

        public bool HasCheckpoint => latest != null || pendingRestore != null;

        public string CurrentCheckpointId => latest != null ? latest.checkpointId : string.Empty;

        public int InitializeOrder => -25;

        public void OnBootInitialize()
        {
            ServiceRegistry.Register<ICheckpointService>(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<ICheckpointService>(this);
        }

        /// <summary>采集当前空间快照并设为最近检查点。</summary>
        public void Capture(string checkpointId)
        {
            GameSession session = GameSession.Instance;
            ILevelRoot room = ServiceRegistry.TryGet<ILevelRoot>(out ILevelRoot active) ? active : null;
            if (session == null || room == null)
            {
                return;
            }

            var snapshot = new Snapshot
            {
                checkpointId = checkpointId,
                levelId = session.CurrentLevel != null ? session.CurrentLevel.levelId : string.Empty,
                roomId = room.RoomId,
                boundaryX = room.CurrentBoundary,
                objective = session.CurrentObjective,
            };

            // 机关年代状态（以稳定 objectId 采集，策划 12.2）。
            snapshot.mechanisms.Clear();
            var states = room.CollectMechanismStates();
            foreach (MechanismStateSnapshot state in states)
            {
                snapshot.mechanisms.Add(state);
            }

            // 记录每个房间的边界位置历史。
            snapshot.roomBoundaries.Clear();
            foreach (KeyValuePair<string, float> pair in roomBoundaryHistory)
            {
                snapshot.roomBoundaries.Add(new RoomBoundarySnapshot(pair.Key, pair.Value));
            }
            snapshot.roomBoundaries.RemoveAll(r => r.roomId == room.RoomId);
            snapshot.roomBoundaries.Add(new RoomBoundarySnapshot(room.RoomId, room.CurrentBoundary));

            if (ServiceRegistry.TryGet<IPlayerContext>(out var player) && player.IsPresent)
            {
                snapshot.fallbackPlayerPosition = player.Position;
            }

            latest = snapshot;
            roomBoundaryHistory[room.RoomId] = room.CurrentBoundary;

            // 检查点音效（稳定 id，由 B 组在 SfxLibrary 登记）。
            if (ServiceRegistry.TryGet<IAudioService>(out var audio))
            {
                audio.PlaySfx("checkpoint_touch");
            }

            // 过检查点保存（策划 12.3）。
            session.SaveProgressNow();
        }

        /// <summary>恢复最近检查点（策划 12.2 顺序）。</summary>
        public void RestoreLatest()
        {
            Snapshot source = pendingRestore ?? latest;
            pendingRestore = null;
            if (source == null || (GameSession.Instance != null && GameSession.Instance.CurrentLevel != null
                && source.levelId != GameSession.Instance.CurrentLevel.levelId))
            {
                // 尚无检查点（如开局即跌落）：回落到关卡入口出生点，避免恢复空转死循环。
                GameSession session = GameSession.Instance;
                if (session != null && session.CurrentLevel != null
                    && ServiceRegistry.TryGet<ILevelRoot>(out var entryRoom))
                {
                    Debug.LogWarning("[CheckpointService] 无检查点，回落到关卡入口出生点。");
                    entryRoom.PlacePlayer(session.CurrentLevel.spawnId);
                    session.SetObjective(session.CurrentLevel.initialObjective);
                }
                return;
            }
            ApplySnapshot(source);
        }

        private void ApplySnapshot(Snapshot source)
        {
            GameSession session = GameSession.Instance;
            if (session == null)
            {
                return;
            }

            // 1) 边界与年代状态（玩家尚未放置，安全；策划 12.2：先恢复环境再放人）。
            ILevelRoot room = ServiceRegistry.TryGet<ILevelRoot>(out ILevelRoot active) ? active : null;
            if (room != null)
            {
                if (ServiceRegistry.TryGet<IBoundaryContext>(out var boundary))
                {
                    float boundaryX = source.roomId == room.RoomId ? source.boundaryX
                        : (TryGetRoomBoundary(source.roomId, out float other) ? other : source.boundaryX);
                    boundary.ConfigureRoom(boundary.RangeMin, boundary.RangeMax, boundaryX, room.RoomId);
                }

                // 2) 机关状态。
                foreach (MechanismStateSnapshot mechanism in source.mechanisms)
                {
                    room.ApplyMechanismState(mechanism.objectId, mechanism.Era);
                }

                // 3) 放置角色：优先安全出生点 ID（策划 12.2），无匹配则用捕获位置。
                var spawn = room.FindSpawn(source.checkpointId);
                if (ServiceRegistry.TryGet<IPlayerContext>(out var player) && player.IsPresent)
                {
                    if (spawn != null)
                    {
                        player.TeleportTo(spawn);
                    }
                    else
                    {
                        player.TeleportToPosition(source.fallbackPlayerPosition);
                    }
                }
            }

            // 4) 目标。
            if (!string.IsNullOrEmpty(source.objective))
            {
                session.SetObjective(source.objective);
            }
            latest = source;
        }

        private bool TryGetRoomBoundary(string roomId, out float boundaryX)
        {
            if (roomBoundaryHistory.TryGetValue(roomId, out boundaryX))
            {
                return true;
            }
            if (pendingRestore != null)
            {
                foreach (RoomBoundarySnapshot snapshot in pendingRestore.roomBoundaries)
                {
                    if (snapshot.roomId == roomId)
                    {
                        boundaryX = snapshot.boundaryX;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>把最近检查点写入存档数据（供 SaveService 序列化）。</summary>
        public void WriteTo(SaveData data)
        {
            if (data == null)
            {
                return;
            }
            Snapshot source = latest ?? pendingRestore;
            if (source != null)
            {
                data.checkpointId = source.checkpointId;
                data.roomBoundaries = new List<RoomBoundarySnapshot>(source.roomBoundaries);
                data.mechanismStates = new List<MechanismStateSnapshot>(source.mechanisms);
            }
            else if (ServiceRegistry.TryGet<ILevelRoot>(out var room))
            {
                data.checkpointId = null;
                data.roomBoundaries = new List<RoomBoundarySnapshot>
                {
                    new RoomBoundarySnapshot(room.RoomId, room.CurrentBoundary),
                };
            }
        }

        /// <summary>从存档数据恢复检查点状态（不立即应用；关卡加载后由 GameSession 触发 RestoreLatest）。</summary>
        public void RestoreFrom(SaveData data)
        {
            if (data == null)
            {
                latest = null;
                pendingRestore = null;
                roomBoundaryHistory.Clear();
                return;
            }
            pendingRestore = new Snapshot
            {
                checkpointId = data.checkpointId,
                levelId = data.currentLevelId,
                objective = data.currentObjective,
                mechanisms = new List<MechanismStateSnapshot>(data.mechanismStates ?? new List<MechanismStateSnapshot>()),
                roomBoundaries = new List<RoomBoundarySnapshot>(data.roomBoundaries ?? new List<RoomBoundarySnapshot>()),
                roomId = FindRoomIdFrom(data),
                boundaryX = FindBoundaryFrom(data),
            };
            roomBoundaryHistory.Clear();
            foreach (RoomBoundarySnapshot snapshot in pendingRestore.roomBoundaries)
            {
                roomBoundaryHistory[snapshot.roomId] = snapshot.boundaryX;
            }
        }

        private static string FindRoomIdFrom(SaveData data)
        {
            return data.roomBoundaries != null && data.roomBoundaries.Count > 0
                ? data.roomBoundaries[data.roomBoundaries.Count - 1].roomId
                : string.Empty;
        }

        private static float FindBoundaryFrom(SaveData data)
        {
            return data.roomBoundaries != null && data.roomBoundaries.Count > 0
                ? data.roomBoundaries[data.roomBoundaries.Count - 1].boundaryX
                : 0f;
        }
    }
}
