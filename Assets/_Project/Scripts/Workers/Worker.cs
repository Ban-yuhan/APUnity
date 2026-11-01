using TMPro;
using UnityEngine;

namespace APProto
{
    // 알바 1명. 직종별 FSM 은 원본 workers.js 의 aiUnloader/aiScanner/aiInspector/aiCarrier/aiLoader.
    // 오분류(검정 상자)·베테랑 자동 복구는 목업 범위에서 제외.
    [RequireComponent(typeof(Carrier))]
    public class Worker : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int SpeedParam = Animator.StringToHash("Speed");

        [SerializeField] Renderer uniform;
        [SerializeField] Renderer aura;
        [SerializeField] TMP_Text nameLabel;
        [Tooltip("Model 교체 시 걷기 애니메이션용 (float 파라미터 Speed)")]
        [SerializeField] Animator animator;

        public WorkerRole Role { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsVeteran { get; private set; }
        public int Tier { get; private set; } = -1;
        public float WagePerSecond { get; private set; }
        public float VeteranRemaining { get; private set; }

        // 검수 알바 배치 (WorkerManager 가 매 프레임 재분배)
        public Region AssignedRegion { get; set; }
        public bool IsAssistant { get; set; }
        public bool ReachedTarget { get; private set; }

        Carrier carrier;
        GameConfig cfg;
        float workTime, speed, workSpeed, timer;
        bool delivering;            // 하차/운반/상차: false = 줍는 중, true = 내려놓는 중
        Region sourceRegion;        // 운반: 검수품을 집던 분류대 / 상차: 담당 트럭
        bool moving;
        Forklift driving;           // 상차 알바: 지게차 운전 중이면 지게차가 이동/작업

        public void ExitForklift() => driving = null;

        public void Init(WorkerRole role, string displayName, bool veteran)
        {
            carrier = GetComponent<Carrier>();
            cfg = GameManager.Instance.Config;
            Role = role;
            DisplayName = displayName;
            IsVeteran = veteran;
            var roleData = cfg.GetRole(role);
            WagePerSecond = roleData.wagePerSecond * (veteran ? cfg.veteranWageMultiplier : 1f);
            VeteranRemaining = cfg.veteranDuration;
            SetColor(uniform, roleData.uniformColor);
            UpdateTier();
        }

        public void Tick(float dt)
        {
            workTime += dt;
            if (IsVeteran) VeteranRemaining -= dt;
            UpdateTier();

            moving = false;
            if (!driving) switch (Role)
            {
                case WorkerRole.Unloader: Unloader(); break;
                case WorkerRole.Scanner: Scanner(); break;
                case WorkerRole.Inspector: Inspector(); break;
                case WorkerRole.Carrier: CarrierAI(); break;
                case WorkerRole.Loader: Loader(); break;
            }
            if (animator) animator.SetFloat(SpeedParam, moving ? speed : 0);

            string badge = IsVeteran ? $" ({Mathf.CeilToInt(VeteranRemaining)}초)" : "";
            nameLabel.text = $"{DisplayName}{badge}";
        }

        void UpdateTier()
        {
            var tiers = cfg.proficiencyTiers;
            int tier = tiers.Length - 1;
            if (!IsVeteran)
                while (tier > 0 && workTime < tiers[tier].minWorkSeconds) tier--;
            if (tier == Tier) return;

            bool promoted = Tier >= 0;
            Tier = tier;
            var t = tiers[tier];
            speed = cfg.workerBaseSpeed * (IsVeteran ? cfg.veteranSpeedMultiplier : t.speedMultiplier);
            workSpeed = IsVeteran ? cfg.veteranWorkMultiplier : t.workMultiplier;
            carrier.Capacity = t.capacity;
            SetColor(aura, t.color);
            if (promoted)
                GameManager.Instance.Notify($"[{DisplayName}] 알바가 [{t.displayName}] 등급으로 승급! (최대 소지량: {t.capacity}개)", Palette.Gold);
        }

        // ------------------------------------------------------------ roles

        void Unloader()
        {
            var gm = GameManager.Instance;
            var truck = gm.InboundTruck;
            var dock = gm.InboundDock;
            if (!delivering)
            {
                if (!MoveTo(Pad.Find(PadType.TruckUnload, truck), 0.4f)) return;
                if (truck.IsDocked && truck.Cargo.Count > 0 && !carrier.IsFull && !dock.IsFull)
                {
                    if (TickTransfer()) carrier.Add(truck.TakeOne());
                    if (carrier.IsFull) delivering = true;
                }
                else
                {
                    timer = 0;
                    if (carrier.Count > 0) delivering = true;
                }
            }
            else
            {
                if (!MoveTo(Pad.Find(PadType.DockPut, dock), 0.4f)) return;
                if (carrier.Count > 0 && !dock.IsFull)
                {
                    if (TickTransfer()) dock.Add(carrier.RemoveAt(carrier.Count - 1));
                }
                else timer = 0;
                if (carrier.Count == 0) delivering = false;
            }
        }

        void Scanner()
        {
            var dock = GameManager.Instance.InboundDock;
            var pad = Pad.Find(PadType.ScanAction, dock);
            if (!MoveTo(pad, 0.3f)) return;
            if (!dock.HasUnscanned) ResetCharge(pad);
            else if (Charge(pad, workSpeed)) dock.ScanAll();
        }

        void Inspector()
        {
            var line = GameManager.Instance.GetLine(AssignedRegion);
            if (!line) return;
            var table = line.Table;
            var pad = Pad.Find(PadType.SortInspect, table);
            if (!pad) return;

            // 메인/보조가 나란히 서도록 오프셋
            var target = pad.transform.position + new Vector3(0, 0, IsAssistant ? -0.4f : 0.4f);
            ReachedTarget = MoveTo(target, 0.25f);
            if (!ReachedTarget)
            {
                timer = 0;
                return;
            }
            if (table.Uninspected.Count == 0)
            {
                ResetCharge(pad);
                return;
            }

            var wm = WorkerManager.Instance;
            // 메인이 타이머 전담, 메인 부재 시 보조가 단독 진행. 보조가 함께 있으면 +20%
            if (IsAssistant && wm.HasInspectorAt(AssignedRegion, false)) return;
            float coop = !IsAssistant && wm.HasInspectorAt(AssignedRegion, true) ? 1.2f : 1f;
            if (Charge(pad, workSpeed * coop)) table.InspectAll();
        }

        void CarrierAI()
        {
            var gm = GameManager.Instance;
            var dock = gm.InboundDock;

            if (!delivering)
            {
                if (carrier.IsFull)
                {
                    delivering = true;
                    timer = 0;
                    return;
                }

                // 픽업 대상: 손에 든 것과 같은 출처 우선, 빈손이면 입고 선반 스캔품 -> 분류대 검수품 순
                bool dockHasScanned = dock.Packages.Exists(p => p.Region != Region.None && !p.IsInspected);
                SortingTable table = null;
                bool fromDock = false;
                if (carrier.Count > 0)
                {
                    if (!carrier.Items[0].IsInspected) fromDock = dockHasScanned;
                    else
                    {
                        var t = gm.GetLine(sourceRegion).Table;
                        if (t.Inspected.Count > 0) table = t;
                    }
                    if (!fromDock && !table)
                    {
                        delivering = true;
                        timer = 0;
                        return;
                    }
                }
                else if (dockHasScanned) fromDock = true;
                else
                {
                    foreach (var r in gm.UnlockedRegions)
                    {
                        var t = gm.GetLine(r).Table;
                        if (t.Inspected.Count == 0) continue;
                        table = t;
                        sourceRegion = r;
                        break;
                    }
                }

                if (fromDock)
                {
                    if (!MoveTo(Pad.Find(PadType.DockTake, dock), 0.4f)) return;
                    if (TickTransfer())
                    {
                        var pkg = dock.TakeScanned();
                        if (pkg != null) carrier.Add(pkg);
                    }
                }
                else if (table)
                {
                    if (!MoveTo(Pad.Find(PadType.SortTake, table), 0.4f)) return;
                    if (table.Inspected.Count > 0 && TickTransfer()) carrier.Add(table.TakeInspected());
                }
                else timer = 0;  // 할 일 없음: 대기
                return;
            }

            if (carrier.Count == 0)
            {
                delivering = false;
                timer = 0;
                return;
            }

            var first = carrier.Items[0];
            var line = gm.GetLine(first.Region);
            int idx = carrier.FindIndex(p => p.Region == first.Region);
            if (!first.IsInspected)
            {
                // 스캔품 -> 목적지 분류대
                if (!MoveTo(Pad.Find(PadType.SortPut, line.Table), 0.4f)) return;
                if (TickTransfer()) line.Table.AddUninspected(carrier.RemoveAt(idx));
            }
            else if (line.Truck.CanLoad)
            {
                // 검수품 -> 트럭 직행
                if (!MoveTo(Pad.Find(PadType.TruckLoad, line.Truck), 0.6f)) return;
                if (TickTransfer()) line.Truck.Load(carrier.RemoveAt(idx));
            }
            else
            {
                // 트럭 부재/만차 -> 출고 선반 보관
                if (!MoveTo(Pad.Find(PadType.OutboundPut, line.Rack), 0.4f)) return;
                if (!line.Rack.IsFull && TickTransfer()) line.Rack.Add(carrier.RemoveAt(idx));
                else if (line.Rack.IsFull) timer = 0;
            }
            if (carrier.Count == 0) delivering = false;
        }

        void Loader()
        {
            var gm = GameManager.Instance;

            // 메가허브: 파렛트 분량이 쌓였고 빈 지게차가 있으면 손 상차 대신 지게차 우선
            var bulk = UpgradeManager.Instance.HasMegahub ? Forklift.FindBulkLine() : null;
            var fork = bulk ? Forklift.FindFree() : null;
            if (fork)
            {
                if (carrier.Count > 0)
                {
                    // 들고 있던 건 먼저 선반에 내려놓기
                    var held = gm.GetLine(carrier.Items[0].Region);
                    if (MoveTo(Pad.Find(PadType.OutboundPut, held.Rack), 0.4f) && !held.Rack.IsFull && TickTransfer())
                        held.Rack.Add(carrier.RemoveAt(carrier.Count - 1));
                    return;
                }
                if (MoveTo(fork.transform.position, 1.2f))
                {
                    fork.BoardWorker(this, bulk.Region);
                    driving = fork;
                    delivering = false;
                }
                return;
            }

            // 들고 있는데 트럭이 떠났으면 출고 선반으로 반환
            if (carrier.Count > 0)
            {
                var held = gm.GetLine(carrier.Items[0].Region);
                if (!held.Truck.IsDocked)
                {
                    if (!MoveTo(Pad.Find(PadType.OutboundPut, held.Rack), 0.4f)) return;
                    if (!held.Rack.IsFull && TickTransfer()) held.Rack.Add(carrier.RemoveAt(carrier.Count - 1));
                    if (carrier.Count == 0) delivering = false;
                    return;
                }
            }

            if (!delivering)
            {
                if (carrier.IsFull)
                {
                    delivering = true;
                    timer = 0;
                    return;
                }

                RegionLine chosen = null;
                var assigned = sourceRegion != Region.None ? gm.GetLine(sourceRegion) : null;
                if (assigned && assigned.gameObject.activeSelf && assigned.Rack.Packages.Count > 0 && assigned.Truck.CanLoad)
                    chosen = assigned;
                else
                {
                    foreach (var r in gm.UnlockedRegions)
                    {
                        var l = gm.GetLine(r);
                        if (!l.Truck.CanLoad || l.Rack.Packages.Count == 0) continue;
                        chosen = l;
                        break;
                    }
                }

                if (!chosen || (carrier.Count > 0 && chosen.Region != carrier.Items[0].Region))
                {
                    timer = 0;
                    if (carrier.Count > 0) delivering = true;
                    return;
                }
                if (!MoveTo(Pad.Find(PadType.OutboundTake, chosen.Rack), 0.4f)) return;
                if (TickTransfer())
                {
                    carrier.Add(chosen.Rack.TakeLast());
                    sourceRegion = chosen.Region;
                }
                return;
            }

            if (carrier.Count == 0)
            {
                delivering = false;
                return;
            }
            var truck = gm.GetLine(carrier.Items[0].Region).Truck;
            if (!MoveTo(Pad.Find(PadType.TruckLoad, truck), 0.6f)) return;
            if (truck.CanLoad)
            {
                if (TickTransfer()) truck.Load(carrier.RemoveAt(carrier.Count - 1));
            }
            else timer = 0;  // 만차: 자동 출발 대기 -> 출발하면 위에서 선반으로 반환
            if (carrier.Count == 0) delivering = false;
        }

        // ------------------------------------------------------------ helpers

        bool MoveTo(Pad pad, float arrive) => pad && MoveTo(pad.transform.position, arrive);

        bool MoveTo(Vector3 target, float arrive)
        {
            var pos = transform.position;
            var diff = target - pos;
            diff.y = 0;
            float dist = diff.magnitude;
            if (dist <= arrive) return true;

            float step = Mathf.Min(dist, speed * WeatherManager.Instance.SpeedMultiplier * Time.deltaTime);
            transform.position = pos + diff / dist * step;
            transform.rotation = Quaternion.LookRotation(diff);
            moving = true;
            timer = 0;
            return false;
        }

        bool TickTransfer()
        {
            timer += Time.deltaTime;
            if (timer < cfg.transferInterval / (workSpeed * WeatherManager.Instance.WorkMultiplier)) return false;
            timer = 0;
            return true;
        }

        bool Charge(Pad pad, float rate)
        {
            timer += Time.deltaTime * rate * WeatherManager.Instance.WorkMultiplier;
            pad.SetProgress(timer / cfg.padActionDuration);
            if (timer < cfg.padActionDuration) return false;
            ResetCharge(pad);
            return true;
        }

        void ResetCharge(Pad pad)
        {
            timer = 0;
            pad.SetProgress(0);
        }

        static void SetColor(Renderer r, Color c)
        {
            if (!r) return;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColor, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
