using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 出生点（关卡空间概念，策划 12.2）：
    /// 对位置优先存安全出生点 ID，而不是任意空中坐标。
    /// 放置在可安全站稳的位置（检查点恢复与房间入口共用）。
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour, Core.Interfaces.ISpawnPoint
    {
        [Tooltip("稳定出生点 id（检查点与存档引用）")]
        [SerializeField] private string spawnId = "spawn_default";

        [Tooltip("角色放置时相对本点的偏移（例如从胶囊底部对齐到胶囊中心）")]
        [SerializeField] private Vector2 placementOffset = new Vector2(0f, 1f);

        public string SpawnId => spawnId;

        public Vector2 Position => (Vector2)transform.position + placementOffset;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
            Gizmos.DrawWireSphere((Vector2)transform.position + placementOffset, 0.25f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label((Vector2)transform.position + placementOffset + Vector2.up * 0.4f, spawnId);
#endif
        }
    }
}
