using UnityEngine;

namespace BorrowTomorrow.Core.Data
{
    /// <summary>
    /// 关卡静态定义（ScriptableObject，数据驱动，策划方案 10.5）。
    /// 字段：levelId、sceneName、boundaryMin/Max、initialBoundary、spawnId、hintGroups。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDef_New", menuName = "BorrowTomorrow/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("稳定关卡 id：L00、L01A、L01B、L02、L03、L04、Epilogue")]
        public string levelId;

        [Tooltip("对应场景文件名（不含扩展名）")]
        public string sceneName;

        [Tooltip("显示名（HUD 与章节选择用）")]
        public string displayName;

        [Tooltip("进入关卡时的初始目标文本")]
        public string initialObjective;

        [Header("时间边界")]
        [Tooltip("本关边界允许范围最小 X（房间左端，允许略超出形成全过去，策划 4.2）")]
        public float boundaryMin;

        [Tooltip("本关边界允许范围最大 X（房间右端）")]
        public float boundaryMax;

        [Tooltip("初始边界位置")]
        public float initialBoundary;

        [Header("出生与出口")]
        [Tooltip("初始出生点 id（LevelRoot 中的 SpawnPoint）")]
        public string spawnId;

        [Tooltip("出口开放所需剧情标记；为空则进入即开放")]
        public string[] exitRequiredFlags;

        [Tooltip("出口条件未满足时的自语文本（策划 7.7）")]
        public string exitLockedHint;

        [Header("三级提示（策划 12.4 / 7.x）")]
        public HintGroup[] hintGroups;
    }

    /// <summary>
    /// 一组绑定到关卡阶段的三级提示。玩家按 H 才展开下一层，不自动报答案。
    /// </summary>
    [System.Serializable]
    public sealed class HintGroup
    {
        [Tooltip("阶段 key：完成该阶段后停止旧提示计时（策划 12.4）")]
        public string stageKey;

        [Tooltip("一级提示：观察引导（如“看看栅栏”）")]
        public string hintLevel1;

        [Tooltip("二级提示：规则说明（如“它现在不完整”）")]
        public string hintLevel2;

        [Tooltip("三级提示：接近答案（如“把边界拉到栅栏左边”）")]
        public string hintLevel3;
    }
}
