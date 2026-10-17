using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 检查点标记（策划 7.x 各关检查点 / 12.2）：
    /// 玩家进入触发区时采集空间快照并保存进度。
    /// </summary>
    public sealed class CheckpointMarker : MonoBehaviour
    {
        [Tooltip("稳定检查点 id（存档引用）")]
        [SerializeField] private string checkpointId = "checkpoint_00";

        [Tooltip("玩家所在层")]
        [SerializeField] private LayerMask playerMask = ~0;

        [Tooltip("本检查点是否为房间安全点（恢复时回到这里）")]
        [SerializeField] private bool isRespawnPoint = true;

        public string CheckpointId => checkpointId;

        private bool triggeredThisStay;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isRespawnPoint || triggeredThisStay)
            {
                return;
            }
            if (((1 << other.gameObject.layer) & playerMask) == 0)
            {
                return;
            }
            if (!other.GetComponentInParent<PlayerContextMarker>())
            {
                // 用层过滤已足够；此标记组件仅供快速识别，非必需。
            }
            if (ServiceRegistry.TryGet<ICheckpointService>(out var checkpoints))
            {
                checkpoints.Capture(checkpointId);
                triggeredThisStay = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & playerMask) != 0)
            {
                triggeredThisStay = false; // 离开后允许再次触发（如反复取回物品路线）
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.95f, 0.9f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(1.2f, 1.6f, 0.1f));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.1f, checkpointId);
#endif
        }
    }

    /// <summary>玩家层标记组件（可选，便于层过滤与调试可视化）。</summary>
    public sealed class PlayerContextMarker : MonoBehaviour
    {
    }
}
