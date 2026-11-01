using UnityEngine;

namespace APProto
{
    // 발판 위에 서 있으면 자동 상호작용 (원본 game.js updatePlayerPadInteraction)
    [RequireComponent(typeof(Carrier))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] float hintCooldown = 2.5f;

        Carrier carrier;
        Pad current;
        float timer, hintTimer;

        PlayerVehicles vehicles;

        void Awake()
        {
            carrier = GetComponent<Carrier>();
            vehicles = GetComponent<PlayerVehicles>();
        }

        void Update()
        {
            hintTimer -= Time.deltaTime;
            carrier.Capacity = vehicles && vehicles.Cart ? GameManager.Instance.Config.cartCapacity : UpgradeManager.Instance.PlayerCapacity;

            // 지게차 탑승 중에는 도보 발판 상호작용 없음
            var pad = vehicles && vehicles.Forklift ? null : FindPad();
            if (current && current != pad)
            {
                current.SetProgress(0);
                timer = 0;
            }
            current = pad;
            if (!pad) return;

            var gm = GameManager.Instance;
            var cfg = gm.Config;
            float work = UpgradeManager.Instance.PlayerWorkSpeed * WeatherManager.Instance.WorkMultiplier;
            float interval = cfg.transferInterval / work;

            switch (pad.Type)
            {
                case PadType.TruckUnload:
                {
                    var truck = (InboundTruck)pad.Station;
                    if (gm.InboundDock.IsFull)
                    {
                        Hint($"입고 선반이 가득 차 더 이상 하차할 수 없습니다! ({gm.InboundDock.Packages.Count}/{gm.InboundDock.Capacity})");
                        timer = 0;
                    }
                    else if (truck.IsDocked && truck.Cargo.Count > 0 && !carrier.IsFull && Tick(interval))
                        carrier.Add(truck.TakeOne());
                    break;
                }

                case PadType.DockPut:
                {
                    var dock = (InboundDock)pad.Station;
                    // 원본은 아무 택배나 올렸지만, 검수품이 올라가면 영영 못 꺼내므로 미검수품만
                    int idx = carrier.FindIndex(p => !p.IsInspected);
                    if (idx < 0) timer = 0;
                    else if (dock.IsFull)
                    {
                        Hint($"입고 선반 적재 한도 초과 ({dock.Capacity}개)");
                        timer = 0;
                    }
                    else if (Tick(interval)) dock.Add(carrier.RemoveAt(idx));
                    break;
                }

                case PadType.ScanAction:
                {
                    var dock = (InboundDock)pad.Station;
                    if (!dock.HasUnscanned) ResetCharge(pad);
                    else if (Charge(pad, work))
                        gm.Notify($"{dock.ScanAll()}개 택배의 송장 스캔 완료!", Palette.Blue);
                    break;
                }

                case PadType.DockTake:
                {
                    var dock = (InboundDock)pad.Station;
                    if (carrier.IsFull) timer = 0;
                    else if (dock.Packages.Count > 0 && !dock.Packages.Exists(p => p.Region != Region.None))
                    {
                        Hint("선반 위쪽 발판에서 먼저 [송장 스캔]을 진행하세요!");
                        timer = 0;
                    }
                    else if (Tick(interval))
                    {
                        var pkg = dock.TakeScanned();
                        if (pkg != null) carrier.Add(pkg);
                    }
                    break;
                }

                case PadType.SortPut:
                {
                    var table = (SortingTable)pad.Station;
                    int idx = carrier.FindIndex(p => p.Region == table.Region && !p.IsInspected);
                    if (idx >= 0)
                    {
                        if (Tick(interval)) table.AddUninspected(carrier.RemoveAt(idx));
                    }
                    else
                    {
                        timer = 0;
                        if (carrier.FindIndex(p => p.Region == Region.None) >= 0)
                            Hint("아직 송장 스캔이 되지 않은 택배입니다! 입고 선반 위쪽 발판에서 먼저 스캔하세요.");
                        else if (carrier.Count > 0)
                            Hint($"[{cfg.GetRegion(table.Region).displayName}] 전용 분류대입니다. 일치하는 택배만 투입할 수 있습니다.");
                    }
                    break;
                }

                case PadType.SortInspect:
                {
                    var table = (SortingTable)pad.Station;
                    if (table.Uninspected.Count == 0) ResetCharge(pad);
                    else if (Charge(pad, work))
                    {
                        table.InspectAll();
                        gm.Notify($"[{cfg.GetRegion(table.Region).displayName}] 검수 완료! 출고 대기 구역으로 이동되었습니다.", Palette.Green);
                    }
                    break;
                }

                case PadType.SortTake:
                {
                    var table = (SortingTable)pad.Station;
                    if (table.Inspected.Count > 0 && !carrier.IsFull && Tick(interval))
                        carrier.Add(table.TakeInspected());
                    else if (table.Inspected.Count == 0 || carrier.IsFull) timer = 0;
                    break;
                }

                case PadType.OutboundPut:
                {
                    var rack = (OutboundRack)pad.Station;
                    int idx = carrier.FindIndex(p => p.IsInspected && p.Region == rack.Region);
                    if (idx < 0) timer = 0;
                    else if (rack.IsFull)
                    {
                        Hint($"출고 선반이 가득 찼습니다! (최대 {rack.Capacity}개)");
                        timer = 0;
                    }
                    else if (Tick(interval)) rack.Add(carrier.RemoveAt(idx));
                    break;
                }

                case PadType.OutboundTake:
                {
                    var rack = (OutboundRack)pad.Station;
                    if (rack.Packages.Count > 0 && !carrier.IsFull)
                    {
                        if (Tick(interval)) carrier.Add(rack.TakeLast());
                    }
                    else timer = 0;
                    break;
                }

                case PadType.TruckLoad:
                {
                    var truck = (OutboundTruck)pad.Station;
                    if (!truck.CanLoad)
                    {
                        timer = 0;
                        break;
                    }
                    int idx = carrier.FindIndex(p => p.IsInspected && p.Region == truck.Region);
                    if (idx >= 0)
                    {
                        if (Tick(interval)) truck.Load(carrier.RemoveAt(idx));
                    }
                    else
                    {
                        timer = 0;
                        if (carrier.FindIndex(p => p.IsInspected) >= 0)
                            Hint($"[{cfg.GetRegion(truck.Region).displayName} 전용 트럭]입니다! 타 지역 택배는 실을 수 없습니다.");
                    }
                    break;
                }

                case PadType.Emergency:
                    // 제설 / 차수판 / 발전기: 3초 머무르면 긴급 조치 완료
                    if (Charge(pad, work)) WeatherManager.Instance.Resolve();
                    break;
            }
        }

        Pad FindPad()
        {
            var pos = transform.position;
            foreach (var pad in Pad.All)
            {
                var d = pad.transform.position - pos;
                d.y = 0;
                if (d.sqrMagnitude <= pad.Radius * pad.Radius) return pad;
            }
            return null;
        }

        // 들기/두기: interval 마다 true
        bool Tick(float interval)
        {
            timer += Time.deltaTime;
            if (timer < interval) return false;
            timer = 0;
            return true;
        }

        // 게이지형(스캔/검수): 완료 시 true
        bool Charge(Pad pad, float workSpeed)
        {
            float duration = GameManager.Instance.Config.padActionDuration;
            timer += Time.deltaTime * workSpeed;
            pad.SetProgress(timer / duration);
            if (timer < duration) return false;
            ResetCharge(pad);
            return true;
        }

        void ResetCharge(Pad pad)
        {
            timer = 0;
            pad.SetProgress(0);
        }

        void Hint(string message)
        {
            if (hintTimer > 0) return;
            hintTimer = hintCooldown;
            GameManager.Instance.Notify(message, Palette.Red);
        }
    }
}
