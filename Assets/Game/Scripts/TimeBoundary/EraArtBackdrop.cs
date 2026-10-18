using BorrowTomorrow.Core;
using UnityEngine;
namespace BorrowTomorrow.TimeBoundary
{
    public sealed class EraArtBackdrop : MonoBehaviour
    {
        public SpriteRenderer past;
        public SpriteRenderer present;
        private BoundaryController boundary;
        private MaterialPropertyBlock properties;
        private void Awake(){boundary=GetComponentInParent<BoundaryController>();properties=new MaterialPropertyBlock();}
        private void LateUpdate()
        {
            bool active=BorrowTomorrow.Core.ServiceRegistry.TryGet<BorrowTomorrow.Core.Interfaces.IBoundaryContext>(out var b)
                && ReferenceEquals(b,boundary);
            if(past!=null)past.enabled=active;if(present!=null)present.enabled=active;
            if(!active||boundary==null)return;
            float value=boundary.CommittedBoundary;
            if(value>=boundary.RangeMax-.01f)value=100000f;
            else if(value<=boundary.RangeMin+.01f)value=-100000f;
            Apply(past, value); Apply(present, value);
        }
        private void Apply(SpriteRenderer renderer, float value)
        {
            if (renderer == null || renderer.sprite == null) return;
            // A SpriteRenderer also supplies texture properties. Never replace them with an empty block.
            renderer.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", renderer.sprite.texture);
            properties.SetFloat("_Boundary", value);
            renderer.SetPropertyBlock(properties);
        }
    }
}
