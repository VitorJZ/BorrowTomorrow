using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;
namespace BorrowTomorrow.Narrative
{
    public sealed class PresentOnlyInvestigation : MonoBehaviour
    {
        public float anchorX;
        private NarrativeInteractable target;
        private Collider2D[] areas;
        private Renderer[] visuals;
        private bool[] areaEnabled;
        private bool[] visualEnabled;
        private bool? wasVisible;

        private void Awake()
        {
            target = GetComponent<NarrativeInteractable>();
            areas = GetComponentsInChildren<Collider2D>(true);
            visuals = GetComponentsInChildren<Renderer>(true);
            areaEnabled = new bool[areas.Length];
            visualEnabled = new bool[visuals.Length];
            for (int i = 0; i < areas.Length; i++) areaEnabled[i] = areas[i].enabled;
            for (int i = 0; i < visuals.Length; i++) visualEnabled[i] = visuals[i].enabled;
        }

        private void OnEnable() { wasVisible = null; RefreshVisibility(); }
        private void Update() => RefreshVisibility();

        private void RefreshVisibility()
        {
            bool visible = ServiceRegistry.TryGet<IBoundaryContext>(out var boundary)
                && boundary.EraAtAnchor(anchorX) == Core.Data.Era.Present;
            if (wasVisible == visible) return;
            wasVisible = visible;
            if (target != null) target.enabled = visible;
            // Art skins live below the original graybox renderer. Preserve each
            // component's configured state so returning to Present does not revive it.
            for (int i = 0; i < areas.Length; i++)
                if (areas[i] != null) areas[i].enabled = visible && areaEnabled[i];
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i] != null) visuals[i].enabled = visible && visualEnabled[i];
        }
    }
}
