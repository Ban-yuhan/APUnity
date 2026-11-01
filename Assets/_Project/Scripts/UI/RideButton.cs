using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APProto
{
    // 터치용 탑승/하차 버튼. 근처에 탈것이 있거나 탑승 중일 때만 보임.
    public class RideButton : MonoBehaviour
    {
        [SerializeField] PlayerVehicles vehicles;
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;

        void Start() => button.onClick.AddListener(vehicles.Toggle);

        void Update()
        {
            string action = vehicles.ActionLabel;
            button.gameObject.SetActive(action != null);
            if (action != null) label.text = action;
        }
    }
}
