using System;
using System.Collections.Generic;

namespace BorrowTomorrow.Core
{
    /// <summary>游戏顶层阶段。依据策划方案 10.6：Boot → MainMenu → Loading → Playing。</summary>
    public enum GamePhase
    {
        Boot,
        MainMenu,
        Loading,
        Playing,
        Epilogue,
        Credits,
    }

    /// <summary>
    /// Playing 阶段的子状态。
    /// 依据策划方案 10.6：Playing 可进入 AdjustingBoundary、Interacting、Cutscene、Paused、Respawning；
    /// AdjustingBoundary 只锁定角色主动位移，不使用全局 TimeScale=0。
    /// </summary>
    public enum PlayingSubState
    {
        Free,
        AdjustingBoundary,
        Interacting,
        Cutscene,
        Paused,
        Respawning,
    }

    /// <summary>
    /// 顶层阶段 + Playing 子状态的双层状态机。
    /// 策划方案 10.6：Paused 从哪个子状态进入就恢复到哪个子状态，
    /// 不能恢复后同时执行移动和剧情。
    /// </summary>
    public sealed class GameStateMachine
    {
        public GamePhase Phase { get; private set; } = GamePhase.Boot;

        public PlayingSubState SubState { get; private set; } = PlayingSubState.Free;

        private PlayingSubState pausedReturnSubState = PlayingSubState.Free;

        /// <summary>顶层阶段变化：。</summary>
        public event Action<GamePhase, GamePhase> PhaseChanged;

        /// <summary>子状态变化：。</summary>
        public event Action<PlayingSubState, PlayingSubState> SubStateChanged;

        /// <summary>玩家是否可以进行移动、跳跃、攀爬等主动操作。</summary>
        public bool IsPlayerControlAllowed =>
            Phase == GamePhase.Playing && SubState == PlayingSubState.Free;

        /// <summary>是否允许开始拖动时间边界（策划 4.5：拖动只能在落地或站稳时开始）。</summary>
        public bool IsBoundaryAdjustAllowed =>
            Phase == GamePhase.Playing && SubState == PlayingSubState.Free;

        /// <summary>是否处于全局暂停（对白推进、剧情计时都应停住）。</summary>
        public bool IsPaused =>
            Phase == GamePhase.Playing && SubState == PlayingSubState.Paused;

        private static readonly Dictionary<GamePhase, HashSet<GamePhase>> phaseTransitions =
            new Dictionary<GamePhase, HashSet<GamePhase>>
            {
                [GamePhase.Boot] = new HashSet<GamePhase> { GamePhase.MainMenu },
                [GamePhase.MainMenu] = new HashSet<GamePhase> { GamePhase.Loading },
                [GamePhase.Loading] = new HashSet<GamePhase> { GamePhase.Playing, GamePhase.MainMenu },
                [GamePhase.Playing] = new HashSet<GamePhase>
                {
                    GamePhase.Loading,   // 下一关 / 重开
                    GamePhase.Epilogue,  // 末关结束
                    GamePhase.Credits,   // 尾声完成进入制作名单（策划 10.6）
                    GamePhase.MainMenu,  // 返回主菜单
                },
                [GamePhase.Epilogue] = new HashSet<GamePhase> { GamePhase.Credits, GamePhase.MainMenu },
                [GamePhase.Credits] = new HashSet<GamePhase> { GamePhase.MainMenu },
            };

        public void SetPhase(GamePhase next)
        {
            if (next == Phase)
            {
                return;
            }
            if (Phase == GamePhase.Playing && next == GamePhase.Loading)
            {
                // 离开 Playing 前先复位子状态，防止恢复后残留旧子状态。
                SetSubStateInternal(PlayingSubState.Free);
            }
            if (!phaseTransitions[Phase].Contains(next))
            {
                UnityEngine.Debug.LogWarning($"[GameStateMachine] 非法阶段转换：{Phase} → {next}，已忽略。");
                return;
            }
            GamePhase previous = Phase;
            Phase = next;
            PhaseChanged?.Invoke(previous, next);
        }

        public void SetSubState(PlayingSubState next)
        {
            if (Phase != GamePhase.Playing || next == SubState)
            {
                return;
            }
            SetSubStateInternal(next);
        }

        private void SetSubStateInternal(PlayingSubState next)
        {
            PlayingSubState previous = SubState;
            SubState = next;
            SubStateChanged?.Invoke(previous, next);
        }

        /// <summary>进入暂停：记录来源子状态，恢复时原样返回（策划 10.6）。</summary>
        public void EnterPause()
        {
            if (Phase != GamePhase.Playing || SubState == PlayingSubState.Paused)
            {
                return;
            }
            pausedReturnSubState = SubState;
            SetSubStateInternal(PlayingSubState.Paused);
        }

        public void ExitPause()
        {
            if (SubState != PlayingSubState.Paused)
            {
                return;
            }
            SetSubStateInternal(pausedReturnSubState);
        }
    }
}
