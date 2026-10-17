using UnityEngine;

namespace BorrowTomorrow.TimeBoundary
{
    /// <summary>
    /// 时间视觉控制器（策划 10.4 TemporalVisualController + 11.1 渲染方案）：
    /// 背景层用两个 Sprite Mask 分别展示边界两侧的双年代背景；
    /// 恒定层不受遮罩影响；关键 TemporalObject 由自身整体切换外观与碰撞。
    /// 策划 11.1：Sprite Mask 是精灵显示机制，不解决物理碰撞——
    /// 碰撞状态由 TemporalObject 明确切换。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TemporalVisualController : MonoBehaviour
    {
        [Header("双年代背景遮罩")]
        [Tooltip("过去背景组根：需含 SpriteMask 与 1x1 矩形遮罩 SpriteRenderer")]
        [SerializeField] private Transform pastMaskRoot;

        [Tooltip("现在背景组根：需含 SpriteMask 与 1x1 矩形遮罩 SpriteRenderer")]
        [SerializeField] private Transform presentMaskRoot;

        [Tooltip("房间左端（背景延伸起点，通常取 boundaryMin 再向左留量）")]
        [SerializeField] private float roomLeft = 0f;

        [Tooltip("房间右端")]
        [SerializeField] private float roomRight = 32f;

        [Tooltip("遮罩向左右各外扩的世界单位，避免边缘裁切")]
        [SerializeField] private float maskPadding = 2f;

        [Header("边界线")]
        [Tooltip("竖直边界线渲染体（放在 BoundaryFx 排序层）")]
        [SerializeField] private Transform boundaryLine;

        [Tooltip("边界线默认高度")]
        [SerializeField] private float boundaryLineHeight = 12f;

        [Tooltip("房间中心 Y（边界线放置参考）")]
        [SerializeField] private float roomCenterY = 0f;

        private BoundaryController boundary;

        /// <summary>组装：由 LevelRoot 在房间激活时调用（连接本房间的边界控制器）。</summary>
        public void Bind(BoundaryController boundaryController)
        {
            if (boundary != null)
            {
                boundary.OnBoundaryCommitted -= HandleBoundaryCommitted;
            }
            boundary = boundaryController;
            if (boundary != null)
            {
                boundary.OnBoundaryCommitted += HandleBoundaryCommitted;
                HandleBoundaryCommitted(boundary.CommittedBoundary);
            }
        }

        private void HandleBoundaryCommitted(float b)
        {
            UpdateMask(pastMaskRoot, roomLeft - maskPadding, b);
            UpdateMask(presentMaskRoot, b, roomRight + maskPadding);
            if (boundaryLine != null)
            {
                boundaryLine.position = new Vector3(b, roomCenterY, 0f);
                boundaryLine.localScale = new Vector3(1f, boundaryLineHeight, 1f);
            }
        }

        private static void UpdateMask(Transform maskRoot, float left, float right)
        {
            if (maskRoot == null || right <= left)
            {
                if (maskRoot != null)
                {
                    maskRoot.localScale = Vector3.zero; // 关闭该侧显示
                }
                return;
            }
            float width = right - left;
            maskRoot.position = new Vector3((left + right) * 0.5f, maskRoot.position.y, 0f);
            maskRoot.localScale = new Vector3(width, maskRoot.localScale.y, 1f);
        }

        private void OnValidate()
        {
            if (boundary != null)
            {
                HandleBoundaryCommitted(boundary.CommittedBoundary);
            }
        }
    }
}
