using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace APProto
{
    // WASD/방향키 + 좌클릭(터치) 드래그 가상 조이스틱 이동
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] Vector2 boundsX = new(-32f, 24f);
        [SerializeField] Vector2 boundsZ = new(-13f, 13f);
        [Tooltip("메가허브(3단계) 확장 후 동쪽 이동 한계")]
        [SerializeField] float megahubMaxX = 31.5f;
        [SerializeField] float turnSpeed = 900f;

        [Header("가상 조이스틱")]
        [SerializeField] float joystickRadius = 40f;
        [SerializeField] float joystickDeadzone = 6f;
        [SerializeField] RectTransform joystickBase;
        [SerializeField] RectTransform joystickKnob;

        [Tooltip("Model 교체 시 걷기 애니메이션용 (float 파라미터 Speed)")]
        [SerializeField] Animator animator;

        static readonly int SpeedParam = Animator.StringToHash("Speed");

        CharacterController controller;
        bool joystickActive;
        Vector2 joystickStart;

        PlayerVehicles vehicles;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            vehicles = GetComponent<PlayerVehicles>();
        }

        void Update()
        {
            Vector3 dir = ReadKeyboard();
            if (dir == Vector3.zero) dir = ReadJoystick();

            // 지게차 탑승 중이면 지게차를 운전 (위치는 PlayerVehicles 가 동기화)
            if (vehicles && vehicles.Forklift)
            {
                vehicles.Forklift.Drive(dir);
                return;
            }

            float speed = UpgradeManager.Instance.PlayerSpeed * WeatherManager.Instance.SpeedMultiplier;
            if (vehicles && vehicles.Cart) speed *= GameManager.Instance.Config.cartSpeedMultiplier;
            if (dir != Vector3.zero)
            {
                controller.Move(dir * (speed * Time.deltaTime));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
            }

            var p = transform.position;
            p.x = Mathf.Clamp(p.x, boundsX.x, UpgradeManager.Instance.HasMegahub ? megahubMaxX : boundsX.y);
            p.z = Mathf.Clamp(p.z, boundsZ.x, boundsZ.y);
            p.y = 0;
            transform.position = p;

            if (animator) animator.SetFloat(SpeedParam, dir.magnitude * speed);
        }

        static Vector3 ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector3.zero;
            float x = 0, z = 0;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1;
            return new Vector3(x, 0, z).normalized;
        }

        Vector3 ReadJoystick()
        {
            var pointer = Pointer.current;
            if (pointer == null) return Vector3.zero;

            if (pointer.press.wasPressedThisFrame && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
            {
                joystickActive = true;
                joystickStart = pointer.position.ReadValue();
                SetJoystickVisual(true, joystickStart, joystickStart);
            }
            if (!joystickActive) return Vector3.zero;
            if (!pointer.press.isPressed)
            {
                joystickActive = false;
                SetJoystickVisual(false, default, default);
                return Vector3.zero;
            }

            Vector2 delta = pointer.position.ReadValue() - joystickStart;
            float dist = delta.magnitude;
            SetJoystickVisual(true, joystickStart, joystickStart + Vector2.ClampMagnitude(delta, joystickRadius));
            if (dist <= joystickDeadzone) return Vector3.zero;

            // 화면 위 = 월드 +Z(북쪽), 화면 오른쪽 = +X
            float intensity = Mathf.Min(1f, (dist - joystickDeadzone) / (joystickRadius - joystickDeadzone));
            Vector2 n = delta / dist * intensity;
            return new Vector3(n.x, 0, n.y);
        }

        void SetJoystickVisual(bool visible, Vector2 basePos, Vector2 knobPos)
        {
            if (!joystickBase || !joystickKnob) return;
            joystickBase.gameObject.SetActive(visible);
            joystickKnob.gameObject.SetActive(visible);
            joystickBase.position = basePos;
            joystickKnob.position = knobPos;
        }
    }
}
