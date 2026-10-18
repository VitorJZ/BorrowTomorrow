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
            properties.SetFloat("_Boundary",value);
            past?.SetPropertyBlock(properties);present?.SetPropertyBlock(properties);
        }
    }
}
