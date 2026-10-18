using UnityEngine;

namespace BorrowTomorrow.Core.Data
{
    /// <summary>
    /// 关卡静态定义（ScriptableObject，数据驱动，策划方案 10.5）。
    /// 字段：levelId、sceneName、boundaryMin/Max、initialBoundary、spawnId。
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
    }
}
