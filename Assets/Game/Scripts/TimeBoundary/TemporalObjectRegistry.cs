using System;
using System.Collections.Generic;
using System.Linq;
using BorrowTomorrow.Core.Data;
using UnityEngine;

namespace BorrowTomorrow.TimeBoundary
{
    /// <summary>
    /// 房间内年代物体注册表（策划 10.3：LevelRoot 的 TemporalObjects 汇集点）。
    /// 挂在房间根上，收集本房间全部 TemporalObject 并提供阈值计划构建。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TemporalObjectRegistry : MonoBehaviour
    {
        private readonly List<TemporalObject> objects = new List<TemporalObject>();

        public IReadOnlyList<TemporalObject> Objects => objects;

        public void Register(TemporalObject obj)
        {
            if (obj != null && !objects.Contains(obj))
            {
                objects.Add(obj);
            }
        }

        public void Unregister(TemporalObject obj)
        {
            objects.Remove(obj);
        }

        /// <summary>按ObjectId查找（存档恢复与剧情引用）。</summary>
        public TemporalObject FindById(string objectId)
        {
            return objects.FirstOrDefault(o => o.ObjectId == objectId);
        }

        /// <summary>
        /// 构建从 fromBoundary 到 toBoundary 的变更计划（策划 11.4 第 1~3 步）。
        /// 必须检查中间阈值，不能只检查拖动终点（策划 11.4）。
        /// </summary>
        public BoundaryChangePlan BuildPlan(float fromBoundary, float toBoundary)
        {
            return BoundaryChangePlan.Build(
                fromBoundary, toBoundary,
                objects.Select(o => (o.AnchorX, o)));
        }

        /// <summary>
        /// 按精确边界位置立即应用全部物体的年代状态。
        /// 用于关卡初始化与存档恢复（immediate：无过渡、无音效）。
        /// </summary>
        public void ResetToBoundary(float boundaryX, bool immediate = true)
        {
            foreach (TemporalObject obj in objects)
            {
                Era era = boundaryX < obj.AnchorX ? Era.Past : Era.Present;
                obj.ApplyEra(era, immediate);
            }
        }
    }
}
