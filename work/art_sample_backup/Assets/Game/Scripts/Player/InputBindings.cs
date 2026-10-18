using System;
using UnityEngine;

namespace BorrowTomorrow.Player
{
    /// <summary>
    /// 可配置键位（策划 5.1 桌面键鼠默认操作 / 5.2 允许重绑关键输入）。
    /// 首版通过 Inspector 直接调整；运行时重绑界面属后续增强。
    /// </summary>
    [Serializable]
    public sealed class InputBindings
    {
        [Header("移动（A/D 或左右箭头）")]
        public KeyCode[] moveLeft = { KeyCode.A, KeyCode.LeftArrow };
        public KeyCode[] moveRight = { KeyCode.D, KeyCode.RightArrow };

        [Header("攀爬（W/S 或上下箭头）")]
        public KeyCode[] climbUp = { KeyCode.W, KeyCode.UpArrow };
        public KeyCode[] climbDown = { KeyCode.S, KeyCode.DownArrow };

        [Header("动作")]
        public KeyCode[] jump = { KeyCode.Space };
        public KeyCode[] interact = { KeyCode.E };

        [Header("时间边界（Q/R 为键盘替代操作）")]
        public KeyCode[] boundaryLeft = { KeyCode.Q };
        public KeyCode[] boundaryRight = { KeyCode.R };

        [Header("系统")]
        public KeyCode[] respawn = { KeyCode.Backspace };
        public KeyCode[] hint = { KeyCode.H };
        public KeyCode[] objectiveRecords = { KeyCode.Tab };
        public KeyCode[] pause = { KeyCode.Escape };

        public bool AnyDown(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKeyDown(keys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public bool AnyHeld(KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKey(keys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static float ReadAxis(KeyCode[] negative, KeyCode[] positive)
        {
            float value = 0f;
            for (int i = 0; i < negative.Length; i++)
            {
                if (Input.GetKey(negative[i]))
                {
                    value -= 1f;
                    break;
                }
            }
            for (int i = 0; i < positive.Length; i++)
            {
                if (Input.GetKey(positive[i]))
                {
                    value += 1f;
                    break;
                }
            }
            return Mathf.Clamp(value, -1f, 1f);
        }
    }
}
