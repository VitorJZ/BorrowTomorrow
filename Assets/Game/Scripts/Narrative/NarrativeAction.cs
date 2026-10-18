using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 事件内动作阶段（规格 1.4 §4.6）：在 beforeEntryId 显示前插入，不新增对白条目或完成标记。
    /// 顺序：[confirmBefore 等待独立按下] → 动作演出 seconds 秒 → [confirmAfter 等待独立按下] → 进入 beforeEntryId。
    /// </summary>
    [CreateAssetMenu(fileName = "ACT_New", menuName = "BorrowTomorrow/Narrative Action")]
    public sealed class NarrativeAction : ScriptableObject
    {
        public string actionId;
        [Tooltip("动作完成后进入的对白条目")]
        public string beforeEntryId;
        [Tooltip("动作开始前的确认提示（如“抚平布角”）；为空则动作随事件输入直接开始")]
        public string confirmBefore;
        [Tooltip("动作结束后的确认提示（如“告诉晓晓位置”）；为空则直接进入对白")]
        public string confirmAfter;
        public float seconds = 1.2f;
        public string sfxId;
        [Tooltip("主角动作名（ICharacterArt.PlayGesture）；病房等无主角画面留空")]
        public string gesture;

        [System.Serializable]
        public sealed class Layer
        {
            public string name;
            public Texture2D[] frames;
            public float start;
            public float end = 1;
            public float alphaBefore = 1;
            public float alphaAfter = 1;
            public Vector2 pivot = new Vector2(.5f,.5f);
            public Vector2 offsetBefore;
            public Vector2 offsetAfter;
            public Vector2 scaleBefore = Vector2.one;
            public Vector2 scaleAfter = Vector2.one;
            public Vector3 rotationAfter;
            // A local patch uses the same UV rectangle; the rest of the composition remains fixed.
            public Rect region = new Rect(0,0,1,1);
        }
        [Header("同构图动作分层（绘制顺序由后向前）")]
        public Texture2D stageBackground;
        public Layer[] layers = new Layer[0];

        [Header("插画前后状态")]
        [Tooltip("动作前状态；未提供正式图时由 afterImage + 遮挡层占位")]
        public Texture2D beforeImage;
        public Texture2D afterImage;
        [Tooltip("占位遮挡层（插画归一化坐标，左下为原点）；宽度为 0 则不用")]
        public Rect coverRect;
        public Color coverColor = Color.white;
        [Tooltip("动作过程中遮挡层位移（以插画尺寸为单位）")]
        public Vector2 coverSlide;
        [Tooltip("动作结束时的镜头推近倍数，1 为不推近；仅保留到紧随的第一句")]
        public float zoom = 1f;
        [Tooltip("推近中心（插画归一化坐标）")]
        public Vector2 focus = new Vector2(0.5f, 0.5f);
    }

    /// <summary>动作阶段画面；由 UI 层实现，progress 0→1，prompt 为空表示不等待确认。</summary>
    public interface INarrativeActionView
    {
        void PresentAction(NarrativeAction action, float progress, string prompt, bool locksControl);
    }
}
