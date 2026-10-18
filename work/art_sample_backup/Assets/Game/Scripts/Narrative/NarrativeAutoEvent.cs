using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Narrative
{
    // Polls actual room/player/mechanism state so loading inside a trigger cannot lose a story event.
    public sealed class NarrativeAutoEvent : MonoBehaviour
    {
        public string roomId;
        public DialogueEntry entry;
        public bool blocking = true;
        public bool requireGrounded = true;
        public float minimumX = -10000f;
        public float maximumX = 10000f;
        public float minimumY = -10000f;
        public string presentMechanism;
        private float readySeconds;
        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null || session.State.Phase != GamePhase.Playing
                || !ServiceRegistry.TryGet<ILevelRoot>(out var room) || room.RoomId != roomId) { readySeconds = 0f; return; }
            readySeconds += Time.deltaTime;
            if (readySeconds < 0.65f || entry == null || DialoguePlayer.Completed(entry)
                || !DialoguePlayer.RequirementsMet(entry.requiredFlags)
                || session.State.SubState != PlayingSubState.Free) return;
            if (!ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsPresent
                || player.Position.x < minimumX || player.Position.x > maximumX || player.Position.y < minimumY
                || (requireGrounded && !player.IsGrounded)) return;
            if (!string.IsNullOrEmpty(presentMechanism))
            {
                bool present = false;
                foreach (var state in room.CollectMechanismStates())
                    if (state.objectId == presentMechanism && state.Era == Era.Present) present = true;
                if (!present) return;
            }
            if (ServiceRegistry.TryGet<IDialogueService>(out var dialogue) && !dialogue.IsPlaying)
                dialogue.Play(entry, blocking ? DialoguePriority.Story : DialoguePriority.Ambient);
        }
    }
}
