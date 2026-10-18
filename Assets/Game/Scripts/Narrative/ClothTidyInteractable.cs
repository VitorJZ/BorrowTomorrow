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
        public Sprite completedSprite;
        private bool busy;
        public string InteractId => "tidy_clothes";
        public string InteractPrompt => "整理衣物";
        public bool CanInteract => isActiveAndEnabled && !busy && GameSession.Instance != null && !GameSession.Instance.StoryFlags.HasFlag("ClothTidyDone");
        private void Start() { if (!CanInteract && cloth != null) ShowCompleted(); }
        private void ShowCompleted()
        {
            if(cloth==null)return;
            cloth.position=chairPosition;
            var renderer=cloth.GetComponent<SpriteRenderer>();
            if(renderer!=null && completedSprite!=null)
            {
                renderer.sprite=completedSprite;renderer.enabled=true;
                cloth.localScale=new Vector3(.9f/completedSprite.bounds.size.x,1.4f/completedSprite.bounds.size.y,1);
            }
        }
        public bool Interact() { if (!CanInteract) return false; StartCoroutine(Tidy()); return true; }
        private IEnumerator Tidy()
        {
            busy = true;
            var session = GameSession.Instance;
            session.State.SetSubState(PlayingSubState.Interacting);
            bool animated=ServiceRegistry.TryGet<ICharacterArt>(out var character);
            if(animated)character.PlayGesture("PickUp");
            Vector3 from = cloth != null ? cloth.position : transform.position;
            if (ServiceRegistry.TryGet<IAudioService>(out var audio)) audio.PlaySfx("sfx_cloth_paper");
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (cloth != null) cloth.position = Vector3.Lerp(from, chairPosition, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f;
                if(animated && cloth!=null)
                {
                    var renderer=cloth.GetComponent<SpriteRenderer>();
                    if(renderer!=null)renderer.enabled=t<.33f;
                }
                yield return null;
            }
            ShowCompleted();
            session.StoryFlags.SetFlag("ClothTidyDone");
            if (session.State.SubState == PlayingSubState.Interacting) session.State.SetSubState(PlayingSubState.Free);
        }
    }
}
