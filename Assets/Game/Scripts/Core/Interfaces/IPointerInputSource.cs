using UnityEngine;

namespace BorrowTomorrow.Core.Interfaces
{
    /// Optional pointer view. Shares the input router with keyboard controls so
    /// direct manipulation can be replayed through the actual Update path.
    public interface IPointerInputSource
    {
        Vector2 PointerPosition { get; }
        bool PrimaryPressedThisFrame { get; }
        bool PrimaryHeld { get; }
        bool SecondaryPressedThisFrame { get; }
        bool HintPressedThisFrame { get; }
    }
}
