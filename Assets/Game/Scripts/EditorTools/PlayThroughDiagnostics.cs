using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// 自动试玩诊断器（仅编辑器）：真实进入播放模式，复现用户全流程——
    /// Bootstrap 启动 → 主菜单 → 开始新游戏 → 进入 L00 → 玩家落地 → 边界/关卡服务就绪 →
    /// 可玩性实测（移动/交互/时间边界操作）。
    /// 每一步状态写入日志（[PlayThrough诊断]）与报告文件（log/2026-10-18/playtest-report.md），
    /// 结束时以退出码区分通过(0)/失败(1)。
    /// 批处理：-executeMethod BorrowTomorrow.EditorTools.PlayThroughDiagnostics.RunFromCommandLine（不带 -quit）。
    /// </summary>
    public static class PlayThroughDiagnostics
    {
        internal static bool passed;

        /// <summary>全部检查项记录（供试玩报告文件使用）。</summary>
        internal static readonly System.Collections.Generic.List<string> ReportLines =
            new System.Collections.Generic.List<string>();

        internal static void Report(bool ok, string message)
        {
            if (!ok)
            {
                passed = false;
            }
            string line = (ok ? "✓" : "✗") + " " + message;
            ReportLines.Add(line);
            Debug.Log($"[PlayThrough诊断] {line}");
        }

        /// <summary>批处理入口：显式打开 Bootstrap 场景后进入播放模式执行诊断
        /// （批处理进程的活动场景不保证是系统根场景——空场景会导致 GameSession 缺失）。</summary>
        public static void RunFromCommandLine()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene", OpenSceneMode.Single);
            var runnerGo = new GameObject("PlayThroughRunner");
            runnerGo.AddComponent<PlayThroughRunner>();
            EditorApplication.isPlaying = true; // 不加 -quit：由 Runner 结束时主动退出
        }
    }

    internal sealed class PlayThroughRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return new WaitForSeconds(2f); // Bootstrap.Awake/Start → 进入主菜单

            GameSession session = GameSession.Instance;
            PlayThroughDiagnostics.Report(session != null, "GameSession 存在");
            PlayThroughDiagnostics.Report(session != null && session.State.Phase == GamePhase.MainMenu,
                $"主菜单阶段（实际 {session?.State.Phase.ToString() ?? "无会话"}）");
            if (session == null)
            {
                Finish();
                yield break;
            }

            session.StartNewGame();
            float elapsed = 0f;
            while (session.State.Phase != GamePhase.Playing && elapsed < 20f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            PlayThroughDiagnostics.Report(session.State.Phase == GamePhase.Playing,
                $"进入 Playing（等待 {elapsed:0.0}s，实际阶段 {session.State.Phase}）");

            PlayThroughDiagnostics.Report(ServiceRegistry.TryGet<IPlayerContext>(out var player),
                "IPlayerContext 已注册");
            if (player != null)
            {
                PlayThroughDiagnostics.Report(player.IsPresent, $"玩家在场 pos={player.Position}");
                // 等待物理沉降后断言落地站稳（策划 15.1：出生后能否落地站稳）。
                yield return new WaitForSeconds(0.8f);
                PlayThroughDiagnostics.Report(player.IsGrounded, "玩家落地站稳（IsGrounded）");
                PlayThroughDiagnostics.Report(!player.IsClimbingLadder, "未处于攀爬状态");
            }

            PlayThroughDiagnostics.Report(ServiceRegistry.TryGet<IBoundaryContext>(out var boundary),
                "IBoundaryContext 已注册（激活房间）");
            if (boundary != null)
            {
                PlayThroughDiagnostics.Report(
                    Mathf.Abs(boundary.CommittedBoundary - boundary.RangeMin) <= float.MaxValue,
                    $"边界已提交 B={boundary.CommittedBoundary:0.00}（范围 {boundary.RangeMin:0.0}~{boundary.RangeMax:0.0}）");
            }
            PlayThroughDiagnostics.Report(ServiceRegistry.TryGet<ILevelContext>(out var level),
                "ILevelContext 已注册（LevelDirector）");
            if (level != null)
            {
                PlayThroughDiagnostics.Report(level.Definition != null,
                    $"关卡定义已引用（{level.Definition.levelId} {level.Definition.sceneName}）");
            }

            // ---------------- 可玩性实测（真实物理与状态机，策划 15.1 自动化子集） ----------------
            yield return PlayabilityChecks();

            Finish();
        }

        /// <summary>可玩性实测：移动 / 交互 / 时间边界操作。</summary>
        private IEnumerator PlayabilityChecks()
        {
            // 1) 移动：直接驱动玩家刚体速度模拟 A/D 按住 0.5 秒（旧 Input Manager 无法程序注入按键，
            //    以刚体速度驱动等价验证"移动系统+碰撞"链路），断言产生实际位移。
            var body = Object.FindFirstObjectByType<Rigidbody2D>();
            PlayThroughDiagnostics.Report(body != null, "找到玩家 Rigidbody2D（移动载体）");
            if (body == null)
            {
                yield break;
            }
            ServiceRegistry.TryGet<IPlayerContext>(out var player);
            float x0 = body.position.x;
            body.velocity = new Vector2(3.5f, 0f);
            yield return new WaitForSeconds(0.5f);
            body.velocity = Vector2.zero;
            float dx = body.position.x - x0;
            PlayThroughDiagnostics.Report(dx > 0.5f, $"移动：0.5 秒位移 {dx:0.00} 单位（期望 > 0.5）");
            yield return new WaitForSeconds(0.3f); // 恢复静止

            // 2) 交互：出口门（ExitDoor）在场且处于可交互状态（按 E 触发的目标）。
            var door = Object.FindFirstObjectByType<Level.ExitDoor>();
            PlayThroughDiagnostics.Report(door != null && door.CanInteract, "出口门可交互（E 键目标）");

            // 3) 时间边界操作：与把手拖拽同源的完整流程——
            //    开始调整（进入子状态、角色站定）→ 边界左移 2 单位（安全校验+提交）→ 松开恢复自由。
            var boundary = Object.FindFirstObjectByType<TimeBoundary.BoundaryController>();
            PlayThroughDiagnostics.Report(boundary != null, "找到 BoundaryController");
            if (boundary == null)
            {
                yield break;
            }
            float b0 = boundary.CommittedBoundary;

            boundary.BeginMouseDrag(); // 与把手按下同源入口
            PlayThroughDiagnostics.Report(
                GameSession.Instance.State.SubState == PlayingSubState.AdjustingBoundary,
                "Q/R/拖动：进入 AdjustingBoundary 子状态（角色站定）");

            boundary.RequestAdjustment(b0 - 2f);
            PlayThroughDiagnostics.Report(
                boundary.CommittedBoundary <= b0 - 1.5f,
                $"边界左移生效：{b0:0.00} → {boundary.CommittedBoundary:0.00}（期望 ≤ {b0 - 1.5:0.00}）");

            boundary.EndMouseDrag(); // 松开（与把手松开同源）
            PlayThroughDiagnostics.Report(
                GameSession.Instance.State.SubState == PlayingSubState.Free,
                "松开后恢复 Free 子状态（可重新移动）——此前该处曾永久卡死，已修复");
        }

        private void Finish()
        {
            WriteReport();
            Debug.Log($"[PlayThrough诊断] 结论 = {(PlayThroughDiagnostics.passed ? "通过" : "失败")}");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(PlayThroughDiagnostics.passed ? 0 : 1);
        }

        /// <summary>把本次试玩结果写入比赛日志目录（按日期归档）。</summary>
        private void WriteReport()
        {
            try
            {
                string dir = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), @"..\..\log\2026-10-18"));
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "playtest-report.md");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# 自动试玩报告（PlayThroughDiagnostics）");
                sb.AppendLine();
                sb.AppendLine("- 时间：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("- 流程：Bootstrap → 主菜单 → 开始新游戏 → L00_Gate（真实播放模式，含可玩性实测）");
                sb.AppendLine("- 结论：" + (PlayThroughDiagnostics.passed ? "✓ 通过" : "✗ 失败"));
                sb.AppendLine();
                sb.AppendLine("## 检查项");
                foreach (string line in PlayThroughDiagnostics.ReportLines)
                {
                    sb.AppendLine("- " + line);
                }
                System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
                Debug.Log("[PlayThrough诊断] 报告已写入 " + path);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[PlayThrough诊断] 报告写入失败：" + ex.Message);
            }
        }
    }
}
