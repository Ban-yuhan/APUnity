using System;
using System.Collections.Generic;
using UnityEngine;

namespace APProto
{
    [DefaultExecutionOrder(-100)]  // 다른 컴포넌트 Awake 에서 Instance/Config 사용
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField] InboundTruck inboundTruck;
        [SerializeField] InboundDock inboundDock;
        [SerializeField] RegionLine[] regionLines;

        public GameConfig Config => config;
        public InboundTruck InboundTruck => inboundTruck;
        public InboundDock InboundDock => inboundDock;
        public IReadOnlyList<RegionLine> RegionLines => regionLines;
        public float Money { get; private set; }
        public int Reputation { get; set; }

        public event Action<string, Color> Notified;

        readonly List<Region> unlocked = new();

        void Awake()
        {
            Instance = this;
            Money = config.startMoney;
            Reputation = config.startReputation;
        }

        void Update()
        {
            // 시설 유지비 (0원 밑으로 떨어지지 않음)
            if (Money > 0) Money = Mathf.Max(0, Money - UpgradeManager.Instance.TierData.maintenancePerSecond * Time.deltaTime);

            if (IsFever)
            {
                FeverRemaining -= Time.deltaTime;
                if (FeverRemaining <= 0)
                {
                    IsFever = false;
                    FeverCooldown = config.feverCooldown;
                    Notify("앙팡데이 피버타임이 종료되었습니다.", Color.gray);
                }
            }
            else if (FeverCooldown > 0) FeverCooldown -= Time.deltaTime;
        }

        // ------------------------------------------------------------ 앙팡데이 피버타임

        public bool IsFever { get; private set; }
        public float FeverRemaining { get; private set; }
        public float FeverCooldown { get; private set; }
        public bool CanStartFever => !IsFever && FeverCooldown <= 0;
        public float FeverIntervalMultiplier => IsFever ? config.feverIntervalMultiplier : 1f;
        public float FeverProfitMultiplier => IsFever ? config.feverProfitMultiplier : 1f;

        public bool TryStartFever()
        {
            if (!CanStartFever) return false;
            IsFever = true;
            FeverRemaining = config.feverDuration;
            Notify($"앙팡데이 피버타임 시작! {config.feverDuration:0}초간 배차 간격 {(1 - config.feverIntervalMultiplier) * 100:0}% 단축 및 수익 {config.feverProfitMultiplier:0.#}배!", Palette.Orange);
            return true;
        }

        // 활성화된 RegionLine = 해금된 노선
        public IReadOnlyList<Region> UnlockedRegions
        {
            get
            {
                unlocked.Clear();
                foreach (var line in regionLines)
                    if (line.gameObject.activeSelf) unlocked.Add(line.Region);
                return unlocked;
            }
        }

        public RegionLine GetLine(Region region) => Array.Find(regionLines, l => l.Region == region);

        public void AddMoney(float amount) => Money += amount;

        public bool TryUnlockRegion(Region region)
        {
            var line = GetLine(region);
            if (line == null || line.gameObject.activeSelf) return false;

            var data = config.GetRegion(region);
            if (Money < data.unlockCost)
            {
                Notify("소지금이 부족합니다!", Palette.Red);
                return false;
            }
            Money -= data.unlockCost;
            line.Unlock();
            Notify($"[{data.displayName}] 전용 배송 노선 및 출고 트럭이 활성화되었습니다!", Palette.Green);
            return true;
        }

        public void Notify(string message, Color color) => Notified?.Invoke(message, color);
    }

    public static class Palette
    {
        public static readonly Color Red = new Color32(0xe7, 0x4c, 0x3c, 0xff);
        public static readonly Color Green = new Color32(0x2e, 0xcc, 0x71, 0xff);
        public static readonly Color Blue = new Color32(0x34, 0x98, 0xdb, 0xff);
        public static readonly Color Orange = new Color32(0xe6, 0x7e, 0x22, 0xff);
        public static readonly Color Gold = new Color32(0xf1, 0xc4, 0x0f, 0xff);
    }
}
