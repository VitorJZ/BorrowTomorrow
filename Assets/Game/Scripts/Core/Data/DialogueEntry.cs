using UnityEngine;

namespace BorrowTomorrow.Core.Data
{
    /// <summary>
    /// 对白条目（ScriptableObject，策划 10.5）。
    /// 字段：entryId、speakerLabel、subtitle、voiceClip、nextId、requiredFlags、setFlags。
    /// 字幕与对白集中维护，禁止脚本内硬编码剧情文本（策划 10.5）。
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "BorrowTomorrow/Dialogue Entry")]
    public sealed class DialogueEntry : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("稳定对白 id（串联与触发引用）")]
        public string entryId;

        [Header("内容")]
        [Tooltip("说话人显示名（“童年林晓”“年轻外婆”“主角”等；不标注主角真名，策划 3.2）")]
        public string speakerLabel;

        [Tooltip("字幕文本（必须覆盖全部中文标点字库）")]
        [TextArea(2, 4)]
        public string subtitle;

        [Tooltip("配音文件；无配音降级版仅靠字幕讲清剧情（策划 09）")]
        public AudioClip voiceClip;

        [Header("流转")]
        [Tooltip("下一条对白 id；为空表示本条结束")]
        public string nextId;

        [Tooltip("本条显示所需前置标记；为空无条件")]
        public string[] requiredFlags;

        [Tooltip("本条播放完成（或合法跳过）后写入的标记")]
        public string[] setFlags;

        [Header("呈现")]
        [Tooltip("本条结束后是否等待玩家按 E 确认再进入下一条（文字停留由玩家确认，策划 5.5）")]
        public bool holdForConfirm = true;

        [Tooltip("字幕停留秒数下限（配音时长不足时保底）")]
        public float minDurationSeconds = 1.2f;

        [Header("完整剧情 v1.3")]
        public string source;
        public string eventId;
        public string[] completionFlags;
        public string objectiveAfter;
        public Texture2D illustration;
        public bool blackBackground;
        public bool centeredTitle;
        public float delayBefore;
        public string sfxId;
        public float endHoldSeconds;
    }
}
