using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Level
{
    // The hook offers a clue only before the rope is released; never captures rope dragging.
    public sealed class RopeHookInteractable : MonoBehaviour, IInteractable
    {
        public RopeDryingRack mechanism;
        public string InteractId => "L01B_rope_hook_inspect_v16";
        public string InteractPrompt => "查看绕绳钩";
        public bool CanInteract => mechanism != null && mechanism.CanInspectHook;
        public bool Interact() => mechanism != null && mechanism.TryInspectHook();
    }
}
