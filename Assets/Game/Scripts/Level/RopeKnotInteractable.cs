using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Core.Data;
using UnityEngine;
namespace BorrowTomorrow.Level
{
    public sealed class RopeKnotInteractable : MonoBehaviour, IInteractable
    {
        public RopeDryingRack mechanism;
        public string InteractId => "L01B_tree_knot_v7";
        public string InteractPrompt => "解开绳结";
        public bool CanInteract => mechanism != null && mechanism.CanReleaseKnot;
        public bool Interact() => mechanism != null && mechanism.TryReleaseKnot();
    }
}
