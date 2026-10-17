using UnityEngine;

namespace BorrowTomorrow.Core.Data
{
    /// <summary>
    /// 年代物体安全校验策略（策划 10.5 safetyPolicy）。
    /// </summary>
    public enum SafetyPolicy
    {
        /// <summary>装饰性变化，不参与通行，不参与安全校验。</summary>
        Ignore = 0,

        /// <summary>关键通行物：新状态碰撞不得覆盖玩家（策划 4.5）。</summary>
        BlockIfCoveringPlayer = 1,

        /// <summary>关键支撑物：撤除玩家脚下支撑时阻止切换（策划 4.5）。</summary>
        BlockIfSupportRemoved = 2,

        /// <summary>通行与支撑综合校验（多数年代平台使用）。</summary>
        FullCheck = 3,
    }

    /// <summary>
    /// 年代物体静态定义（ScriptableObject，策划 10.5）。
    /// 字段：objectId、anchorX、pastState、presentState、safetyPolicy、switchSfx。
    /// 作为策划-美术-程序交接契约（策划 13.3）。
    /// </summary>
    [CreateAssetMenu(fileName = "TemporalObj_New", menuName = "BorrowTomorrow/Temporal Object Definition")]
    public sealed class TemporalObjectDefinition : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("稳定物体 id（存档与剧情引用）")]
        public string objectId;

        [Tooltip("年代判定锚点 X。锚点用物件底座小标记提示（策划 4.4）")]
        public float anchorX;

        [Header("状态")]
        [Tooltip("过去（1994）状态描述")]
        public TemporalObjectState pastState;

        [Tooltip("现在（2024）状态描述")]
        public TemporalObjectState presentState;

        [Header("规则")]
        [Tooltip("安全校验策略")]
        public SafetyPolicy safetyPolicy = SafetyPolicy.FullCheck;

        [Tooltip("锚点滞回半径，防止边界停在物体中心时反复切换（策划 4.4）")]
        public float anchorHysteresis = 0.05f;

        [Header("音频")]
        [Tooltip("切换完成音效 id（SfxLibrary）")]
        public string switchSfxId;
    }

    /// <summary>
    /// 某一年代下的状态描述。canStand/blocks 是策划交接数据，
    /// 运行时以场景子物体的真实 Collider 配置为准。
    /// </summary>
    [System.Serializable]
    public sealed class TemporalObjectState
    {
        [Tooltip("状态标识（如 past_ladder / present_branch）")]
        public string stateId;

        [Tooltip("该年代 Sprite（美术交付；场景中由子物体渲染）")]
        public Sprite sprite;

        [Tooltip("该年代是否可站立支撑")]
        public bool canStand;

        [Tooltip("该年代是否阻挡移动")]
        public bool blocksMovement;
    }
}
