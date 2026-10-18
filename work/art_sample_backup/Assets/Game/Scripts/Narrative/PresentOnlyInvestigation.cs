using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Narrative
{
    public sealed class PresentOnlyInvestigation : MonoBehaviour
    {
        public float anchorX;
        private NarrativeInteractable target;
        private Collider2D area;
        private Renderer visual;
        private void Awake() { target=GetComponent<NarrativeInteractable>();area=GetComponent<Collider2D>();visual=GetComponent<Renderer>(); }
        private void Update()
        {
            bool visible=ServiceRegistry.TryGet<IBoundaryContext>(out var b)&&b.EraAtAnchor(anchorX)==Core.Data.Era.Present;
            if(target!=null)target.enabled=visible;
            if(area!=null)area.enabled=visible;
            if(visual!=null)visual.enabled=visible;
        }
    }
}
