using UnityEngine;

namespace APProto
{
    // 운반 카트 (중형 물류센터부터). 플레이어가 밀면 소지량 8, 이동속도 +30%, 짐은 카트 적재함에 표시.
    public class Cart : MonoBehaviour
    {
        [SerializeField] PackageStack cargoStack;
        [Tooltip("미는 사람 앞 거리")]
        [SerializeField] float pushDistance = 1.4f;

        public static readonly System.Collections.Generic.List<Cart> Active = new();

        public PackageStack CargoStack => cargoStack;
        public Transform Pusher { get; set; }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void LateUpdate()
        {
            if (!Pusher) return;
            transform.SetPositionAndRotation(Pusher.position + Pusher.forward * pushDistance, Pusher.rotation);
        }
    }
}
