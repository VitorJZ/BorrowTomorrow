using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Player
{
    /// <summary>
    /// 玩家交互器（策划 10.4 PlayerInteractor）：
    /// 筛选最近可交互物，执行一次互动并更新 E 提示（策划 5.4：只在范围内显示）。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("交互检测半径（世界单位）")]
        [SerializeField] private float detectionRadius = 1.2f;

        [Tooltip("交互物所在层")]
        [SerializeField] private LayerMask interactableMask = ~0;

        private IInputSource input;
        private IHudService hud;
        private IInteractable nearest;

        private void Start()
        {
            ServiceRegistry.TryGet<IInputSource>(out input);
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }
            bool controlAllowed = GameSession.Instance != null
                && GameSession.Instance.State.IsPlayerControlAllowed
                && !GameSession.Instance.State.IsPaused;

            nearest = FindNearestInteractable(out Vector2 nearestPosition);

            if (hud == null && !ServiceRegistry.TryGet<IHudService>(out hud))
            {
                hud = null;
            }
            hud?.SetInteractionPrompt(controlAllowed && nearest != null,
                nearest != null ? nearest.InteractPrompt : null, nearestPosition);

            if (controlAllowed && nearest != null && input.ConsumeInteract())
            {
                nearest.Interact();
            }
        }

        private IInteractable FindNearestInteractable(out Vector2 nearestPosition)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, interactableMask);
            IInteractable best = null;
            float bestSqr = float.MaxValue;
            nearestPosition = transform.position;
            foreach (Collider2D hit in hits)
            {
                IInteractable candidate = hit.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract)
                {
                    continue;
                }
                float sqr = ((Vector2)(hit.transform.position - transform.position)).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                    nearestPosition = hit.transform.position;
                }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
