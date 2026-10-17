using BorrowTomorrow.Core.Data;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;
using UnityEngine;

namespace BorrowTomorrow.Tests
{
    public sealed class TemporalObjectRegistryTests
    {
        [TestCase(-2f, Era.Present)]
        [TestCase(2f, Era.Past)]
        public void ResetBeforeFirstQuery_DiscoversInactiveObjectsAndUsesCorrectSide(float boundary, Era expected)
        {
            var root = new GameObject("Registry");
            try
            {
                var registry = root.AddComponent<TemporalObjectRegistry>();
                var child = new GameObject("Temporal");
                child.transform.SetParent(root.transform);
                child.SetActive(false);
                var temporal = child.AddComponent<TemporalObject>(); // 默认锚点为 0
                registry.ResetToBoundary(boundary);
                Assert.AreEqual(expected, temporal.CurrentEra);
                Assert.AreEqual(expected, temporal.EvaluateEra(boundary));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
