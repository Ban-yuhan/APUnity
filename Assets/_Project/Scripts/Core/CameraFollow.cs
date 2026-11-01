using UnityEngine;

namespace APProto
{
    // 쿼터뷰 카메라가 플레이어를 부드럽게 따라감 (메가허브 확장부가 고정 시점 밖이라 추가)
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new(0, 32, -28);
        [SerializeField] Vector2 clampX = new(-10f, 10f);
        [SerializeField] Vector2 clampZ = new(-4f, 4f);
        [SerializeField] float smoothTime = 0.35f;

        Vector3 velocity;

        void LateUpdate()
        {
            if (!target) return;
            var focus = target.position;
            focus.x = Mathf.Clamp(focus.x, clampX.x, UpgradeManager.Instance && UpgradeManager.Instance.HasMegahub ? clampX.y + 8f : clampX.y);
            focus.z = Mathf.Clamp(focus.z, clampZ.x, clampZ.y);
            focus.y = 0;
            transform.position = Vector3.SmoothDamp(transform.position, focus + offset, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}
