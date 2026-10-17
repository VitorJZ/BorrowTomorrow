using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.TimeBoundary;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 安全阻挡测试（策划 4.5 / 11.4）：
    /// 新实体覆盖玩家与脚下支撑撤除都必须阻止切换。
    /// </summary>
    public sealed class TemporalSafetyResolverTests
    {
        private GameObject host;
        private PlayerMock player;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("SafetyTestHost");
            player = ScriptableObject.CreateInstance<PlayerMock>();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
            if (player != null)
            {
                Object.DestroyImmediate(player);
            }
        }

        private TemporalObject BuildTemporalObject(
            float anchorX, bool pastHasCollider, bool presentHasCollider)
        {
            var obj = new GameObject($"temporal_{anchorX}");
            obj.transform.SetParent(host.transform);
            var temporal = obj.AddComponent<TemporalObject>();
            SetField(temporal, "objectId", $"obj_{anchorX}");
            SetField(temporal, "anchorX", anchorX);

            var pastRoot = new GameObject("Past");
            pastRoot.transform.SetParent(obj.transform);
            if (pastHasCollider)
            {
                pastRoot.AddComponent<BoxCollider2D>();
            }
            SetField(temporal, "pastRoot", pastRoot);

            var presentRoot = new GameObject("Present");
            presentRoot.transform.SetParent(obj.transform);
            if (presentHasCollider)
            {
                presentRoot.AddComponent<BoxCollider2D>();
            }
            SetField(temporal, "presentRoot", presentRoot);
            return temporal;
        }

        private static void SetField(TemporalObject target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            switch (value)
            {
                case float f: property.floatValue = f; break;
                case string s: property.stringValue = s; break;
                case GameObject go: property.objectReferenceValue = go; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void NewColliderCoveringPlayer_IsBlocked()
        {
            TemporalObject fence = BuildTemporalObject(5f, pastHasCollider: true, presentHasCollider: false);
            player.BodyBoundsValue = new Bounds(Vector3.zero, Vector3.one);
            player.IsPresentValue = true;

            // 把过去状态（含碰撞）的位置摆到玩家身上：直接平移物体。
            fence.transform.position = Vector3.zero;

            var switching = new[] { fence };
            SafetyBlockReason reason = TemporalSafetyResolver.Evaluate(switching, BorrowTomorrow.Core.Data.Era.Past, player);
            Assert.AreEqual(SafetyBlockReason.BodyOverlapped, reason,
                "新实体覆盖玩家身体时必须阻止（策划 4.5）");
        }

        [Test]
        public void SupportRemovalUnderPlayer_IsBlocked()
        {
            TemporalObject plank = BuildTemporalObject(5f, pastHasCollider: true, presentHasCollider: false);
            player.IsPresentValue = true;
            player.IsGroundedValue = true;
            player.BodyBoundsValue = new Bounds(new Vector3(0f, 1.2f, 0f), Vector3.one);
            player.GroundColliderValue = plank.GetComponentInChildren<Collider2D>();
            // 玩家站在物体的过去碰撞上；目标状态（现在）无碰撞。
            Collider2D pastCollider = plank.transform.Find("Past").GetComponent<Collider2D>();

            SafetyBlockReason reason = TemporalSafetyResolver.Evaluate(
                new[] { plank }, BorrowTomorrow.Core.Data.Era.Present, player);
            Assert.AreEqual(SafetyBlockReason.SupportRemoved, reason,
                "撤除脚下支撑时必须阻止（策划 4.5 统一规则）");
            Assert.IsNotNull(pastCollider);
        }

        [Test]
        public void SafeSwitch_AwayFromPlayer_IsAllowed()
        {
            TemporalObject fence = BuildTemporalObject(5f, pastHasCollider: true, presentHasCollider: false);
            fence.transform.position = new Vector3(20f, 0f, 0f); // 远离玩家
            Physics2D.SyncTransforms(); // EditMode 下把 Transform 变更同步进物理包围盒
            player.IsPresentValue = true;
            player.IsGroundedValue = false;
            player.BodyBoundsValue = new Bounds(Vector3.zero, Vector3.one);

            SafetyBlockReason reason = TemporalSafetyResolver.Evaluate(
                new[] { fence }, BorrowTomorrow.Core.Data.Era.Past, player);
            Assert.AreEqual(SafetyBlockReason.None, reason);
        }

        [Test]
        public void IgnoredPolicy_SkipsChecks()
        {
            TemporalObject decoration = BuildTemporalObject(0f, true, true);
            SetPolicy(decoration, BorrowTomorrow.Core.Data.SafetyPolicy.Ignore);
            decoration.transform.position = Vector3.zero;
            player.IsPresentValue = true;
            player.BodyBoundsValue = new Bounds(Vector3.zero, Vector3.one);

            SafetyBlockReason reason = TemporalSafetyResolver.Evaluate(
                new[] { decoration }, BorrowTomorrow.Core.Data.Era.Past, player);
            Assert.AreEqual(SafetyBlockReason.None, reason, "装饰物体不参与安全校验（策划 4.3）");
        }

        [TestCase(4f, 0f, SafetyBlockReason.SupportRemoved)]
        [TestCase(0f, -2f, SafetyBlockReason.SupportRemoved)]
        [TestCase(0f, 0f, SafetyBlockReason.None)]
        public void ReplacementPlatform_MustRemainUnderFeet(float x, float y, SafetyBlockReason expected)
        {
            var platform = BuildTemporalObject(0f, true, true);
            SetPolicy(platform, BorrowTomorrow.Core.Data.SafetyPolicy.BlockIfSupportRemoved);
            platform.transform.Find("Present").position = new Vector3(x, y, 0f);
            platform.transform.Find("Present").gameObject.SetActive(false);
            player.IsGroundedValue = true;
            player.BodyBoundsValue = new Bounds(new Vector3(0f, 1f, 0f), Vector3.one);
            player.GroundColliderValue = platform.transform.Find("Past").GetComponent<Collider2D>();
            Assert.AreEqual(expected, TemporalSafetyResolver.Evaluate(new[] { platform },
                BorrowTomorrow.Core.Data.Era.Present, player));
        }

        private static void SetPolicy(TemporalObject target, BorrowTomorrow.Core.Data.SafetyPolicy policy)
        {
            var so = new SerializedObject(target);
            so.FindProperty("safetyPolicy").enumValueFlag = (int)policy;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>IPlayerContext 测试替身。</summary>
        private sealed class PlayerMock : ScriptableObject, IPlayerContext
        {
            public Vector2 Position => Vector2.zero;
            public Bounds BodyBoundsValue = new Bounds(Vector3.zero, Vector3.one);
            public Bounds BodyBounds => BodyBoundsValue;
            public bool IsGroundedValue;
            public bool IsGrounded => IsGroundedValue;
            public bool IsClimbingLadder => false;
            public Collider2D GroundColliderValue;
            public Collider2D GroundCollider => GroundColliderValue;
            public bool IsPresentValue = true;
            public bool IsPresent => IsPresentValue;

            public void TeleportTo(ISpawnPoint spawn)
            {
            }

            public void TeleportToPosition(Vector2 position)
            {
            }
        }
    }
}
