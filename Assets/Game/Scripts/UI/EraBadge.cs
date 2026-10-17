using System.Collections;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using UnityEngine;

namespace BorrowTomorrow.UI
{
    /// <summary>
    /// 年代角标（策划 4.2）：过门转场后重新标清两侧年代。
    /// 短暂显示当前主导年代（1994 / 2024）。
    /// </summary>
    public sealed class EraBadge : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private UnityEngine.UI.Text badgeText;
        [SerializeField] private float displaySeconds = 1.8f;

        private Coroutine running;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }
            root.SetActive(false);
        }

        public void Hide()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            if (root != null) root.SetActive(false);
        }

        public void Flash(Era dominantEra)
        {
            if (running != null)
            {
                StopCoroutine(running);
            }
            running = StartCoroutine(FlashRoutine(dominantEra));
        }

        private IEnumerator FlashRoutine(Era era)
        {
            root.SetActive(true);
            if (badgeText != null)
            {
                badgeText.text = era.Label();
            }
            yield return new WaitForSeconds(displaySeconds);
            root.SetActive(false);
            running = null;
        }
    }
}
