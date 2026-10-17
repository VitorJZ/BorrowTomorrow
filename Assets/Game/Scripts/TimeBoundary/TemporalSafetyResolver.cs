using System;
using System.Collections.Generic;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;

namespace BorrowTomorrow.TimeBoundary
{
    /// <summary>安全阻挡原因（策划 4.5：统一规则，不是某关临时限制）。</summary>
    public enum SafetyBlockReason
    {
        None,
        /// <summary>新实体将覆盖玩家身体。</summary>
        BodyOverlapped,
        /// <summary>玩家正站着的年代平台将被切换成无支撑状态。</summary>
        SupportRemoved,
    }

    /// <summary>
    /// 时间切换安全校验（策划 10.4 TemporalSafetyResolver）：
    /// 计算切换集合，检查新碰撞与支撑撤除。
    /// 纯静态几何判定：不临时启用真实障碍把角色推出去（策划 11.4），
    /// 不依赖物理帧时序，便于自动测试。
    /// </summary>
    public static class TemporalSafetyResolver
    {
        /// <summary>
        /// 评估一次阈值变更的安全性。返回 None 表示允许提交。
        /// </summary>
        public static SafetyBlockReason Evaluate(
            IReadOnlyList<TemporalObject> switching,
            Era targetEra,
            IPlayerContext player)
        {
            if (switching == null || switching.Count == 0)
            {
                return SafetyBlockReason.None;
            }
            if (player == null || !player.IsPresent)
            {
                return SafetyBlockReason.None;
            }

            foreach (TemporalObject obj in switching)
            {
                SafetyPolicy policy = obj.SafetyPolicy;
                if (policy == SafetyPolicy.Ignore)
                {
                    continue;
                }

                // 检查一：新实体将覆盖玩家身体（策划 4.5）。
                bool checkOverlap = policy == SafetyPolicy.BlockIfCoveringPlayer || policy == SafetyPolicy.FullCheck;
                if (checkOverlap && obj.HasColliderIn(targetEra)
                    && obj.TryGetStateBounds(targetEra, out UnityEngine.Bounds newBounds)
                    && newBounds.Intersects(player.BodyBounds))
                {
                    return SafetyBlockReason.BodyOverlapped;
                }

                // 检查二：玩家脚下的支撑将被撤除（策划 4.5）。
                bool checkSupport = policy == SafetyPolicy.BlockIfSupportRemoved || policy == SafetyPolicy.FullCheck;
                if (checkSupport && player.IsGrounded && player.GroundCollider != null
                    && obj.IsSupporting(player.GroundCollider)
                    && !obj.SupportsBodyIn(targetEra, player.BodyBounds))
                {
                    return SafetyBlockReason.SupportRemoved;
                }
            }
            return SafetyBlockReason.None;
        }

        /// <summary>统一受阻提示文案（策划 4.5：“先到稳固的地方。”）。</summary>
        public static string ToDisplayText(SafetyBlockReason reason)
        {
            return reason switch
            {
                SafetyBlockReason.BodyOverlapped => "先到稳固的地方。",
                SafetyBlockReason.SupportRemoved => "先到稳固的地方。",
                _ => string.Empty,
            };
        }
    }
}
