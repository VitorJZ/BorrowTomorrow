using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 玩法冒烟测试（播放模式）：复现“从 Bootstrap 启动 → 主菜单 → 开始新游戏 →
    /// 进入 L00 → 玩家落地 → 边界服务就绪”的完整流程。
    /// 用户报告“一直提示先到稳固的地方”即此链路的 IsGrounded/注册断言失败（策划 15.4）。
    /// </summary>
    public sealed class BootToGameSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapToL00_PlayerGroundedAndBoundaryReady()
        {
            SceneManager.LoadScene("Bootstrap");
            yield return new WaitForSeconds(2f); // Bootstrap.Start → 同步进入主菜单

            GameSession session = GameSession.Instance;
            Assert.IsNotNull(session, "Bootstrap 加载后 GameSession.Instance 应存在");
            Assert.AreEqual(GamePhase.MainMenu, session.State.Phase, "应处于主菜单阶段");

            session.StartNewGame();
            float elapsed = 0f;
            while (session.State.Phase != GamePhase.Playing && elapsed < 20f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(GamePhase.Playing, session.State.Phase, $"20 秒内应进入 Playing（等待了 {elapsed:0.0}s）");

            Assert.IsTrue(ServiceRegistry.TryGet<IPlayerContext>(out var player),
                "IPlayerContext 应已注册（玩家未生成则 PlacePlayer/预制体有问题）");
            Assert.IsTrue(player.IsPresent, "玩家应存活在场");
            Assert.IsTrue(player.IsGrounded,
                $"玩家应落地站稳（pos={player.Position}）——IsGrounded 为假即用户所报“先到稳固的地方”无法消除的直接原因");
            Assert.IsFalse(player.IsClimbingLadder, "平地不应处于攀爬状态");

            Assert.IsTrue(ServiceRegistry.TryGet<IBoundaryContext>(out var boundary),
                "IBoundaryContext 应已注册（激活房间未接线则把手/QR全部失效）");
            Assert.IsTrue(ServiceRegistry.TryGet<ILevelContext>(out var level),
                "ILevelContext 应已注册（LevelDirector 缺失则出口条件永远不满足）");

            // 门口地面探测：把手/QR 的“可开始调整”判定与 IsGrounded 同源，此处直接断言。
            Assert.IsTrue(player.IsGrounded && !player.IsClimbingLadder,
                "边界调整前置条件（落地且非攀爬）应成立");
        }
    }
}
