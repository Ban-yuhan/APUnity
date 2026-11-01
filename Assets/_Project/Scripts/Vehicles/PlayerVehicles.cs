using UnityEngine;
using UnityEngine.InputSystem;

namespace APProto
{
    // E 키 (또는 HUD 탑승 버튼): 근처 지게차 탑승 / 카트 밀기 / 하차 (원본 toggleVehicleRide)
    [RequireComponent(typeof(Carrier))]
    public class PlayerVehicles : MonoBehaviour
    {
        [Tooltip("지게차 탑승 중 숨길 외형")]
        [SerializeField] GameObject model;

        public Forklift Forklift { get; private set; }
        public Cart Cart { get; private set; }

        Carrier carrier;
        CharacterController controller;

        void Awake()
        {
            carrier = GetComponent<Carrier>();
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (Time.timeScale > 0 && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Toggle();
        }

        // HUD 버튼 표시용: 지금 E 를 누르면 무슨 일이 일어나는지 (없으면 null)
        public string ActionLabel =>
            Forklift ? "지게차 하차 (E)"
            : Cart ? "카트 놓기 (E)"
            : NearestForklift() ? "지게차 탑승 (E)"
            : NearestCart() ? "카트 밀기 (E)"
            : null;

        public void Toggle()
        {
            var gm = GameManager.Instance;
            if (Forklift)
            {
                Forklift.UnboardPlayer();
                transform.position = Forklift.transform.position + Forklift.transform.right * 1.8f;
                Physics.SyncTransforms();
                Forklift = null;
                SetRiding(false);
                gm.Notify("지게차에서 내렸습니다.", Color.gray);
                return;
            }
            if (Cart)
            {
                Cart.Pusher = null;
                Cart = null;
                carrier.SetDisplay(null);
                gm.Notify("카트 손잡이를 놓았습니다.", Color.gray);
                return;
            }

            var fork = NearestForklift();
            if (fork)
            {
                Forklift = fork;
                fork.BoardPlayer();
                SetRiding(true);
                gm.Notify("지게차 탑승! (WASD/조이스틱: 운전, E: 하차) 출고 선반 꺼내기 발판에서 파렛트를 뜨세요.", Palette.Orange);
                return;
            }

            var cart = NearestCart();
            if (cart)
            {
                Cart = cart;
                cart.Pusher = transform;
                carrier.SetDisplay(cart.CargoStack);
                gm.Notify($"운반 카트 장착! (이동속도 +{(gm.Config.cartSpeedMultiplier - 1) * 100:0}%, 소지량 {gm.Config.cartCapacity}개 / E: 놓기)", Palette.Blue);
            }
        }

        void SetRiding(bool riding)
        {
            model.SetActive(!riding);
            controller.enabled = !riding;
        }

        void LateUpdate()
        {
            if (Forklift) transform.position = Forklift.transform.position;
        }

        Forklift NearestForklift()
        {
            float range = GameManager.Instance.Config.forkliftBoardRange;
            Forklift best = null;
            foreach (var f in Forklift.Active)
            {
                float d = Vector3.Distance(f.transform.position, transform.position);
                if (f.HasDriver || d >= range) continue;
                range = d;
                best = f;
            }
            return best;
        }

        Cart NearestCart()
        {
            float range = GameManager.Instance.Config.cartBoardRange;
            Cart best = null;
            foreach (var c in Cart.Active)
            {
                float d = Vector3.Distance(c.transform.position, transform.position);
                if (c.Pusher || d >= range) continue;
                range = d;
                best = c;
            }
            return best;
        }
    }
}
