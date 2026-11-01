using System;
using UnityEngine;

namespace APProto
{
    public enum UpgradeType { PlayerSpeed, PlayerCapacity, PlayerWorkSpeed, WarehouseTier, TruckInterval, DockCapacity, RackCapacity }

    // 플레이어/시설 업그레이드 레벨과 효과 적용 (원본 game.js upgrade* 함수들)
    [DefaultExecutionOrder(-90)]
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        [Tooltip("메가허브(3단계) 확장 하역장 바닥/벽")]
        [SerializeField] GameObject megahubExtension;
        [Tooltip("중형 물류센터(2단계)부터 표시")]
        [SerializeField] GameObject carts;
        [Tooltip("메가허브(3단계)부터 표시")]
        [SerializeField] GameObject forklifts;

        public event Action Changed;

        readonly int[] levels = new int[Enum.GetValues(typeof(UpgradeType)).Length];
        GameConfig cfg;

        void Awake()
        {
            Instance = this;
            cfg = GameManager.Instance.Config;
            for (int i = 0; i < levels.Length; i++) levels[i] = 1;
            levels[(int)UpgradeType.RackCapacity] = cfg.rackStartLevel;
        }

        void Start() => Apply();

        public int Level(UpgradeType t) => levels[(int)t];
        public int MaxLevel(UpgradeType t) => t == UpgradeType.WarehouseTier ? cfg.warehouseTiers.Length : Track(t).values.Length;
        public bool IsMax(UpgradeType t) => Level(t) >= MaxLevel(t);
        public int NextCost(UpgradeType t) => t == UpgradeType.WarehouseTier ? cfg.warehouseTiers[Level(t)].cost : Track(t).costs[Level(t)];

        public GameConfig.UpgradeTrack Track(UpgradeType t) => t switch
        {
            UpgradeType.PlayerSpeed => cfg.playerSpeed,
            UpgradeType.PlayerCapacity => cfg.playerCapacity,
            UpgradeType.PlayerWorkSpeed => cfg.playerWorkSpeed,
            UpgradeType.TruckInterval => cfg.truckInterval,
            UpgradeType.DockCapacity => cfg.dockCapacity,
            UpgradeType.RackCapacity => cfg.rackCapacity,
            _ => null,
        };

        float Value(UpgradeType t) => Track(t).values[Level(t) - 1];

        public int Tier => Level(UpgradeType.WarehouseTier);
        public GameConfig.WarehouseTier TierData => cfg.warehouseTiers[Tier - 1];
        public float PlayerSpeed => Value(UpgradeType.PlayerSpeed);
        public int PlayerCapacity => (int)Value(UpgradeType.PlayerCapacity);
        public float PlayerWorkSpeed => Value(UpgradeType.PlayerWorkSpeed);
        public float TruckInterval => Value(UpgradeType.TruckInterval);
        public int DockCapacity => Mathf.Max((int)Value(UpgradeType.DockCapacity), TierData.minDockCapacity);
        public int RackCapacity => (int)Value(UpgradeType.RackCapacity);
        public bool HasConveyor => Tier >= 2;
        public bool HasMegahub => Tier >= 3;

        public bool TryUpgrade(UpgradeType t)
        {
            var gm = GameManager.Instance;
            if (IsMax(t)) return false;
            int cost = NextCost(t);
            if (gm.Money < cost)
            {
                gm.Notify($"소지금이 부족합니다! (필요: {cost:N0}원)", Palette.Red);
                return false;
            }
            gm.AddMoney(-cost);
            levels[(int)t]++;
            Apply();
            gm.Notify(t == UpgradeType.WarehouseTier
                ? $"시설 확장 완료: [{TierData.displayName}] 축하합니다!"
                : $"{Track(t).displayName} Lv.{Level(t)} 업그레이드 완료!", Palette.Green);
            Changed?.Invoke();
            return true;
        }

        public void AddTestMoney()
        {
            GameManager.Instance.AddMoney(cfg.testMoneyAmount);
            GameManager.Instance.Notify($"테스트 지원금 +{cfg.testMoneyAmount:N0}원이 지급되었습니다!", Palette.Gold);
            Changed?.Invoke();
        }

        void Apply()
        {
            var gm = GameManager.Instance;
            if (megahubExtension) megahubExtension.SetActive(HasMegahub);
            if (carts) carts.SetActive(HasConveyor);
            if (forklifts) forklifts.SetActive(HasMegahub);
            gm.InboundDock.ApplyCapacity();
            foreach (var line in gm.RegionLines)
            {
                line.ApplyFacility(HasConveyor, HasMegahub);
                line.Rack.ApplyCapacity();
            }
        }
    }
}
