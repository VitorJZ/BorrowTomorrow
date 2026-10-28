using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
namespace BorrowTomorrow.Narrative
{
    public sealed class SceneMemoryClick : MonoBehaviour
    {
        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (GameSession.Instance == null || !GameSession.Instance.State.IsPlayerControlAllowed || GameSession.Instance.State.IsPaused) return;
            if (ServiceRegistry.TryGet<IPlayerContext>(out var p) && p.IsGrounded && Vector2.Distance(p.Position,transform.position)<2)
                GetComponent<NarrativeInteractable>()?.Interact();
        }
    }
}
