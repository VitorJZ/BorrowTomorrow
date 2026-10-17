using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.TimeBoundary
{
    /// <summary>
    /// 年代物体（策划 10.4 TemporalObject）：稳定 ID、锚点、过去/现在两套状态。
    /// 场景结构约定：本组件所在物体下有两个子物体（Past / Present），
    /// 各自携带该年代的 SpriteRenderer 与 Collider2D；按锚点与已提交边界整体切换。
    /// 关键年代物体以锚点为准整体切换，不做“可见一半却有整块碰撞”的模糊情况（策划 4.4）。
    /// </summary>
    public sealed class TemporalObject : MonoBehaviour
    {
        [Header("定义（策划 10.5，可留空使用下方后备字段）")]
        [SerializeField] private TemporalObjectDefinition definition;

        [Header("后备配置（definition 为空时生效）")]
        [SerializeField] private string objectId = "temporal_object";
        [SerializeField] private float anchorX = 0f;
        [SerializeField] private SafetyPolicy safetyPolicy = SafetyPolicy.FullCheck;
        [SerializeField] private float anchorHysteresis = 0.05f;
        [SerializeField] private string switchSfxId = "temporal_switch";

        [Header("双年代子物体")]
        [Tooltip("过去（1994）状态的子物体根")]
        [SerializeField] private GameObject pastRoot;
        [Tooltip("现在（2024）状态的子物体根")]
        [SerializeField] private GameObject presentRoot;

        [Header("视觉过渡")]
        [Tooltip("切换后新状态以约 0.15 秒的淡入遮掩（策划 11.2）")]
        [SerializeField] private float fadeInSeconds = 0.15f;

        private Era currentEra = Era.Past;
        private TemporalObjectRegistry registeredRegistry;

        private void Awake()
        {
            // 注册到所在房间的注册表（策划 10.3：LevelRoot 汇集本房间 TemporalObjects）。
            registeredRegistry = GetComponentInParent<TemporalObjectRegistry>();
            if (registeredRegistry == null)
            {
                Debug.LogWarning($"[TemporalObject] {ObjectId} 不在任何 TemporalObjectRegistry 下，无法参与边界系统。");
            }
            else
            {
                registeredRegistry.Register(this);
            }
        }

        private void OnDestroy()
        {
            registeredRegistry?.Unregister(this);
        }

        /// <summary>当前年代状态。</summary>
        public Era CurrentEra => currentEra;

        public string ObjectId => definition != null ? definition.objectId : objectId;

        public float AnchorX => definition != null ? definition.anchorX : anchorX;

        public float Hysteresis => definition != null ? definition.anchorHysteresis : anchorHysteresis;

        public SafetyPolicy SafetyPolicy => definition != null ? definition.safetyPolicy : safetyPolicy;

        public string SwitchSfxId => definition != null ? definition.switchSfxId : switchSfxId;

        /// <summary>年代状态应用完成（物理与渲染都已切换）。</summary>
        public event System.Action<TemporalObject, Era> EraApplied;

        /// <summary>
        /// 依据边界与滞回计算本物体应处的年代（策划 4.4：锚点附近保持原状态防抖动）。
        /// </summary>
        public Era EvaluateEra(float boundaryX)
        {
            if (Mathf.Abs(boundaryX - AnchorX) <= Hysteresis)
            {
                return currentEra;
            }
            return boundaryX < AnchorX ? Era.Past : Era.Present;
        }

        /// <summary>
        /// 应用年代状态。immediate=true 用于关卡初始化与存档恢复（无过渡、无音效）。
        /// 依据策划 11.2：先提交物理状态，再在短过渡内完成 Sprite 替换。
        /// </summary>
        public void ApplyEra(Era era, bool immediate = false)
        {
            if (era == currentEra && IsRootActive(era))
            {
                return;
            }
            currentEra = era;
            if (pastRoot != null)
            {
                pastRoot.SetActive(era == Era.Past);
            }
            if (presentRoot != null)
            {
                presentRoot.SetActive(era == Era.Present);
            }
            if (!immediate)
            {
                StartFadeIn(era);
                if (!string.IsNullOrEmpty(SwitchSfxId) && ServiceRegistry.TryGet<IAudioService>(out var audio))
                {
                    audio.PlaySfx(SwitchSfxId, transform.position);
                }
            }
            EraApplied?.Invoke(this, era);
        }

        private bool IsRootActive(Era era)
        {
            GameObject root = era == Era.Past ? pastRoot : presentRoot;
            return root == null || root.activeSelf;
        }

        private void StartFadeIn(Era era)
        {
            if (fadeInSeconds <= 0f)
            {
                return;
            }
            GameObject root = era == Era.Past ? pastRoot : presentRoot;
            if (root != null)
            {
                StartCoroutine(FadeInRoutine(root));
            }
        }

        private IEnumerator FadeInRoutine(GameObject root)
        {
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            float t = 0f;
            while (t < fadeInSeconds)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Lerp(0.55f, 1f, t / fadeInSeconds);
                foreach (SpriteRenderer r in renderers)
                {
                    if (r != null)
                    {
                        Color c = r.color;
                        c.a = alpha;
                        r.color = c;
                    }
                }
                yield return null;
            }
            foreach (SpriteRenderer r in renderers)
            {
                if (r != null)
                {
                    Color c = r.color;
                    c.a = 1f;
                    r.color = c;
                }
            }
        }

        /// <summary>指定年代状态下是否拥有碰撞（即可站立/阻挡的判断基础）。</summary>
        public bool HasColliderIn(Era era)
        {
            GameObject root = era == Era.Past ? pastRoot : presentRoot;
            if (root == null)
            {
                return false;
            }
            return root.GetComponentsInChildren<Collider2D>(true).Length > 0;
        }

        /// <summary>
        /// 指定年代状态的主要碰撞包围盒（世界坐标）。
        /// 供安全校验做“新实体覆盖玩家”检测（策划 11.4：使用预先配置的碰撞代理或形状查询）。
        /// </summary>
        public bool TryGetStateBounds(Era era, out Bounds bounds)
        {
            GameObject root = era == Era.Past ? pastRoot : presentRoot;
            bounds = default;
            if (root == null)
            {
                return false;
            }
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            if (colliders.Length == 0)
            {
                // 无碰撞的装饰年代：用渲染范围兜底（不参与通行时无影响）。
                SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0)
                {
                    return false;
                }
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                return true;
            }
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
            return true;
        }

        /// <summary>玩家当前脚下的碰撞体是否属于本物体（支撑撤除检测）。</summary>
        public bool IsSupporting(Collider2D groundCollider)
        {
            if (groundCollider == null)
            {
                return false;
            }
            return groundCollider.transform == transform
                || (groundCollider.transform.parent != null && groundCollider.transform.parent == transform)
                || groundCollider.GetComponentInParent<TemporalObject>() == this;
        }

        private void OnDrawGizmosSelected()
        {
            // 锚点小标记：策划 4.4，锚点用物件底座小标记提示。
            Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
            Vector3 p = new Vector3(AnchorX, transform.position.y, 0f);
            Gizmos.DrawLine(p + Vector3.up * 0.4f, p - Vector3.up * 0.4f);
            Gizmos.DrawWireSphere(p, 0.08f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + Vector3.up * 0.55f, $"{ObjectId} @ {AnchorX}");
#endif
        }
    }
}
