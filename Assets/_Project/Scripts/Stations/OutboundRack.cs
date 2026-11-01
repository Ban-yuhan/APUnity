using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace APProto
{
    // 지역별 출고 도크 렉 선반. 용량에 따라 층수 증축 (10=1단, 20=2단, 30=3단, 35~40=4단)
    public class OutboundRack : MonoBehaviour
    {
        [SerializeField] PackageStack stack;
        [SerializeField] TMP_Text countLabel;

        [Header("층 증축")]
        [SerializeField] GameObject[] shelves;
        [Tooltip("높이 1 기준 기둥 묶음. scale.y = 선반 전체 높이")]
        [SerializeField] Transform legs;
        [SerializeField] float tierHeight = 0.95f;

        public readonly List<Package> Packages = new();
        public Region Region { get; private set; }
        public int Capacity => UpgradeManager.Instance.RackCapacity;
        public bool IsFull => Packages.Count >= Capacity;

        void Awake() => Region = GetComponentInParent<RegionLine>(true).Region;

        void Start() => Refresh();

        public void ApplyCapacity()
        {
            int cap = Capacity;
            int tiers = cap >= 35 ? 4 : cap >= 30 ? 3 : cap >= 20 ? 2 : 1;
            for (int i = 0; i < shelves.Length; i++) shelves[i].SetActive(i < tiers);
            float height = 0.35f + tiers * tierHeight;
            if (legs) legs.localScale = new Vector3(1, height, 1);
            if (countLabel) countLabel.transform.localPosition = new Vector3(0, height + 0.7f, 0);
            Refresh();
        }

        public void Add(Package pkg)
        {
            Packages.Add(pkg);
            Refresh();
        }

        // 지게차: 택배 8개를 묶어 파렛트 1개로. 모자라면 null.
        public Package TakePallet(int count)
        {
            if (Packages.Count < count) return null;
            Packages.RemoveRange(Packages.Count - count, count);
            Refresh();
            return new Package { Region = Region, IsInspected = true, Count = count };
        }

        public Package TakeLast()
        {
            var pkg = Packages[^1];
            Packages.RemoveAt(Packages.Count - 1);
            Refresh();
            return pkg;
        }

        void Refresh()
        {
            stack.Show(Packages);
            if (countLabel)
            {
                var region = GameManager.Instance.Config.GetRegion(GetComponentInParent<RegionLine>(true).Region);
                countLabel.text = $"{region.displayName} 출고 선반: {Packages.Count} / {Capacity}";
            }
        }
    }
}
