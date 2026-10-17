using System;
using System.Collections.Generic;
using System.Linq;
using BorrowTomorrow.Core.Data;
using UnityEngine;

namespace BorrowTomorrow.TimeBoundary
{
    /// <summary>
    /// 边界变更计划（策划 11.4 提交算法的纯逻辑部分，便于自动测试）。
    /// 找出当前 B 到目标 B 之间跨过的所有物体阈值，按移动方向排序，
    /// 同一阈值上的物体作为一笔变更处理。
    /// </summary>
    public sealed class BoundaryChangePlan
    {
        /// <summary>阈值判定精度（世界单位）。</summary>
        public const float Epsilon = 0.01f;

        /// <summary>同阈值分组容差。</summary>
        public const float GroupTolerance = 0.001f;

        /// <summary>从起点到终点的方向：+1 右移（过去覆盖更多），-1 左移。</summary>
        public readonly int Direction;

        public readonly List<ThresholdStep> Steps = new List<ThresholdStep>();

        private BoundaryChangePlan(int direction)
        {
            Direction = direction;
        }

        public bool IsEmpty => Steps.Count == 0;

        /// <summary>
        /// 构建计划。anchors：待排序的 (anchorX, payload) 对。
        /// 右移时收集 (from, to] 内的锚点并升序；左移时收集 [to, from) 并降序。
        /// </summary>
        public static BoundaryChangePlan Build<T>(
            float fromBoundary, float toBoundary, IEnumerable<(float anchorX, T payload)> anchors)
        {
            int direction = Math.Sign(toBoundary - fromBoundary);
            var plan = new BoundaryChangePlan(direction);
            if (direction == 0)
            {
                return plan;
            }

            IEnumerable<(float anchorX, T payload)> crossed = direction > 0
                ? anchors.Where(a => a.anchorX > fromBoundary + Epsilon && a.anchorX <= toBoundary + Epsilon)
                : anchors.Where(a => a.anchorX < fromBoundary - Epsilon && a.anchorX >= toBoundary - Epsilon);

            IOrderedEnumerable<(float anchorX, T payload)> ordered = direction > 0
                ? crossed.OrderBy(a => a.anchorX)
                : crossed.OrderByDescending(a => a.anchorX);

            foreach ((float anchorX, T payload) in ordered)
            {
                float boundaryAfter = anchorX + direction * Epsilon;
                // 策划 4.2：锚点 x < B → 过去；x > B → 现在。
                Era targetEra = anchorX < boundaryAfter ? Era.Past : Era.Present;
                if (plan.Steps.Count > 0
                    && Mathf.Abs(plan.Steps[plan.Steps.Count - 1].AnchorX - anchorX) <= GroupTolerance)
                {
                    plan.Steps[plan.Steps.Count - 1].Add(payload);
                }
                else
                {
                    plan.Steps.Add(new ThresholdStep(anchorX, targetEra, direction, payload));
                }
            }
            return plan;
        }

        /// <summary>单个阈值上的一组同步变更。</summary>
        public sealed class ThresholdStep
        {
            public readonly float AnchorX;
            public readonly Era TargetEra;
            public readonly int Direction;

            private readonly List<object> payloads = new List<object>();

            public ThresholdStep(float anchorX, Era targetEra, int direction, object firstPayload)
            {
                AnchorX = anchorX;
                TargetEra = targetEra;
                Direction = direction;
                payloads.Add(firstPayload);
            }

            public void Add(object payload)
            {
                payloads.Add(payload);
            }

            /// <summary>该阈值上的全部物体（同一类型，由调用方保证）。</summary>
            public IReadOnlyList<T> GetPayloads<T>()
            {
                return payloads.Cast<T>().ToList();
            }

            /// <summary>越过后边界应停的位置（物体年代已切换的一侧）。</summary>
            public float BoundaryAfter => AnchorX + Direction * Epsilon;

            /// <summary>被安全规则阻止时应停下的位置（物体保持原年代的一侧）。</summary>
            public float SafeBoundaryBefore => AnchorX - Direction * Epsilon;
        }
    }
}
