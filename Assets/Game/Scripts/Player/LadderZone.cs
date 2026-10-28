using UnityEngine;

namespace BorrowTomorrow.Player
{
    /// <summary>
    /// 梯子攀爬区（Trigger）。策划 11.3：梯子用独立 Trigger 与攀爬状态控制；
    /// 离开梯子时恢复正常重力。本组件仅作区域标记，攀爬逻辑在 PlayerMotor2D。
    /// </summary>
    public sealed class LadderZone : MonoBehaviour
    {
        [Tooltip("触发区超过梯顶落脚面的高度；现有梯子均高出平台 0.7 单位。")]
        [SerializeField, Min(0f)] private float topOverrun = .7f;

        public float TopLandingY(Collider2D zone) => zone.bounds.max.y - topOverrun;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
            else
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
            }
        }
    }
}
