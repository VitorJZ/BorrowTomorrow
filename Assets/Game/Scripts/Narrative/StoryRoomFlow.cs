using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Narrative
{
    public sealed class StoryRoomFlow : MonoBehaviour
    {
        private string previousObjective;
        private bool finished;
        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing
                || !ServiceRegistry.TryGet<ILevelRoot>(out var room)) return;
            var f = session.StoryFlags;
            if (room.RoomId != "Epilogue") finished = false;
            string text = session.CurrentObjective;
            switch (room.RoomId)
            {
                case "L00": text = "去阁楼，找到蓝色铁皮盒。"; break;
                case "L01A": text = "跨过水渠，前往院子深处。"; break;
                case "L01B": text = "借木阶梯与长高的树枝，登上缝纫间外廊。"; break;
                case "L02": text = "通过后院外台，进入缝纫间。"; break;
                case "L02Indoor": text = f.HasFlag("UniformRead") ? "穿过楼上回廊，前往阁楼门前。" : "查看缝纫桌上的校服。"; break;
                case "L03": text = f.HasFlag("IdentityKnown") ? Objectives.TellXiaoxaoBoxLocation : "穿过楼上回廊，前往阁楼门前。"; break;
                case "L04": text = f.HasFlag("BoxLocated") ? "已告知晓晓铁盒的位置。" : f.HasFlag("CompartmentSeen") ? "靠近窗边，确认铁盒藏处。" : Objectives.TellXiaoxaoBoxLocation; break;
                case "Epilogue": text = null; break;
            }
            if (text != previousObjective || text != session.CurrentObjective) { previousObjective = text; session.SetObjective(text); }
            if (room.RoomId == "Epilogue" && f.HasFlag("EpilogueSeen") && !finished
                && session.State.SubState == PlayingSubState.Free
                && ServiceRegistry.TryGet<IDialogueService>(out var d) && !d.IsPlaying)
            { finished = true; session.CompleteLevel(); }
        }
    }
}
