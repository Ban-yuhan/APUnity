using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APProto
{
    public class HUD : MonoBehaviour
    {
        [Serializable]
        public class RegionUI
        {
            public Region region;
            public Button departButton;
            public TMP_Text departLabel;
            public Button unlockButton;
            public TMP_Text unlockLabel;
        }

        [SerializeField] TMP_Text moneyText;
        [SerializeField] TMP_Text reputationText;
        [SerializeField] TMP_Text carryText;
        [SerializeField] Carrier playerCarrier;
        [SerializeField] TMP_Text notificationText;
        [SerializeField] float notificationDuration = 3f;
        [SerializeField] RegionUI[] regions;

        float notificationTimer;

        void Start()
        {
            var gm = GameManager.Instance;
            gm.Notified += OnNotified;
            notificationText.gameObject.SetActive(false);

            foreach (var ui in regions)
            {
                var region = ui.region;
                ui.departButton.onClick.AddListener(() => gm.GetLine(region).Truck.TryDepart());
                ui.unlockButton.onClick.AddListener(() => gm.TryUnlockRegion(region));
                var data = gm.Config.GetRegion(region);
                ui.unlockLabel.text = $"{data.displayName} 노선 해금 ({data.unlockCost:N0}원)";
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.Notified -= OnNotified;
        }

        void OnNotified(string message, Color color)
        {
            notificationText.text = message;
            notificationText.color = color;
            notificationText.gameObject.SetActive(true);
            notificationTimer = notificationDuration;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            moneyText.text = $"{Mathf.FloorToInt(gm.Money):N0}원";
            reputationText.text = $"평판 {gm.Reputation}";
            carryText.text = $"소지 {playerCarrier.Count}/{playerCarrier.Capacity}";

            if (notificationTimer > 0 && (notificationTimer -= Time.deltaTime) <= 0)
                notificationText.gameObject.SetActive(false);

            foreach (var ui in regions)
            {
                var line = gm.GetLine(ui.region);
                bool unlocked = line.gameObject.activeSelf;
                ui.departButton.gameObject.SetActive(unlocked);
                ui.unlockButton.gameObject.SetActive(!unlocked);
                if (!unlocked) continue;

                var truck = line.Truck;
                string name = gm.Config.GetRegion(ui.region).displayName;
                ui.departButton.interactable = truck.CanDepart;
                ui.departLabel.text = !truck.IsDocked ? $"[{name}] 트럭 이동 중"
                    : truck.IsFull ? $"[{name}] 만차! {Mathf.Max(1, Mathf.CeilToInt(truck.AutoDepartRemaining))}초 후 자동출발"
                    : truck.CanDepart ? $"[{name}] 배송 출발 ({truck.Cargo.Count}/{truck.Capacity})"
                    : $"[{name}] 최소 {gm.Config.minDepartureCount}개 적재 대기 ({truck.Cargo.Count}/{truck.Capacity})";
            }
        }
    }
}
