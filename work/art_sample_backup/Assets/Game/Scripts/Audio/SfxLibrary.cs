using UnityEngine;

namespace BorrowTomorrow.Audio
{
    /// <summary>
    /// 音效库：稳定 SFX id → AudioClip 映射（策划 09 声音清单）。
    /// 挂在系统根上，由 AudioService 检索。
    /// 声音清单：主角脚步、木地板、石地、梯子、跳落、互动、门轴、
    /// 边界拖动、切换成功、切换受阻、磁带按键、磁带底噪、
    /// 缝纫机、风、鸟、狗、铁盒、布料。
    /// </summary>
    public sealed class SfxLibrary : MonoBehaviour
    {
        [System.Serializable]
        public sealed class SfxEntry
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [SerializeField] private SfxEntry[] entries = new SfxEntry[0];

        public bool TryGetClip(string id, out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 1f;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            foreach (SfxEntry entry in entries)
            {
                if (entry != null && entry.id == id && entry.clip != null)
                {
                    clip = entry.clip;
                    volume = entry.volume;
                    return true;
                }
            }
            return false;
        }
    }
}
