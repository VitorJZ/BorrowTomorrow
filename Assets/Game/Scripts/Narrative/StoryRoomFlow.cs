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
            if (room.RoomId != "Epilogue" || !f.HasFlag("EpilogueSeen")) finished = false;
            // Objectives are rebuilt from completed story flags first (spec 1.4 §4.4/§6.1):
            // returning to an earlier room never rolls the goal back behind the reveal.
            string text = session.CurrentObjective;
            switch (room.RoomId)
            {
                case "Epilogue": text = null; break;
                case "L00": case "L01A": case "L01B": case "L02": case "L02Indoor": case "L03": case "L04":
                    if (f.HasFlag("BoxLocated")) text = "已告知晓晓铁盒的位置。";
                    else if (f.HasFlag("IdentityKnown"))
                        text = room.RoomId == "L04" && f.HasFlag("CompartmentSeen") ? "靠近窗边，确认铁盒藏处。" : Objectives.TellXiaoxaoBoxLocation;
                    else if (room.RoomId == "L04") text = Objectives.TellXiaoxaoBoxLocation;
                    else if (f.HasFlag("UniformRead")) text = "穿过楼上回廊，前往阁楼门前。";
                    // L00 开场只教操作；读完腾空通知、由独白引出铁盒后才显示首个目标。
                    else if (room.RoomId == "L00" && !f.HasFlag("NoticeRead") && !f.HasFlag("CrateBlocked")) text = null;
                    else if (room.RoomId == "L00") text = "去阁楼，找到蓝色铁皮盒。";
                    else if (room.RoomId == "L01A") text = f.HasFlag("Mechanism_L01A_BridgeLowered") || f.HasFlag("HintPassed_L01A_Canal")
                        ? "沿放下的木桥过渠，进入院子。" : "借过去的摇柄装置，放下水渠木桥。";
                    else if (room.RoomId == "L01B") text = "登上楼上的阳台。";
                    else if (room.RoomId == "L02") text = "通过后院外台，进入缝纫间。";
                    else if (room.RoomId == "L02Indoor") text = "查看缝纫桌上的校服。";
                    else text = "穿过楼上回廊，前往阁楼门前。";
                    break;
            }
            if (text != previousObjective || text != session.CurrentObjective) { previousObjective = text; session.SetObjective(text); }
            if (room.RoomId == "Epilogue" && f.HasFlag("EpilogueSeen") && !finished
                && session.State.SubState == PlayingSubState.Free
                && ServiceRegistry.TryGet<IDialogueService>(out var d) && !d.IsPlaying)
            { finished = true; session.CompleteLevel(); }
        }
    }
}
