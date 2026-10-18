using System.Collections.Generic;
using BorrowTomorrow.Core.Data;

namespace BorrowTomorrow.Core.Interfaces
{
    /// <summary>音频服务：总线混音、年代混合与音效播放。</summary>
    public interface IAudioService
    {
        /// <summary>按画面过去面积比例（0~1）平滑混合双年代配器（策划 09）。</summary>
        void SetEraMix(float pastAreaRatio);

        /// <summary>播放一次性音效（按稳定 SFX id）。</summary>
        void PlaySfx(string sfxId, UnityEngine.Vector2? atWorldPosition = null);

        /// <summary>对白期间轻度压低其他声部，对白优先（策划 09）。</summary>
        void SetDialogueDucking(bool active);

        /// <summary>应用玩家保存的音量偏好（主/音乐/语音）。</summary>
        void ApplyVolumeSettings(SaveSettings settings);
    }

    public interface IStoryPresentation
    {
        void ShowIllustration(UnityEngine.Texture2D texture, bool blackBackground = false);
        void HideIllustration();
    }

    /// <summary>对白播放服务。文字停留由玩家确认，配音可跳过（策划 5.5）。</summary>
    public interface IDialogueService
    {
        bool IsPlaying { get; }

        /// <summary>
        /// 播放一条对白。priority 决定排队或打断规则：
        /// 关键剧情不可被环境触发器截断（策划 12.1）。
        /// </summary>
        void Play(DialogueEntry entry, DialoguePriority priority, System.Action onCompleted = null);

        /// <summary>推进当前台词（玩家按下交互键确认）。</summary>
        void Advance();

        /// <summary>合法跳过当前对白；跳过仍须写入 setFlags（策划 5.5）。</summary>
        void SkipCurrent();
    }

    /// <summary>对白优先级（策划 12.1：低优先级片段排队或跳过，提示不能覆盖身份揭示）。</summary>
    public enum DialoguePriority
    {
        Ambient = 0,   // 环境回声、自语
        Objective = 1, // 目标自语提示
        Story = 2,     // 必看剧情（校服、照片、揭示）
        Reveal = 3,    // 身份揭示等不可打断演出
    }

    /// <summary>存档服务：版本化序列化与本地文件读写（策划 10.4）。</summary>
    public interface ISaveService
    {
        /// <summary>是否找到可用的进度存档。</summary>
        bool HasProgressSave();

        /// <summary>读取进度存档；缺失或损坏时的行为见 LoadResult。</summary>
        SaveLoadResult TryLoadProgress(out SaveData data);

        /// <summary>写入进度存档（临时文件成功后替换正式文件并保留备份，策划 12.3）。</summary>
        bool SaveProgress(SaveData data);

        /// <summary>读取/写入设置存档（与进度分开，重新开始不重置字幕和音量偏好，策划 12.3）。</summary>
        SaveSettings LoadSettings();

        void SaveSettings(SaveSettings settings);
    }

    public enum SaveLoadResult
    {
        Loaded,
        MissingSave,   // 存档缺失：从新游戏开始
        UsedBackup,    // 正式文件无法解析，已恢复备份
        Corrupted,     // 备份也不可用：由 UI 提供选择，不静默覆盖唯一记录
    }

    /// <summary>检查点服务：采集与恢复空间快照（策划 10.4、12.2）。</summary>
    public interface ICheckpointService
    {
        /// <summary>采集当前空间快照并设为最近检查点。</summary>
        void Capture(string checkpointId);

        /// <summary>过门时记录离开房间的边界，不改变最近安全检查点。</summary>
        void RememberRoomBoundary(string roomId, float boundaryX);

        /// <summary>恢复最近检查点：按策划 12.2 顺序恢复边界、年代状态、机关与角色位置。</summary>
        void RestoreLatest();

        /// <summary>是否有已采集的检查点。</summary>
        bool HasCheckpoint { get; }

        /// <summary>当前检查点 id。</summary>
        string CurrentCheckpointId { get; }

        /// <summary>把最近检查点快照写入存档数据（供 SaveService 序列化）。</summary>
        void WriteTo(SaveData data);

        /// <summary>从存档数据恢复检查点状态（不立即放置角色，由 GameSession 协调顺序）。</summary>
        void RestoreFrom(SaveData data);
    }

    /// <summary>HUD 服务：目标、字幕、交互提示、提示气泡与确认框。</summary>
    public interface IHudService
    {
        /// <summary>设置左上目标文本（进入区域后数秒淡出，Tab 展开，策划 5.4）。</summary>
        void ShowObjective(string text, float autoHideSeconds = 4f);

        void ShowObjectivePersistent(string text);

        void HideObjective();

        /// <summary>显示底部对白字幕（最多两行）。</summary>
        void ShowSubtitle(string speakerLabel, string text);

        void HideSubtitle();

        /// <summary>显示/隐藏交互物附近的 E 提示（只在范围内显示，策划 5.4；位置用于屏幕投影）。</summary>
        void SetInteractionPrompt(bool visible, string hint = null, UnityEngine.Vector2 worldPosition = default);

        /// <summary>显示边界受阻提示：“先到稳固的地方。”（策划 4.5）。</summary>
        void ShowBoundaryBlocked(string reason);

        /// <summary>章节角标：过门转场后重新标清两侧年代（策划 4.2）。</summary>
        void FlashEraBadge(BorrowTomorrow.Core.Data.Era dominantEra);

        /// <summary>场景/恢复转场用：屏幕淡出（策划 4.6 跌落淡出约 0.3 秒）。</summary>
        void FadeOut(float seconds);

        /// <summary>屏幕淡入。</summary>
        void FadeIn(float seconds);
    }

    /// <summary>关卡上下文：向系统暴露当前关卡元数据。</summary>
    public interface ILevelContext
    {
        LevelDefinition Definition { get; }

        /// <summary>本关是否满足出口开放条件（LevelDirector 判定）。</summary>
        bool ExitUnlocked { get; }

        /// <summary>出口条件未满足时的自语文本（策划 7.7：不使用无解释空气墙）。</summary>
        string ExitLockedHint { get; }
    }
}
