using System;
using BorrowTomorrow.Core.Data;

namespace BorrowTomorrow.Core.Interfaces
{
    /// <summary>
    /// 时间边界系统对外视图。渲染、音频、HUD 与关卡逻辑只读取已提交 B，
    /// 不读取未被批准的候选值（策划方案 11.4 第 6 条）。
    /// </summary>
    public interface IBoundaryContext
    {
        /// <summary>当前房间已提交的边界位置（世界 X）。</summary>
        float CommittedBoundary { get; }

        /// <summary>边界允许范围（当前房间）。</summary>
        float RangeMin { get; }

        float RangeMax { get; }

        /// <summary>边界提交后触发：。失败受阻时触发 OnAdjustBlocked。</summary>
        event Action<float> OnBoundaryCommitted;

        /// <summary>调整被安全规则阻止时触发（原因文本用于提示“先到稳固的地方”）。</summary>
        event Action<string> OnAdjustBlocked;

        /// <summary>LevelRoot 在关卡初始化时调用：配置房间范围与初始边界（策划 4.2：每间房保存自己的边界位置）。</summary>
        void ConfigureRoom(float rangeMin, float rangeMax, float initialBoundary, string roomId);

        /// <summary>来自鼠标把手或 Q/R 的调整请求；内部完成安全校验后提交。</summary>
        void RequestAdjustment(float targetBoundaryX);

        /// <summary>键盘单步调整（Q 向左 / R 向右，策划 5.1）。</summary>
        void Nudge(float directionSign);

        /// <summary>查询某个锚点 X 在当前已提交边界下的年代归属。</summary>
        Era EraAtAnchor(float anchorX);
    }
}
