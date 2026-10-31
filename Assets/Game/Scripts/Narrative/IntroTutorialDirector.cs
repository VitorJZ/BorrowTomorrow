using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    /// <summary>
    /// L00 操作教学节奏：
    /// 1. 磁带结束、人物从屏外走到起点并站稳后，再给 A/D 与空格提示，不弹剧情台词；
    /// 2. 读完通知独白后，玩家继续向前撞上 2024 年木箱，再给拖动时间边界提示；
    /// 3. 玩家把边界拖过木箱后收起提示，不打断后续探索。
    /// 只负责提示的显示时机，对白与旗帜仍由 NarrativeAutoEvent / DialoguePlayer 驱动。
    /// </summary>
    public sealed class IntroTutorialDirector : MonoBehaviour
    {
        [Tooltip("木箱年代物件的锚点 X；边界越过此值后木箱进入 1994 态。")]
        public float crateAnchorX = 12f;

        private const float MinimumControlsSeconds = 2.5f;
        private const float MaximumControlsSeconds = 10f;
        private const float MovementToDismiss = 0.9f;

        private OpeningTapeIntro opening;

        private void Start() => opening = FindObjectOfType<OpeningTapeIntro>();

        private bool controlsShown;
        private bool controlsHidden;
        private bool timelineShown;
        private bool timelineHidden;
        private float controlsShownAt;
        private Vector3 controlsStartPosition;
        private bool controlsStartCaptured;

        private void Update()
        {
            GameSession session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing || session.State.IsPaused)
            {
                return;
            }
            if (!ServiceRegistry.TryGet<IHudService>(out var hud))
            {
                return;
            }

            bool dialoguePlaying = ServiceRegistry.TryGet<IDialogueService>(out var dialogue) && dialogue.IsPlaying;
            if (dialoguePlaying)
            {
                // 任何剧情开始，基础操作提示立刻让位。
                if (controlsShown && !controlsHidden)
                {
                    hud.HideControlsHint();
                    controlsHidden = true;
                }
                return;
            }

            bool introPlayed = session.StoryFlags.HasFlag(StoryFlags.IntroPlayed);
            bool noticeRead = session.StoryFlags.HasFlag(StoryFlags.NoticeRead);
            bool crateBlocked = session.StoryFlags.HasFlag(StoryFlags.CrateBlocked);

            if (!controlsShown && (opening == null || opening.EntranceComplete) && introPlayed && !noticeRead && !crateBlocked
                && session.State.SubState == PlayingSubState.Free)
            {
                hud.ShowControlsHint();
                controlsShown = true;
                controlsShownAt = Time.unscaledTime;
                if (ServiceRegistry.TryGet<IPlayerContext>(out var player) && player.IsPresent)
                {
                    controlsStartPosition = player.Position;
                    controlsStartCaptured = true;
                }
            }

            if (controlsShown && !controlsHidden)
            {
                float elapsed = Time.unscaledTime - controlsShownAt;
                bool moved = controlsStartCaptured
                    && ServiceRegistry.TryGet<IPlayerContext>(out var player)
                    && player.IsPresent
                    && Mathf.Abs(player.Position.x - controlsStartPosition.x) > MovementToDismiss;
                if (noticeRead || crateBlocked || (moved && elapsed >= MinimumControlsSeconds)
                    || elapsed >= MaximumControlsSeconds)
                {
                    hud.HideControlsHint();
                    controlsHidden = true;
                }
            }

            if (!timelineShown && crateBlocked && CrateStillInPresent())
            {
                hud.ShowTimelineHint();
                timelineShown = true;
            }

            if (timelineShown && !timelineHidden)
            {
                bool crossed = ServiceRegistry.TryGet<IBoundaryContext>(out var boundary)
                    && boundary.CommittedBoundary > crateAnchorX + .1f;
                if (crossed || session.StoryFlags.HasFlag(StoryFlags.BoxLocated))
                {
                    hud.HideTimelineHint();
                    timelineHidden = true;
                }
            }
        }

        private bool CrateStillInPresent()
        {
            // 读档回到已切换年代时不要重新闪出提示。
            return !ServiceRegistry.TryGet<IBoundaryContext>(out var boundary)
                || boundary.CommittedBoundary <= crateAnchorX + .05f;
        }
    }
}
