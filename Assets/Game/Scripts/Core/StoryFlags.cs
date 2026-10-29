namespace BorrowTomorrow.Core
{
    /// <summary>
    /// 关键剧情标记常量。依据策划方案 12.1。
    /// PhotoRevealed 与 IdentityKnown 只在揭示演出完成或合法跳过后写入。
    /// </summary>
    public static class StoryFlags
    {
        /// <summary>开场磁带录音已播放。</summary>
        public const string IntroPlayed = "IntroPlayed";

        /// <summary>院门内的腾空通知已读，回屋动机已由独白引出。</summary>
        public const string NoticeRead = "NoticeRead";

        /// <summary>玩家已被 2024 年的木箱挡住，并获知时间边界可拖动。</summary>
        public const string CrateBlocked = "CrateBlocked";

        /// <summary>身高刻线与儿童画已看过。</summary>
        public const string HeightMarksSeen = "HeightMarksSeen";

        /// <summary>缝纫间校服互动（袖口对白）已完成。</summary>
        public const string UniformRead = "UniformRead";

        /// <summary>阁楼门旁照片已翻看（揭示演出完成或合法跳过后写入）。</summary>
        public const string PhotoRevealed = "PhotoRevealed";

        /// <summary>身份真相已明确，后续不再用身份迷惑作为悬念。</summary>
        public const string IdentityKnown = "IdentityKnown";

        /// <summary>暗格位置已找到，进入“告知位置”状态。</summary>
        public const string BoxLocated = "BoxLocated";

        /// <summary>尾声（两天后）已看过。</summary>
        public const string EpilogueSeen = "EpilogueSeen";
    }

    /// <summary>
    /// 玩家当前目标文本常量。依据策划方案 3.2 / 3.6。
    /// 禁止在开场显示主角姓名或任何暗示身份的措辞。
    /// </summary>
    public static class Objectives
    {
        /// <summary>开场任务（3.2）。</summary>
        public const string ReachAtticFindBox = "去阁楼，找到蓝色铁皮盒。";

        /// <summary>身份揭示后的任务（3.6）。</summary>
        public const string TellXiaoxaoBoxLocation = "找到铁盒的位置，告诉晓晓。";
    }
}
