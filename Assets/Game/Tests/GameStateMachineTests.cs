using BorrowTomorrow.Core;
using NUnit.Framework;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 状态机测试（策划 10.6：Boot→MainMenu→Loading→Playing；
    /// Paused 从哪个子状态进入就恢复到哪个子状态）。
    /// </summary>
    public sealed class GameStateMachineTests
    {
        [Test]
        public void PhaseFlow_BootToPlaying()
        {
            var machine = new GameStateMachine();
            Assert.AreEqual(GamePhase.Boot, machine.Phase);

            machine.SetPhase(GamePhase.MainMenu);
            machine.SetPhase(GamePhase.Loading);
            machine.SetPhase(GamePhase.Playing);
            Assert.AreEqual(GamePhase.Playing, machine.Phase);
        }

        [Test]
        public void IllegalPhaseTransition_IsRejected()
        {
            var machine = new GameStateMachine();
            machine.SetPhase(GamePhase.MainMenu);
            machine.SetPhase(GamePhase.Playing); // MainMenu → Playing 非法
            Assert.AreEqual(GamePhase.MainMenu, machine.Phase, "非法转换应被忽略");
        }

        [Test]
        public void Pause_ReturnsToPreviousSubState()
        {
            var machine = new GameStateMachine();
            machine.SetPhase(GamePhase.MainMenu);
            machine.SetPhase(GamePhase.Loading);
            machine.SetPhase(GamePhase.Playing);

            machine.SetSubState(PlayingSubState.Cutscene);
            machine.EnterPause();
            Assert.AreEqual(PlayingSubState.Paused, machine.SubState);

            machine.ExitPause();
            Assert.AreEqual(PlayingSubState.Cutscene, machine.SubState,
                "恢复后应回到进入暂停前的子状态（策划 10.6）");
        }

        [TestCase(PlayingSubState.Paused)]
        [TestCase(PlayingSubState.Cutscene)]
        [TestCase(PlayingSubState.AdjustingBoundary)]
        public void ReturnToMenu_ThenStartGame_ClearsPreviousSubState(PlayingSubState previous)
        {
            var machine = new GameStateMachine();
            machine.SetPhase(GamePhase.MainMenu);
            machine.SetPhase(GamePhase.Loading);
            machine.SetPhase(GamePhase.Playing);
            machine.SetSubState(previous);
            machine.SetPhase(GamePhase.MainMenu);
            Assert.AreEqual(PlayingSubState.Free, machine.SubState);
            machine.SetPhase(GamePhase.Loading);
            machine.SetPhase(GamePhase.Playing);
            Assert.IsTrue(machine.IsPlayerControlAllowed);
        }

        [Test]
        public void ControlLocked_WhileAdjustingOrPaused()
        {
            var machine = new GameStateMachine();
            machine.SetPhase(GamePhase.MainMenu);
            machine.SetPhase(GamePhase.Loading);
            machine.SetPhase(GamePhase.Playing);

            Assert.IsTrue(machine.IsPlayerControlAllowed);
            machine.SetSubState(PlayingSubState.AdjustingBoundary);
            Assert.IsFalse(machine.IsPlayerControlAllowed, "拖动边界期间角色站定（策划 4.5）");
            Assert.IsFalse(machine.IsBoundaryAdjustAllowed, "已在调整中不能再开始拖动");

            machine.SetSubState(PlayingSubState.Free);
            machine.EnterPause();
            Assert.IsFalse(machine.IsPlayerControlAllowed);
            Assert.IsFalse(machine.IsBoundaryAdjustAllowed);
            Assert.IsTrue(machine.IsPaused);
        }
    }
}
