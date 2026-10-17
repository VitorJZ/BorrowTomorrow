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
        private bool populatedFromHierarchy;

        /// <summary>
        /// 本房间全部年代物体。首次访问时从层级惰性收集（编辑器模式下 AddComponent 不触发 Awake，
        /// 也不依赖任何 Awake 顺序——策划 10.3：LevelRoot 初始化时物体必已就位）。
        /// </summary>
        public IReadOnlyList<TemporalObject> Objects
        {
            get
            {
                if (!populatedFromHierarchy)
                {
                    populatedFromHierarchy = true;
                    // 去重：个别物体可能已在 Awake 中自行注册。
                    foreach (TemporalObject found in GetComponentsInChildren<TemporalObject>(true))
                    {
                        if (!objects.Contains(found))
                        {
                            objects.Add(found);
                        }
                    }
                }
                objects.RemoveAll(o => o == null); // 清理运行期销毁的残留引用
                return objects;
            }
        }

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
            return Objects.FirstOrDefault(o => o.ObjectId == objectId);
        }

        /// <summary>
        /// 构建从 fromBoundary 到 toBoundary 的变更计划（策划 11.4 第 1~3 步）。
        /// 必须检查中间阈值，不能只检查拖动终点（策划 11.4）。
        /// </summary>
        public BoundaryChangePlan BuildPlan(float fromBoundary, float toBoundary)
        {
            return BoundaryChangePlan.Build(
                fromBoundary, toBoundary,
                Objects.Select(o => (o.AnchorX, o)));
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
