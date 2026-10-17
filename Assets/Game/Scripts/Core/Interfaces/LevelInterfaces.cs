using BorrowTomorrow.Core.Data;

namespace BorrowTomorrow.Core.Interfaces
{
    /// <summary>
    /// 房间根组件契约。策划方案 10.3：每关 LevelRoot 提供
    /// PlayerSpawn、CameraBounds、BoundaryRange、Interactables、Checkpoints、Exit。
    /// 一个关卡场景可包含多个房间（如 L01 的水渠房与树房），每间房保存自己的边界位置（策划 4.2）。
    /// </summary>
    public interface ILevelRoot
    {
        LevelDefinition Definition { get; }

        /// <summary>房间稳定 id（如 L01A、L01B）。</summary>
        string RoomId { get; }

        /// <summary>是否为当前活动房间（决定相机边界、交互与边界把手范围）。</summary>
        bool IsActiveRoom { get; }

        /// <summary>激活房间：应用相机边界、边界范围并启用交互（由 GameSession 在转场时调用）。</summary>
        void ActivateRoom();

        /// <summary>停用房间。</summary>
        void DeactivateRoom();

        /// <summary>按出生点 id 放置玩家（策划 12.2：环境恢复完成后才放置角色）。</summary>
        void PlacePlayer(string spawnId);

        /// <summary>本房间当前已提交边界位置（供检查点快照采集）。</summary>
        float CurrentBoundary { get; }

        /// <summary>存档恢复用：按稳定 objectId 应用年代物体状态（策划 12.2）。
        /// 物体不存在时安全忽略（防旧存档引用已删内容）。</summary>
        void ApplyMechanismState(string objectId, Era era);

        /// <summary>采集本房间全部年代物体的当前状态（检查点快照用，策划 12.2）。</summary>
        System.Collections.Generic.IReadOnlyList<MechanismStateSnapshot> CollectMechanismStates();

        /// <summary>按出生点 id 查找出生点；不存在返回 null。</summary>
        ISpawnPoint FindSpawn(string spawnId);
    }

    /// <summary>相机约束接口：房间转场时更新边界，拖动边界时冻结跟随（策划 5.3）。</summary>
    public interface ICameraRig
    {
        /// <summary>设置当前活动房间的相机活动范围（世界坐标）。</summary>
        void SetRoomBounds(UnityEngine.Rect worldBounds);

        /// <summary>冻结/恢复镜头跟随（调整时间边界期间镜头保持不动，策划 5.3）。</summary>
        void SetFollowFrozen(bool frozen);
    }
}
