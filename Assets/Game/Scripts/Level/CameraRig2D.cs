using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 正交相机跟随机（策划 6.1：正交摄像机，Z 仅用于绘制层次）。
    /// 策划 5.3：拖动时镜头保持不动，不能因镜头跟随使边界位置跳变。
    /// </summary>
    public sealed class CameraRig2D : MonoBehaviour, ICameraRig
    {
        [Tooltip("跟随平滑速度（越大越紧跟）")]
        [SerializeField] private float followSpeed = 6f;

        [Tooltip("相机高度与角色的固定 Z 距离")]
        [SerializeField] private float cameraZ = -10f;

        [Tooltip("垂直跟随死区（角色小幅上下移动时相机不动）")]
        [SerializeField] private float verticalDeadZone = 0.4f;

        private Camera ownedCamera;
        private Rect roomBounds;
        private bool followFrozen;
        private float verticalAnchorY;

        private void Awake()
        {
            ownedCamera = GetComponent<Camera>();
            if (ownedCamera != null)
            {
                ownedCamera.orthographic = true;
            }
            ServiceRegistry.Register<ICameraRig>(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<ICameraRig>(this);
        }

        private void LateUpdate()
        {
            if (followFrozen || !ServiceRegistry.TryGet<IPlayerContext>(out var player) || !player.IsPresent)
            {
                return;
            }
            Vector2 target = player.Position;
            float deltaY = target.y - verticalAnchorY;
            if (Mathf.Abs(deltaY) > verticalDeadZone)
            {
                verticalAnchorY += deltaY - Mathf.Sign(deltaY) * verticalDeadZone;
            }
            Vector3 desired = new Vector3(target.x, verticalAnchorY, cameraZ);
            Vector3 next = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
            transform.position = ClampToRoom(next);
        }

        private Vector3 ClampToRoom(Vector3 position)
        {
            if (ownedCamera == null || roomBounds.width <= 0.01f)
            {
                return position;
            }
            float halfH = ownedCamera.orthographicSize;
            float halfW = halfH * ownedCamera.aspect;
            float x = Mathf.Clamp(position.x, roomBounds.xMin + halfW, roomBounds.xMax - halfW);
            float y = Mathf.Clamp(position.y, roomBounds.yMin + halfH, roomBounds.yMax - halfH);
            if (roomBounds.width < halfW * 2f)
            {
                x = roomBounds.center.x;
            }
            if (roomBounds.height < halfH * 2f)
            {
                y = roomBounds.center.y;
            }
            return new Vector3(x, y, position.z);
        }

        public void SetRoomBounds(Rect worldBounds)
        {
            roomBounds = worldBounds;
            if (ownedCamera != null)
            {
                transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, cameraZ);
            }
        }

        /// <summary>拖动边界期间冻结跟随（策划 5.3）。拖动结束保持画面稳定，玩家移动后自然恢复。</summary>
        public void SetFollowFrozen(bool frozen)
        {
            followFrozen = frozen;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireCube(roomBounds.center, new Vector3(roomBounds.width, roomBounds.height, 0.1f));
        }
    }
}
