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

        [Tooltip("本条演出插画；不参与玩法碰撞") ]
        public Texture2D illustration;

        [Tooltip("以整页文书图片居中展示，隐藏对白框、说话人与按钮；仍由 E/点击推进")]
        public bool documentMode;

        public bool blackBackground;

        [Tooltip("记忆回放段：黑底、暖色旧照调、暗角颗粒扫描线与轻微晃动，用于磁带/回忆 callback")]
        public bool memoryEcho;

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

        [Tooltip("为真时本条在配音结束并停顿后自动进入下一条，无需玩家按键")]
        public bool autoAdvance;

        [Tooltip("自动推进前，配音结束后的停顿秒数")]
        public float autoAdvanceDelay = 0.75f;

        [Tooltip("本条开始时淡入游戏画面（用于黑屏开场后的第一句）")]
        public bool fadeInOnStart;

        [Header("完整剧情 v1.3")]
        public string source;
        public string eventId;
        public string[] completionFlags;
        public string objectiveAfter;
        public bool centeredTitle;
        public float delayBefore;
        public string sfxId;
        public float endHoldSeconds;
        [Header("进入本句前的场景转场（0 为直接衔接）")]
        [Min(0f)] public float sceneLeadHold;
        [Min(0f)] public float sceneBlendSeconds;
        public Rect scenePhotoRect;
        public Rect scenePreviousPhotoCrop = new Rect(0f, 0f, 1f, 1f);
        public float scenePhotoRotation;
        [Min(0f)] public float sceneImageHold;
        public float SceneTransitionDuration => sceneLeadHold + sceneBlendSeconds + sceneImageHold;
    }
}
