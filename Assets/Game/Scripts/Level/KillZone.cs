using BorrowTomorrow.Core;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 跌落恢复区（策划 4.6 / 7.1 河底跌落区）：
    /// 玩家跌入后由 GameSession 走“淡出→最近检查点→淡入”流程。
    /// 不做游泳系统，不自动改变谜题。
    /// </summary>
    public sealed class KillZone : MonoBehaviour
    {
        [Tooltip("触发玩家层")]
        [SerializeField] private LayerMask playerMask = ~0;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & playerMask) == 0)
            {
                return;
            }
            if (GameSession.Instance == null)
            {
                return;
            }
            // 恢复后玩家会被移出触发区，避免重复触发。
            GameSession.Instance.RequestRespawnFromFall();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.25f, 0.25f, 0.45f);
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Gizmos.DrawCube(transform.position, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
    }
}
