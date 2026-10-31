namespace BorrowTomorrow.Core.Interfaces
{
    public interface ICharacterArt
    {
        void PlayGesture(string name);

        /// <summary>开场入场的世界空间水平偏移；逻辑身体留在安全出生点。</summary>
        void SetEntranceOffset(float worldOffsetX, bool walking);

        /// <summary>人物画面相对身体原点的右侧范围，用于屏幕外入场。</summary>
        float EntranceRightExtent { get; }
    }
}
