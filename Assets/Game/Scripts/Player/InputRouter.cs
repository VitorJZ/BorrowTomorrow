using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Player
{
    /// <summary>
    /// 输入路由（策划 10.4 InputRouter）：统一输出移动、跳跃、交互、
    /// 边界调整（Q/R）、恢复、提示、目标与暂停意图。
    /// 所有屏幕提示从同一输入映射读取（策划 5.2）；玩法代码不直接读按键。
    /// 挂在系统根上，注册为 IInputSource。
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class InputRouter : MonoBehaviour, IInputSource, IBootInitializable
    {
        [SerializeField] private InputBindings bindings = new InputBindings();

        public int InitializeOrder => -50;

        private bool interactConsumedThisFrame;

        private void Awake()
        {
            ServiceRegistry.Register<IInputSource>(this);
        }

        public void OnBootInitialize()
        {
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<IInputSource>(this);
        }

        private void Update()
        {
            interactConsumedThisFrame = false;
        }

        public bool ConsumeInteract()
        {
            if (!InteractPressedThisFrame || interactConsumedThisFrame)
            {
                return false;
            }
            interactConsumedThisFrame = true;
            return true;
        }

        public float MoveAxis => InputBindings.ReadAxis(bindings.moveLeft, bindings.moveRight);

        public float ClimbAxis => InputBindings.ReadAxis(bindings.climbDown, bindings.climbUp);

        public bool JumpPressedThisFrame => bindings.AnyDown(bindings.jump);

        public bool JumpHeld => bindings.AnyHeld(bindings.jump);

        public bool InteractPressedThisFrame => bindings.AnyDown(bindings.interact);

        public float BoundaryNudgeDirection =>
            (bindings.AnyHeld(bindings.boundaryRight) ? 1f : 0f) -
            (bindings.AnyHeld(bindings.boundaryLeft) ? 1f : 0f);

        public bool RespawnPressedThisFrame => bindings.AnyDown(bindings.respawn);

        public bool ObjectiveHeld => bindings.AnyHeld(bindings.objectiveRecords);

        public bool PausePressedThisFrame => bindings.AnyDown(bindings.pause);
    }
}
