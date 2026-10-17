using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Narrative
{
    public sealed class ClothTidyInteractable : MonoBehaviour, IInteractable
    {
        public Transform cloth;
        public Vector3 chairPosition;
        private bool busy;
        public string InteractId => "tidy_clothes";
        public string InteractPrompt => "整理衣物";
        public bool CanInteract => !busy && GameSession.Instance != null && !GameSession.Instance.StoryFlags.HasFlag("ClothTidyDone");
        private void Start() { if (!CanInteract && cloth != null) cloth.position = chairPosition; }
        public bool Interact() { if (!CanInteract) return false; StartCoroutine(Tidy()); return true; }
        private IEnumerator Tidy()
        {
            busy = true;
            var session = GameSession.Instance;
            session.State.SetSubState(PlayingSubState.Interacting);
            Vector3 from = cloth != null ? cloth.position : transform.position;
            if (ServiceRegistry.TryGet<IAudioService>(out var audio)) audio.PlaySfx("sfx_cloth_paper");
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (cloth != null) cloth.position = Vector3.Lerp(from, chairPosition, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f;
                yield return null;
            }
            if (cloth != null) cloth.position = chairPosition;
            session.StoryFlags.SetFlag("ClothTidyDone");
            if (session.State.SubState == PlayingSubState.Interacting) session.State.SetSubState(PlayingSubState.Free);
        }
    }
}
