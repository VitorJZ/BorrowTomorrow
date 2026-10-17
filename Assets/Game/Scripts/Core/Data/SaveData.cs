using System;
using System.Collections.Generic;

namespace BorrowTomorrow.Core.Data
{
    /// <summary>
    /// 进度存档数据（策划 12.2）：
    /// saveVersion、currentLevelId、checkpointId、各房间 boundaryX、
    /// 机关状态、已触发剧情 ID、当前目标。
    /// 运行状态用普通可序列化结构（策划 10.5），不依赖场景对象。
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int saveVersion = CurrentSaveVersion;

        public const int CurrentSaveVersion = 1;

        public string currentLevelId;

        public string checkpointId;

        public string currentObjective;

        /// <summary>各房间边界位置（跨房间不共享数值，策划 4.2）。</summary>
        public List<RoomBoundarySnapshot> roomBoundaries = new List<RoomBoundarySnapshot>();

        /// <summary>必要的机关年代状态。</summary>
        public List<MechanismStateSnapshot> mechanismStates = new List<MechanismStateSnapshot>();

        /// <summary>已触发剧情稳定 ID。</summary>
        public List<string> triggeredStoryIds = new List<string>();

        public SaveSettings settings = new SaveSettings();
    }

    /// <summary>单个房间的边界快照。</summary>
    [Serializable]
    public sealed class RoomBoundarySnapshot
    {
        public string roomId;
        public float boundaryX;

        public RoomBoundarySnapshot()
        {
        }

        public RoomBoundarySnapshot(string roomId, float boundaryX)
        {
            this.roomId = roomId;
            this.boundaryX = boundaryX;
        }
    }

    /// <summary>单个年代物体的状态快照（以稳定 objectId 引用）。</summary>
    [Serializable]
    public sealed class MechanismStateSnapshot
    {
        public string objectId;
        public int eraState; // 0 = Past, 1 = Present

        public MechanismStateSnapshot()
        {
        }

        public MechanismStateSnapshot(string objectId, Era era)
        {
            this.objectId = objectId;
            eraState = (int)era;
        }

        public Era Era => (Era)eraState;
    }

    /// <summary>
    /// 玩家偏好设置：与进度分开保存，重新开始游戏不重置（策划 12.3）。
    /// </summary>
    [Serializable]
    public sealed class SaveSettings
    {
        public float volumeMaster = 1f;
        public float volumeMusic = 0.8f;
        public float volumeVoice = 1f;
        public float volumeAmbience = 0.8f;

        /// <summary>字幕默认开启（策划 5.5）。</summary>
        public bool subtitlesEnabled = true;

        /// <summary>字幕字号缩放（无障碍）。</summary>
        public float subtitleScale = 1f;

        /// <summary>屏幕震动默认低强度且可关闭（策划 5.5）。</summary>
        public bool screenShakeEnabled = true;
    }
}
