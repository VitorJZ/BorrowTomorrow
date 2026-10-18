using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Player
{
    /// <summary>
    /// 玩家上下文适配器：把 PlayerMotor2D 的状态以 IPlayerContext
    /// 形式提供给时间安全校验等系统（依赖倒置，避免模块横向依赖）。
    /// </summary>
    public sealed class PlayerContext : IPlayerContext
    {
        private readonly PlayerMotor2D motor;
        private readonly CapsuleCollider2D capsule;

        public PlayerContext(PlayerMotor2D motor)
        {
            this.motor = motor;
            capsule = motor.GetComponent<CapsuleCollider2D>();
        }

        public Vector2 Position => motor.transform.position;

        public Bounds BodyBounds => capsule != null ? capsule.bounds : new Bounds((Vector3)motor.transform.position, Vector3.one);

        public bool IsGrounded => motor.IsGrounded;

        public bool IsClimbingLadder => motor.IsClimbingLadder;

        public Collider2D GroundCollider => motor.GroundCollider;

        public bool IsPresent => motor != null && motor.isActiveAndEnabled;

        public void TeleportTo(ISpawnPoint spawn)
        {
            if (spawn != null)
            {
                motor.TeleportTo(spawn.Position);
            }
        }

        public void TeleportToPosition(Vector2 position)
        {
            motor.TeleportTo(position);
        }

        /// <summary>玩家是否满足开始拖动边界的身体条件（站稳、非攀爬）。</summary>
        public bool CanBeginBoundaryAdjust => motor.CanBeginBoundaryAdjust;
    }
}
