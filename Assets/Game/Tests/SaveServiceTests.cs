using System;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using BorrowTomorrow.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 存档序列化往返测试（策划 15.5）：
    /// 版本化数据、临时文件替换、备份恢复路径。
    /// </summary>
    public sealed class SaveServiceTests
    {
        private SaveService service;
        private string originalDirectory;

        [SetUp]
        public void SetUp()
        {
            var host = new GameObject("SaveServiceHost");
            service = host.AddComponent<SaveService>();
            // 独立测试目录，避免污染真实存档。
            SetPrivateDirectory(System.IO.Path.Combine(Application.temporaryCachePath, "BT_SaveTests_" + Guid.NewGuid().ToString("N")));
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(originalDirectory) && System.IO.Directory.Exists(originalDirectory))
            {
                System.IO.Directory.Delete(originalDirectory, true);
            }
            if (service != null)
            {
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        private void SetPrivateDirectory(string directory)
        {
            // EditMode 测试中 AddComponent 不触发 Awake，需手动确保目录存在。
            System.IO.Directory.CreateDirectory(directory);
            var so = new SerializedObject(service);
            so.FindProperty("saveDirectory").stringValue = directory;
            so.ApplyModifiedPropertiesWithoutUndo();
            originalDirectory = directory;
        }

        [Test]
        public void SaveAndLoad_RoundtripPreservesData()
        {
            var data = new SaveData
            {
                currentLevelId = "L02",
                checkpointId = "cp_L02_mid",
                currentObjective = "找到铁盒的位置，告诉晓晓。",
            };
            data.roomBoundaries.Add(new RoomBoundarySnapshot("L02", 11.5f));
            data.mechanismStates.Add(new MechanismStateSnapshot("old_ladder", Era.Past));
            data.triggeredStoryIds.Add(StoryFlags.IntroPlayed);
            data.triggeredStoryIds.Add(StoryFlags.UniformRead);

            Assert.IsTrue(service.SaveProgress(data), "保存应成功");
            Assert.IsTrue(service.HasProgressSave());

            SaveLoadResult result = service.TryLoadProgress(out SaveData loaded);
            Assert.AreEqual(SaveLoadResult.Loaded, result);
            Assert.AreEqual("L02", loaded.currentLevelId);
            Assert.AreEqual("cp_L02_mid", loaded.checkpointId);
            Assert.AreEqual("找到铁盒的位置，告诉晓晓。", loaded.currentObjective);
            Assert.AreEqual(1, loaded.roomBoundaries.Count);
            Assert.AreEqual(11.5f, loaded.roomBoundaries[0].boundaryX, 1e-4f);
            Assert.AreEqual(1, loaded.mechanismStates.Count);
            Assert.AreEqual(Era.Past, loaded.mechanismStates[0].Era);
            CollectionAssert.AreEquivalent(
                new[] { StoryFlags.IntroPlayed, StoryFlags.UniformRead }, loaded.triggeredStoryIds);
        }

        [Test]
        public void MissingSave_ReportsMissing()
        {
            SaveLoadResult result = service.TryLoadProgress(out _);
            Assert.AreEqual(SaveLoadResult.MissingSave, result, "无存档时应报告缺失（从新游戏开始）");
        }

        [Test]
        public void CorruptedMainFile_FallsBackToBackup()
        {
            var data = new SaveData { currentLevelId = "L01", checkpointId = "cp_x" };
            Assert.IsTrue(service.SaveProgress(data));
            Assert.IsTrue(service.SaveProgress(data), "第二次保存应产生备份（策划 12.3）");

            // 破坏正式文件。
            string progressPath = System.IO.Path.Combine(originalDirectory, "progress.json");
            System.IO.File.WriteAllText(progressPath, "{ corrupted !!!");

            SaveLoadResult result = service.TryLoadProgress(out SaveData loaded);
            Assert.AreEqual(SaveLoadResult.UsedBackup, result, "正式存档损坏时应恢复备份");
            Assert.IsNotNull(loaded);
            Assert.AreEqual("L01", loaded.currentLevelId);
        }

        [Test]
        public void Settings_SavedSeparatelyFromProgress()
        {
            var settings = new SaveSettings { volumeMaster = 0.5f, subtitlesEnabled = false };
            service.SaveSettings(settings);
            service.SaveProgress(new SaveData { currentLevelId = "L00", checkpointId = "cp" });

            SaveSettings loaded = service.LoadSettings();
            Assert.AreEqual(0.5f, loaded.volumeMaster, 1e-4f);
            Assert.IsFalse(loaded.subtitlesEnabled);
        }

        [Test]
        public void SaveDataVersion_IsCurrentOnWrite()
        {
            var data = new SaveData { currentLevelId = "L00", saveVersion = 0 };
            service.SaveProgress(data);
            service.TryLoadProgress(out SaveData loaded);
            Assert.AreEqual(SaveData.CurrentSaveVersion, loaded.saveVersion, "写出存档应带当前版本号");
        }
    }
}
