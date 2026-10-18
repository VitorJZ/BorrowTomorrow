using UnityEngine;

namespace BorrowTomorrow.Core.Interfaces
{
    /// <summary>
    /// 玩家状态只读视图，供时间安全校验等系统查询，
    /// 避免其他模块直接依赖玩家实现类（依赖倒置）。
    /// </summary>
    public interface IPlayerContext
    {
        Vector2 Position { get; }

        /// <summary>玩家身体包围盒（世界坐标），用于“新实体覆盖玩家”检测。</summary>
        Bounds BodyBounds { get; }

        /// <summary>是否落地或以其他方式站稳（可开始拖动边界）。</summary>
        bool IsGrounded { get; }

        /// <summary>是否处于梯子攀爬状态（攀爬中禁止拖动，策划 4.5）。</summary>
        bool IsClimbingLadder { get; }

        /// <summary>当前脚下地面碰撞体；腾空时为 null。用于支撑撤除检测。</summary>
        Collider2D GroundCollider { get; }

        /// <summary>玩家是否存活在场（未被跌落恢复流程移除）。</summary>
        bool IsPresent { get; }

        /// <summary>将玩家放置到指定出生点（存档恢复用，策划 12.2：先恢复环境再放人）。</summary>
        void TeleportTo(ISpawnPoint spawn);

        /// <summary>直接放置到世界坐标（仅限存档恢复流程使用）。</summary>
        void TeleportToPosition(Vector2 position);
    }

    /// <summary>关卡出生点。对位置优先存安全出生点 ID，而不是任意空中坐标（策划 12.2）。</summary>
    public interface ISpawnPoint
    {
        string SpawnId { get; }

        Vector2 Position { get; }
    }
}
