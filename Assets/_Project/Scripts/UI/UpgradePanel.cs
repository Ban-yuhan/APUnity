using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APProto
{
    // 업그레이드 창. 열려 있는 동안 게임 일시정지.
    public class UpgradePanel : MonoBehaviour
    {
        [Serializable]
        public class CardUI
        {
            public UpgradeType type;
            public TMP_Text title;
            public TMP_Text body;
            public Button button;
            public TMP_Text buttonLabel;
        }

        [SerializeField] GameObject window;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button testMoneyButton;
        [SerializeField] TMP_Text summaryText;
        [SerializeField] CardUI[] cards;

        void Start()
        {
            var um = UpgradeManager.Instance;
            openButton.onClick.AddListener(() => SetOpen(true));
            closeButton.onClick.AddListener(() => SetOpen(false));
            testMoneyButton.onClick.AddListener(um.AddTestMoney);
            foreach (var c in cards)
            {
                var type = c.type;
                c.button.onClick.AddListener(() => um.TryUpgrade(type));
            }
            um.Changed += Refresh;
            window.SetActive(false);
        }

        void OnDestroy()
        {
            if (UpgradeManager.Instance) UpgradeManager.Instance.Changed -= Refresh;
        }

        public void SetOpen(bool open)
        {
            window.SetActive(open);
            Time.timeScale = open ? 0 : 1;
            Refresh();
        }

        void Refresh()
        {
            if (!window.activeSelf) return;
            var um = UpgradeManager.Instance;
            var gm = GameManager.Instance;
            summaryText.text = $"소지금 {Mathf.FloorToInt(gm.Money):N0}원   ·   시설 유지비 {Mathf.RoundToInt(um.TierData.maintenancePerSecond * 60):N0}원/분";

            foreach (var c in cards)
            {
                int lv = um.Level(c.type);
                bool max = um.IsMax(c.type);
                if (c.type == UpgradeType.WarehouseTier)
                {
                    var tiers = gm.Config.warehouseTiers;
                    c.title.text = $"시설 규모: {um.TierData.displayName}";
                    c.body.text = $"{um.TierData.description}\n\n" +
                                  (max ? "최종 단계 완성!" : $"다음: {tiers[lv].displayName}\n{tiers[lv].description}");
                    c.buttonLabel.text = max ? "MAX" : $"시설 증축 ({um.NextCost(c.type):N0}원)";
                }
                else
                {
                    var track = um.Track(c.type);
                    c.title.text = $"{track.displayName} (Lv.{lv}/{um.MaxLevel(c.type)})";
                    c.body.text = $"현재: {Describe(track, lv)}\n" + (max ? "최고 레벨 달성!" : $"다음: {Describe(track, lv + 1)}");
                    c.buttonLabel.text = max ? "MAX" : $"업그레이드 ({um.NextCost(c.type):N0}원)";
                }
                c.button.interactable = !max;
            }
        }

        static string Describe(GameConfig.UpgradeTrack track, int level) =>
            track.descriptions != null && track.descriptions.Length >= level
                ? track.descriptions[level - 1]
                : $"{track.values[level - 1]:0.##}{track.unit}";
    }
}
