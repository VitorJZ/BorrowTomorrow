using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Save;
using NUnit.Framework;
using UnityEngine;

namespace BorrowTomorrow.Tests
{
    public sealed class CheckpointSnapshotTests
    {
        private CheckpointService service;
        [SetUp] public void Setup() => service = new GameObject("CheckpointTest").AddComponent<CheckpointService>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(service.gameObject);

        [Test] public void PendingSave_RoundtripRetainsRoomAndFallbackPosition()
        {
            var source = new SaveData { currentLevelId = "L01", checkpointRoomId = "L01B",
                hasCheckpointPosition = true, checkpointPosition = new Vector2(45f, 2f) };
            source.roomBoundaries.Add(new RoomBoundarySnapshot("L01B", 60f));
            service.RestoreFrom(source);
            var target = new SaveData { currentLevelId = "L01" };
            service.WriteTo(target);
            Assert.AreEqual("L01B", target.checkpointRoomId);
            Assert.IsTrue(target.hasCheckpointPosition);
            Assert.AreEqual(source.checkpointPosition, target.checkpointPosition);
        }
        [Test] public void DifferentLevel_DoesNotSerializeOldCheckpoint()
        {
            service.RestoreFrom(new SaveData { currentLevelId = "L01", checkpointId = "old" });
            var target = new SaveData { currentLevelId = "L02" };
            service.WriteTo(target);
            Assert.IsNull(target.checkpointId);
        }
        [Test] public void NewRestore_ReplacesPreviousPendingSnapshotAndClearRemovesIt()
        {
            service.RestoreFrom(new SaveData { currentLevelId = "L01", checkpointId = "old" });
            service.RestoreFrom(new SaveData { currentLevelId = "L02", checkpointId = "new" });
            var target = new SaveData { currentLevelId = "L02" };
            service.WriteTo(target);
            Assert.AreEqual("new", target.checkpointId);
            service.RestoreFrom(null);
            Assert.IsFalse(service.HasCheckpoint);
        }
    }
}
