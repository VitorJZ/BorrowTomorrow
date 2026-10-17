using BorrowTomorrow.Core.Data;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 边界阈值排序与分组测试（策划 11.4：必须检查中间阈值，同一阈值作为一笔变更）。
    /// </summary>
    public sealed class BoundaryChangePlanTests
    {
        [Test]
        public void RightwardDrag_CollectsAnchorsInAscendingOrder()
        {
            var plan = BoundaryChangePlan.Build(0f, 10f, new[]
            {
                (3f, "A"), (7f, "B"), (12f, "C"), (1f, "D"),
            });

            Assert.AreEqual(1, plan.Direction);
            Assert.AreEqual(3, plan.Steps.Count, "(0,10] 内应有 3 个阈值：1、3、7");
            Assert.AreEqual(1f, plan.Steps[0].AnchorX, 1e-4f);
            Assert.AreEqual(3f, plan.Steps[1].AnchorX, 1e-4f);
            Assert.AreEqual(7f, plan.Steps[2].AnchorX, 1e-4f);
            // 不应包含区间外的 12。
            CollectionAssert.DoesNotContain(new[] { 1f, 3f, 7f }, 12f);
        }

        [Test]
        public void LeftwardDrag_CollectsAnchorsInDescendingOrder()
        {
            var plan = BoundaryChangePlan.Build(10f, 2f, new[]
            {
                (3f, "A"), (7f, "B"), (2.5f, "C"),
            });

            Assert.AreEqual(-1, plan.Direction);
            // 策划 4.2 语义：左拖收集 [to, from) 内锚点并降序——2.5 也在区间内。
            Assert.AreEqual(3, plan.Steps.Count, "[2,10) 内应有 3 个阈值：7、3、2.5");
            Assert.AreEqual(7f, plan.Steps[0].AnchorX, 1e-4f);
            Assert.AreEqual(3f, plan.Steps[1].AnchorX, 1e-4f);
            Assert.AreEqual(2.5f, plan.Steps[2].AnchorX, 1e-4f);
        }

        [Test]
        public void SameAnchorObjects_AreGroupedInOneStep()
        {
            var plan = BoundaryChangePlan.Build(0f, 10f, new[]
            {
                (5f, "A"), (5.0004f, "B"), (5.0008f, "C"), (8f, "D"),
            });

            Assert.AreEqual(2, plan.Steps.Count, "同一阈值（±0.001）应合并为一笔变更");
            Assert.AreEqual(3, plan.Steps[0].GetPayloads<string>().Count);
            Assert.AreEqual("D", plan.Steps[1].GetPayloads<string>()[0]);
        }

        [Test]
        public void StepTargetEra_FollowsBoundarySemantics()
        {
            // 策划 4.2：锚点 x < B → 过去；x > B → 现在。
            BoundaryChangePlan rightward = BoundaryChangePlan.Build(0f, 10f, new[] { (5f, "A") });
            Assert.AreEqual(Era.Past, rightward.Steps[0].TargetEra,
                "右移越过锚点后物体应进入过去（过去覆盖更多）");

            BoundaryChangePlan leftward = BoundaryChangePlan.Build(10f, 0f, new[] { (5f, "A") });
            Assert.AreEqual(Era.Present, leftward.Steps[0].TargetEra,
                "左移越过锚点后物体应进入现在");
        }

        [Test]
        public void SafeBoundaryBefore_KeepsObjectsInOriginalEra()
        {
            BoundaryChangePlan rightward = BoundaryChangePlan.Build(0f, 10f, new[] { (5f, "A") });
            Assert.AreEqual(5f - BoundaryChangePlan.Epsilon, rightward.Steps[0].SafeBoundaryBefore, 1e-4f,
                "右拖受阻时应停在锚点左侧（保持现在）");
            Assert.AreEqual(5f + BoundaryChangePlan.Epsilon, rightward.Steps[0].BoundaryAfter, 1e-4f,
                "右拖成功后应停在锚点右侧（进入过去）");
        }

        [Test]
        public void NoCrossedAnchors_ProducesEmptyPlan()
        {
            var plan = BoundaryChangePlan.Build(0f, 2f, new[] { (5f, "A") });
            Assert.IsTrue(plan.IsEmpty);
        }
    }
}
