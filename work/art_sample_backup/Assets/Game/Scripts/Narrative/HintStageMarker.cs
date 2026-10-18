using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// 解谜提示阶段完成标记（规格 1.4 §6.3）：玩家稳定站到机关之后的区域即写入 stageFlag，
    /// HintDirector 随即切到下一阶段，不再反复显示已通过机关的提示。
    /// </summary>
    public sealed class HintStageMarker : MonoBehaviour
    {
        public string roomId;
        public string stageFlag;
        public float minimumX = -10000f;
        public float maximumX = 10000f;
        public float minimumY = -10000f;

        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing || string.IsNullOrEmpty(stageFlag)
                || session.StoryFlags.HasFlag(stageFlag)
                || !ServiceRegistry.TryGet<ILevelRoot>(out var room) || room.RoomId != roomId
                || !ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsPresent || !player.IsGrounded) return;
            Vector2 p = player.Position;
            if (p.x >= minimumX && p.x <= maximumX && p.y >= minimumY) session.StoryFlags.SetFlag(stageFlag);
        }
    }
}
