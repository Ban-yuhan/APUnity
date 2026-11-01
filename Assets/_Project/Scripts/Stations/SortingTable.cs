using System.Collections.Generic;
using UnityEngine;

namespace APProto
{
    // 지역별 분류대: 왼쪽(검수 전) -> 검수 -> 오른쪽(검수 완료)
    public class SortingTable : MonoBehaviour
    {
        [SerializeField] PackageStack uninspectedStack;
        [SerializeField] PackageStack inspectedStack;

        public readonly List<Package> Uninspected = new();
        public readonly List<Package> Inspected = new();
        public Region Region { get; private set; }

        void Awake() => Region = GetComponentInParent<RegionLine>(true).Region;

        void Start() => Refresh();

        public void AddUninspected(Package pkg)
        {
            Uninspected.Add(pkg);
            Refresh();
        }

        public void InspectAll()
        {
            foreach (var pkg in Uninspected) pkg.IsInspected = true;
            Inspected.AddRange(Uninspected);
            Uninspected.Clear();
            Refresh();
        }

        public Package TakeInspected()
        {
            var pkg = Inspected[^1];
            Inspected.RemoveAt(Inspected.Count - 1);
            Refresh();
            return pkg;
        }

        void Refresh()
        {
            uninspectedStack.Show(Uninspected);
            inspectedStack.Show(Inspected);
        }
    }
}
