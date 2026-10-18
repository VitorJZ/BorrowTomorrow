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
    /// Bootstrap 启动 → 主菜单 → 开始新游戏 → 进入 L00 → 玩家落地 → 边界/关卡服务就绪。
    /// 每一步状态写入日志（[PlayThrough诊断]），结束时以退出码区分通过(0)/失败(1)。
    /// 批处理：-executeMethod BorrowTomorrow.EditorTools.PlayThroughDiagnostics.RunFromCommandLine（不带 -quit）。
    /// </summary>
    public static class PlayThroughDiagnostics
    {
        internal static bool passed;

        public static void RunFromCommandLine()
        {
            passed = true;
            // 显式打开 Bootstrap：批处理进程的活动场景不保证是系统根场景
            //（上次关闭时打开的是哪个，播放就从哪个开始——空场景会导致 GameSession 缺失）。
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene", OpenSceneMode.Single);
            var runnerGo = new GameObject("PlayThroughRunner");
            runnerGo.AddComponent<PlayThroughRunner>();
            EditorApplication.isPlaying = true; // 不加 -quit：由 Runner 结束时主动退出
        }

        internal static void Report(bool ok, string message)
        {
            if (!ok)
            {
                passed = false;
            }
            Debug.Log($"[PlayThrough诊断] {(ok ? "✓" : "✗")} {message}");
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
                PlayThroughDiagnostics.Report(player.IsGrounded,
                    $"玩家落地站稳（IsGrounded）——这是 QR/把手可用的前置条件");
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

            Finish();
        }

        private void Finish()
        {
            Debug.Log($"[PlayThrough诊断] 结论 = {(PlayThroughDiagnostics.passed ? "通过" : "失败")}");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(PlayThroughDiagnostics.passed ? 0 : 1);
        }
    }
}
