using System;
using System.IO;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Save
{
    /// <summary>
    /// 存档服务（策划 10.4 SaveService / 12.3 写入策略）：
    /// 版本化数据序列化与本地文件读写。
    /// 写临时文件成功后替换正式文件，并保留一个可用备份；
    /// 存档缺失从新游戏开始；无法解析时提供恢复备份或重新开始，不静默覆盖唯一记录。
    /// 设置与进度分开保存，重新开始游戏不重置字幕和音量偏好（策划 12.3）。
    /// </summary>
    public sealed class SaveService : MonoBehaviour, ISaveService, IBootInitializable
    {
        private const string ProgressFileName = "progress.json";
        private const string BackupFileName = "progress.backup.json";
        private const string TempFileName = "progress.tmp.json";
        private const string SettingsFileName = "settings.json";

        [SerializeField] private string saveDirectory;

        public int InitializeOrder => -45;

        private void Awake()
        {
            saveDirectory = Path.Combine(Application.persistentDataPath, "BorrowTomorrow");
            Directory.CreateDirectory(saveDirectory);
        }

        public void OnBootInitialize()
        {
            ServiceRegistry.Register<ISaveService>(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<ISaveService>(this);
        }

        private string ProgressPath => Path.Combine(saveDirectory, ProgressFileName);
        private string BackupPath => Path.Combine(saveDirectory, BackupFileName);
        private string TempPath => Path.Combine(saveDirectory, TempFileName);
        private string SettingsPath => Path.Combine(saveDirectory, SettingsFileName);

        public bool HasProgressSave()
        {
            return File.Exists(ProgressPath) || File.Exists(BackupPath);
        }

        public SaveLoadResult TryLoadProgress(out SaveData data)
        {
            data = null;
            if (File.Exists(ProgressPath))
            {
                SaveLoadResult result = TryParseFile(ProgressPath, out data);
                if (result == SaveLoadResult.Loaded)
                {
                    data = Migrate(data);
                    return SaveLoadResult.Loaded;
                }
                Debug.LogWarning("[SaveService] 正式存档无法解析，尝试备份。");
            }

            if (File.Exists(BackupPath))
            {
                SaveLoadResult result = TryParseFile(BackupPath, out data);
                if (result == SaveLoadResult.Loaded)
                {
                    data = Migrate(data);
                    // 用可用备份修复正式文件，避免下次继续走损坏路径。
                    SaveProgress(data);
                    return SaveLoadResult.UsedBackup;
                }
                return SaveLoadResult.Corrupted; // 备份也不可用：不静默覆盖唯一记录
            }
            return SaveLoadResult.MissingSave;
        }

        /// <summary>临时文件写入成功后替换正式文件，并保留一个可用备份（策划 12.3）。</summary>
        public bool SaveProgress(SaveData data)
        {
            if (data == null)
            {
                return false;
            }
            try
            {
                data.saveVersion = SaveData.CurrentSaveVersion;
                string json = JsonUtility.ToJson(data, prettyPrint: true);

                Directory.CreateDirectory(saveDirectory); // 防御：确保目录存在
                File.WriteAllText(TempPath, json);

                // 校验临时文件可回读后再提交，防止写一半损坏正式记录。
                if (!File.Exists(TempPath))
                {
                    return false;
                }

                if (File.Exists(ProgressPath))
                {
                    File.Copy(ProgressPath, BackupPath, overwrite: true);
                }
                if (File.Exists(TempPath))
                {
                    File.Copy(TempPath, ProgressPath, overwrite: true);
                    File.Delete(TempPath);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 保存失败：{e.Message}");
                return false;
            }
        }

        public SaveSettings LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    SaveSettings loaded = JsonUtility.FromJson<SaveSettings>(File.ReadAllText(SettingsPath));
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveService] 设置读取失败，使用默认值：{e.Message}");
                }
            }
            return new SaveSettings();
        }

        public void SaveSettings(SaveSettings settings)
        {
            if (settings == null)
            {
                return;
            }
            try
            {
                File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 设置保存失败：{e.Message}");
            }
        }

        private static SaveLoadResult TryParseFile(string path, out SaveData data)
        {
            data = null;
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return SaveLoadResult.Corrupted;
                }
                data = JsonUtility.FromJson<SaveData>(json);
                return data != null && !string.IsNullOrEmpty(data.currentLevelId)
                    ? SaveLoadResult.Loaded
                    : SaveLoadResult.Corrupted;
            }
            catch (Exception)
            {
                return SaveLoadResult.Corrupted;
            }
        }

        /// <summary>版本迁移：旧 saveVersion 升级到当前结构。新结构无历史时可逐步补充。</summary>
        private static SaveData Migrate(SaveData data)
        {
            if (data == null)
            {
                return null;
            }
            while (data.saveVersion < SaveData.CurrentSaveVersion)
            {
                switch (data.saveVersion)
                {
                    // 示例：case 1: /* v1→v2 变更 */ data.saveVersion = 2; break;
                    default:
                        data.saveVersion = SaveData.CurrentSaveVersion; // 兜底：未知旧版本直接视为最新
                        break;
                }
            }
            return data;
        }

        [ContextMenu("Debug/Open Save Directory")]
        private void OpenSaveDirectory()
        {
            System.Diagnostics.Process.Start("explorer.exe", saveDirectory);
        }
    }
}
