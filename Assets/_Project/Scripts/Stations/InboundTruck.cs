using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace APProto
{
    // 입고 트럭: 정차(20초) -> 회차 -> 배차 대기 -> 후진 진입. X축으로만 이동.
    public class InboundTruck : MonoBehaviour
    {
        enum State { Docked, Departing, Away, Arriving }

        [SerializeField] float dockX = -28f;
        [SerializeField] float offscreenX = -52f;
        [SerializeField] TMP_Text statusLabel;

        public readonly List<Package> Cargo = new();
        public bool IsDocked => state == State.Docked;

        State state = State.Docked;
        float dockTimer, earlyDepartTimer, intervalTimer;
        GameConfig cfg;

        void Start()
        {
            cfg = GameManager.Instance.Config;
            Refill();
        }

        void Refill()
        {
            Cargo.Clear();
            for (int i = 0; i < cfg.inboundTruckCapacity; i++) Cargo.Add(new Package());
            dockTimer = cfg.inboundTruckWaitTime;
            earlyDepartTimer = 0;
        }

        public Package TakeOne()
        {
            var pkg = Cargo[^1];
            Cargo.RemoveAt(Cargo.Count - 1);
            return pkg;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var pos = transform.position;
            switch (state)
            {
                case State.Docked:
                    // 하차 완료 시 0.5초 뒤 즉시 회차
                    if (Cargo.Count == 0)
                    {
                        earlyDepartTimer += Time.deltaTime;
                        if (earlyDepartTimer >= 0.5f)
                        {
                            state = State.Departing;
                            gm.Notify("모든 하차 완료! 입고 트럭이 즉시 회차합니다.", Palette.Blue);
                        }
                        break;
                    }
                    earlyDepartTimer = 0;
                    dockTimer -= Time.deltaTime;
                    if (dockTimer <= 0)
                    {
                        state = State.Departing;
                        gm.Notify($"트럭 대기시간 종료! 잔여 택배 {Cargo.Count}개를 실은 채 회차합니다.", Palette.Orange);
                    }
                    break;

                case State.Departing:
                    pos.x -= cfg.inboundDepartSpeed * Time.deltaTime;
                    if (pos.x < offscreenX)
                    {
                        state = State.Away;
                        intervalTimer = UpgradeManager.Instance.TruckInterval * GameManager.Instance.FeverIntervalMultiplier;
                    }
                    break;

                case State.Away:
                    if (!WeatherManager.Instance.CanInbound) break;  // 폭설 미제설: 배차 정지
                    intervalTimer -= Time.deltaTime;
                    if (intervalTimer <= 0)
                    {
                        state = State.Arriving;
                        pos.x = offscreenX;
                        Refill();
                    }
                    break;

                case State.Arriving:
                    pos.x += cfg.inboundArriveSpeed * Time.deltaTime;
                    if (pos.x >= dockX)
                    {
                        pos.x = dockX;
                        state = State.Docked;
                        dockTimer = cfg.inboundTruckWaitTime;
                        gm.Notify($"새로운 입고 배송 트럭이 도착했습니다! (택배 {Cargo.Count}개)", Palette.Blue);
                    }
                    break;
            }
            transform.position = pos;

            if (statusLabel)
                statusLabel.text = state == State.Docked
                    ? $"입고 트럭  {Cargo.Count}/{cfg.inboundTruckCapacity}  ·  {Mathf.CeilToInt(dockTimer)}초"
                    : $"다음 트럭 {Mathf.CeilToInt(Mathf.Max(0, intervalTimer))}초";
        }
    }
}
