using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace APProto
{
    // 지역 전용 출고 트럭: 정차 -> (수동 출발 / 만차 2초 자동 출발) -> 퇴장 -> 6초 후 후진 진입
    public class OutboundTruck : MonoBehaviour
    {
        enum State { Docked, Departing, Away, Arriving }

        [SerializeField] float dockX = 20.5f;
        [SerializeField] float offscreenX = 65f;
        [SerializeField] TMP_Text statusLabel;

        public readonly List<Package> Cargo = new();
        public Region Region { get; private set; }
        public int Capacity => cfg.outboundTruckCapacity;
        public bool IsDocked => state == State.Docked;
        public bool CanLoad => IsDocked && Cargo.Count < Capacity;
        public bool CanDepart => IsDocked && TotalCount >= cfg.minDepartureCount;
        // 파렛트는 1칸이지만 택배 8개로 환산
        public int TotalCount
        {
            get
            {
                int n = 0;
                foreach (var p in Cargo) n += p.Count;
                return n;
            }
        }
        public bool IsFull => Cargo.Count >= Capacity;
        public float AutoDepartRemaining => cfg.autoDepartDelay - autoDepartTimer;

        State state = State.Docked;
        float autoDepartTimer, awayTimer, dockOffset;
        GameConfig cfg;

        float DockX => dockX + dockOffset;

        // 메가허브 확장 시 정차 위치 이동 (정차 중이면 즉시)
        public float DockOffset
        {
            set
            {
                dockOffset = value;
                if (state != State.Docked) return;
                var p = transform.position;
                p.x = DockX;
                transform.position = p;
            }
        }

        void Awake()
        {
            Region = GetComponentInParent<RegionLine>(true).Region;
            cfg = GameManager.Instance.Config;
        }

        public void BeginArrival()
        {
            Cargo.Clear();
            autoDepartTimer = 0;
            state = State.Arriving;
            transform.position = new Vector3(offscreenX, transform.position.y, transform.position.z);
        }

        public void Load(Package pkg) => Cargo.Add(pkg);

        public bool TryDepart()
        {
            var gm = GameManager.Instance;
            if (!CanDepart)
            {
                gm.Notify($"최소 {cfg.minDepartureCount}개 이상의 택배를 적재해야 출발할 수 있습니다!", Palette.Red);
                return false;
            }

            var region = cfg.GetRegion(Region);
            // 정산: (택배 수 * 단가 * 지역 배율) - 기름값
            float earned = Mathf.Round(TotalCount * cfg.basePrice * region.priceMultiplier * gm.FeverProfitMultiplier * DayNightCycle.Instance.NightProfitMultiplier - cfg.fuelCost);
            if (IsFull)
            {
                earned += cfg.fullLoadBonus;
                gm.Reputation += cfg.fullLoadReputation;
                gm.Notify($"[{region.displayName}] 알뜰적재 달성! 보너스 +{cfg.fullLoadBonus:N0}원 & 평판 상승!", Palette.Gold);
            }
            gm.AddMoney(earned);
            gm.Notify($"[{region.displayName}] 출고 트럭 출발! 정산액: +{earned:N0}원", Palette.Green);

            state = State.Departing;
            autoDepartTimer = 0;
            return true;
        }

        void Update()
        {
            var pos = transform.position;
            switch (state)
            {
                case State.Docked:
                    if (IsFull)
                    {
                        autoDepartTimer += Time.deltaTime;
                        if (autoDepartTimer >= cfg.autoDepartDelay) TryDepart();
                    }
                    else autoDepartTimer = 0;
                    break;

                case State.Departing:
                    pos.x += cfg.outboundDepartSpeed * Time.deltaTime;
                    if (pos.x > offscreenX)
                    {
                        state = State.Away;
                        awayTimer = cfg.outboundAwayTime;
                    }
                    break;

                case State.Away:
                    awayTimer -= Time.deltaTime;
                    if (awayTimer <= 0) BeginArrival();
                    return;

                case State.Arriving:
                    pos.x -= cfg.outboundArriveSpeed * Time.deltaTime;
                    if (pos.x <= DockX)
                    {
                        pos.x = DockX;
                        state = State.Docked;
                        GameManager.Instance.Notify($"[{cfg.GetRegion(Region).displayName}] 새로운 출고 배송 트럭이 대기 중입니다.", Palette.Green);
                    }
                    break;
            }
            transform.position = pos;

            if (statusLabel)
            {
                string name = cfg.GetRegion(Region).displayName;
                statusLabel.text = autoDepartTimer > 0
                    ? $"[{name}] 자동출발: {AutoDepartRemaining:0.0}초"
                    : $"[{name}] 적재: {Cargo.Count}/{Capacity}";
            }
        }
    }
}
