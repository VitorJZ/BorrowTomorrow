namespace BorrowTomorrow.Core.Data
{
    /// <summary>时间年代。依据策划方案 4.2：边界左侧为 1994（过去），右侧为 2024（现在）。</summary>
    public enum Era
    {
        Past = 0,    // 1994年
        Present = 1, // 2024年
    }

    public static class EraExtensions
    {
        /// <summary>用于 HUD 与对白显示的年代标签。</summary>
        public static string Label(this Era era)
        {
            return era == Era.Past ? "1994" : "2024";
        }
    }
}
