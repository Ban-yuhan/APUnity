using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace APProto
{
    // 입고 & 송장 스캔 통합 선반. 용량에 따라 1호기 2단 -> 2호기 신설 -> 2호기 2단 증축.
    public class InboundDock : MonoBehaviour
    {
        const int PerRack = 20;  // 렉 1대 = 2단 x 10개

        [SerializeField] PackageStack stack;
        [SerializeField] TMP_Text countLabel;

        [Header("용량 확장 (용량 > 10 / > 20 / > 30 일 때 표시)")]
        [SerializeField] GameObject shelf2;
        [SerializeField] GameObject rack2;
        [SerializeField] GameObject rack2Shelf2;
        [SerializeField] PackageStack rack2Stack;

        public readonly List<Package> Packages = new();
        public int Capacity => UpgradeManager.Instance.DockCapacity;
        public bool IsFull => Packages.Count >= Capacity;
        public bool HasUnscanned => Packages.Exists(p => p.Region == Region.None);

        void Start() => Refresh();

        public void ApplyCapacity()
        {
            int cap = Capacity;
            if (shelf2) shelf2.SetActive(cap > 10);
            if (rack2) rack2.SetActive(cap > 20);
            if (rack2Shelf2) rack2Shelf2.SetActive(cap > 30);
            Refresh();
        }

        public void Add(Package pkg)
        {
            Packages.Add(pkg);
            Refresh();
        }

        // 스캔 완료(미검수) 택배 1개 꺼내기. 없으면 null.
        public Package TakeScanned()
        {
            int idx = Packages.FindIndex(p => p.Region != Region.None && !p.IsInspected);
            if (idx < 0) return null;
            var pkg = Packages[idx];
            Packages.RemoveAt(idx);
            Refresh();
            return pkg;
        }

        // 미스캔 택배 전부에 해금된 지역 중 랜덤 목적지 부여
        public int ScanAll()
        {
            var pool = GameManager.Instance.UnlockedRegions;
            int count = 0;
            foreach (var pkg in Packages)
            {
                if (pkg.Region != Region.None) continue;
                pkg.Region = pool[Random.Range(0, pool.Count)];
                count++;
            }
            Refresh();
            return count;
        }

        void Refresh()
        {
            if (rack2Stack)
            {
                int first = Mathf.Min(Packages.Count, PerRack);
                stack.Show(Packages.GetRange(0, first));
                rack2Stack.Show(Packages.GetRange(first, Packages.Count - first));
            }
            else stack.Show(Packages);
            if (countLabel) countLabel.text = $"입고&스캔: {Packages.Count} / {Capacity}";
        }
    }
}
