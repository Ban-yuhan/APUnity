using TMPro;
using UnityEngine;

namespace APProto
{
    // 지게차 (메가허브부터). 출고 선반에 택배 8개 이상이면 파렛트 1개로 떠서 트럭 1칸에 상차.
    // 플레이어가 운전(WASD/조이스틱) 하거나, 상차 알바가 탑승해 자동 운행 (원본 updateAutonomousForklifts).
    public class Forklift : MonoBehaviour
    {
        enum AiState { Idle, ToRack, ToTruck, Home }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Tooltip("지게발 위 파렛트 화물 (적재 시 표시)")]
        [SerializeField] GameObject palletLoad;
        [SerializeField] Renderer[] palletBoxes;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Vector2 boundsX = new(-20f, 20f);
        [SerializeField] Vector2 boundsZ = new(-12f, 12f);
        [Tooltip("메가허브 확장 후 동쪽 한계")]
        [SerializeField] float megahubMaxX = 31.5f;

        public Package Pallet { get; private set; }
        public bool HasDriver => playerDriving || workerDriver;
        public bool PlayerDriving => playerDriving;

        bool playerDriving;
        Worker workerDriver;
        AiState ai;
        Region target;
        Vector3 homePos;
        Quaternion homeRot;
        float interactTimer, hintTimer;
        GameConfig cfg;

        public static readonly System.Collections.Generic.List<Forklift> Active = new();

        public static Forklift FindFree() => Active.Find(f => !f.HasDriver);

        void Awake()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
            cfg = GameManager.Instance.Config;  // 활성화 직후 같은 프레임에 탑승해도 쓰도록 Awake 에서
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Start()
        {
            if (Pallet == null) SetPallet(null);
        }

        // ------------------------------------------------------------ 탑승

        public void BoardPlayer() => playerDriving = true;
        public void UnboardPlayer() => playerDriving = false;

        public void BoardWorker(Worker w, Region region)
        {
            workerDriver = w;
            target = region;
            ai = AiState.ToRack;
        }

        // ------------------------------------------------------------ 플레이어 운전

        public void Drive(Vector3 dir)
        {
            if (dir != Vector3.zero)
            {
                transform.position += dir * (cfg.forkliftSpeed * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(dir);
            }
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, boundsX.x, UpgradeManager.Instance.HasMegahub ? megahubMaxX : boundsX.y);
            p.z = Mathf.Clamp(p.z, boundsZ.x, boundsZ.y);
            transform.position = p;

            hintTimer -= Time.deltaTime;
            interactTimer += Time.deltaTime;
            if (interactTimer < 0.25f) return;
            interactTimer = 0;
            var gm = GameManager.Instance;
            foreach (var r in gm.UnlockedRegions)
            {
                var line = gm.GetLine(r);
                if (Pallet == null && Near(Pad.Find(PadType.OutboundTake, line.Rack)))
                {
                    if (TryLift(line))
                        gm.Notify($"[{Name(r)}] 지게차 {cfg.palletSize}개 파렛트 적재 완료! 출고 트럭으로 운반하세요!", Palette.Gold);
                    else if (hintTimer <= 0)
                    {
                        hintTimer = 2.5f;
                        gm.Notify($"[{Name(r)}] 지게차는 택배가 {cfg.palletSize}개 이상 모여야 파렛트를 뜰 수 있습니다! ({line.Rack.Packages.Count}/{cfg.palletSize}개)", Palette.Blue);
                    }
                    return;
                }
                if (Pallet != null && r == Pallet.Region && Near(Pad.Find(PadType.TruckLoad, line.Truck)) && TryUnload(line))
                {
                    gm.Notify($"[{Name(r)}] 지게차 파렛트(택배 {cfg.palletSize}개 분량) 트럭 직행 상차 완료!", Palette.Green);
                    return;
                }
            }
        }

        bool Near(Pad pad) => pad && Vector3.Distance(Flat(transform.position), Flat(pad.transform.position)) <= cfg.forkliftWorkRange;

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        string Name(Region r) => cfg.GetRegion(r).displayName;

        bool TryLift(RegionLine line)
        {
            var pallet = line.Rack.TakePallet(cfg.palletSize);
            if (pallet == null) return false;
            SetPallet(pallet);
            return true;
        }

        bool TryUnload(RegionLine line)
        {
            if (!line.Truck.CanLoad) return false;
            line.Truck.Load(Pallet);
            SetPallet(null);
            return true;
        }

        // ------------------------------------------------------------ 알바 자동 운행

        void Update()
        {
            if (!workerDriver) return;
            var gm = GameManager.Instance;
            workerDriver.transform.SetPositionAndRotation(transform.position + Vector3.up * 0.55f, transform.rotation);

            var line = gm.GetLine(target);
            switch (ai)
            {
                case AiState.ToRack:
                    if (!MoveTo(Pad.Find(PadType.OutboundTake, line.Rack).transform.position)) return;
                    ai = TryLift(line) ? AiState.ToTruck : AiState.Home;
                    break;

                case AiState.ToTruck:
                    if (!MoveTo(Pad.Find(PadType.TruckLoad, line.Truck).transform.position)) return;
                    if (!TryUnload(line)) return;  // 트럭 만차/부재: 돌아올 때까지 대기
                    var next = FindBulkLine();
                    if (next)
                    {
                        target = next.Region;
                        ai = AiState.ToRack;
                    }
                    else ai = AiState.Home;
                    break;

                case AiState.Home:
                    if (!MoveTo(homePos)) return;
                    transform.rotation = homeRot;
                    var w = workerDriver;
                    workerDriver = null;
                    ai = AiState.Idle;
                    w.transform.position = transform.position + transform.right * -1.4f;
                    w.ExitForklift();
                    break;
            }
        }

        bool MoveTo(Vector3 target)
        {
            var diff = Flat(target) - Flat(transform.position);
            float dist = diff.magnitude;
            if (dist <= 0.4f) return true;
            transform.position += diff / dist * Mathf.Min(dist, cfg.forkliftSpeed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(diff);
            return false;
        }

        // 출고 선반에 파렛트 분량이 있고 트럭에 자리가 있는 라인
        public static RegionLine FindBulkLine()
        {
            var gm = GameManager.Instance;
            foreach (var r in gm.UnlockedRegions)
            {
                var l = gm.GetLine(r);
                if (l.Rack.Packages.Count >= gm.Config.palletSize && l.Truck.CanLoad) return l;
            }
            return null;
        }

        // ------------------------------------------------------------ 표시

        void SetPallet(Package pallet)
        {
            Pallet = pallet;
            palletLoad.SetActive(pallet != null);
            if (pallet == null) return;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColor, cfg.GetRegion(pallet.Region).color);
            foreach (var r in palletBoxes) r.SetPropertyBlock(mpb);
        }

        void LateUpdate()
        {
            if (!statusLabel || cfg == null) return;
            statusLabel.text = Pallet != null ? $"[{Name(Pallet.Region)} 파렛트 {Pallet.Count}개] 트럭으로!"
                : playerDriving ? "지게차 운전 중 (하차: E)"
                : workerDriver ? $"{workerDriver.DisplayName} 운전 중"
                : "지게차 (탑승: E)";
        }
    }
}
