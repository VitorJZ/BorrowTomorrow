using UnityEngine;

namespace BorrowTomorrow.Core.Interfaces
{
    /// <summary>
    /// 输入源抽象。策划方案 10.1：输入封装独立一层，
    /// 玩法代码不直接散落读取按键；首版由 InputRouter 用旧输入 API 实现，
    /// 未来可替换为 Input System 而不影响调用方。
    /// </summary>
    public interface IInputSource
    {
        /// <summary>水平移动意图（-1/0/1）。A/D 或左右箭头（策划 5.1）。</summary>
        float MoveAxis { get; }

        /// <summary>垂直攀爬意图（-1/0/1）。W/S 或上下箭头。</summary>
        float ClimbAxis { get; }

        /// <summary>本帧按下跳跃（Space）。</summary>
        bool JumpPressedThisFrame { get; }

        /// <summary>本帧按下交互（E：调查、开门、继续短文本）。</summary>
        bool InteractPressedThisFrame { get; }

        /// <summary>
        /// 消费式交互：同一帧内只有第一个调用者获得本次按键
        /// （对白推进优先于场景调查，由执行顺序保证）。
        /// </summary>
        bool ConsumeInteract();

        /// <summary>边界键盘单步：Q 返回 -1，R 返回 +1，无输入 0（策划 5.1）。</summary>
        float BoundaryNudgeDirection { get; }

        /// <summary>本帧请求恢复最近检查点（Backspace，需经确认，策划 5.1/4.6）。</summary>
        bool RespawnPressedThisFrame { get; }

        /// <summary>按住 Tab 查看当前目标与已听录音的文字记录（策划 5.1）。</summary>
        bool ObjectiveHeld { get; }

        /// <summary>本帧请求暂停（Esc，策划 5.1）。</summary>
        bool PausePressedThisFrame { get; }

        /// <summary>跳跃按住状态（用于可变跳跃高度或提前松开截断，若启用）。</summary>
        bool JumpHeld { get; }
    }

    /// <summary>
    /// 交互物契约。策划 10.4 PlayerInteractor：筛选最近可交互物并执行一次互动。
    /// 实现方包括：叙事调查物、门/出口、暗格等。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>稳定交互 id（用于剧情标记与存档）。</summary>
        string InteractId { get; }

        /// <summary>交互提示短文本（如“查看校服”）；为空则仅显示 E。</summary>
        string InteractPrompt { get; }

        /// <summary>是否当前可交互（可被冷却或剧情条件禁用）。</summary>
        bool CanInteract { get; }

        /// <summary>执行一次互动；返回是否消耗本次输入。</summary>
        bool Interact();
    }

    /// <summary>
    /// 系统根组件的启动钩子。Bootstrap 收集系统根下全部实现者，
    /// 按 InitializeOrder 升序调用，保证服务装配顺序（策划 10.3）。
    /// </summary>
    public interface IBootInitializable
    {
        /// <summary>数值越小越先初始化。</summary>
        int InitializeOrder { get; }

        void OnBootInitialize();
    }
}
