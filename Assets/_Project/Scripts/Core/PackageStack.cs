using System.Collections.Generic;
using UnityEngine;

namespace APProto
{
    // 택배 목록을 격자(columns x rows) 로 깔고 위로 층층이 쌓아 보여줌. 택배 수가 가변이라 프리팹 Instantiate 사용.
    public class PackageStack : MonoBehaviour
    {
        [SerializeField] PackageView packagePrefab;
        [SerializeField] int columns = 2;
        [SerializeField] int rows = 2;
        [SerializeField] Vector2 spacing = new(0.8f, 0.8f);
        [SerializeField] float layerHeight = 0.52f;

        readonly List<PackageView> views = new();

        public void Show(IReadOnlyList<Package> packages)
        {
            while (views.Count < packages.Count)
                views.Add(Instantiate(packagePrefab, transform));

            for (int i = 0; i < views.Count; i++)
            {
                bool active = i < packages.Count;
                views[i].gameObject.SetActive(active);
                if (!active) continue;
                views[i].Apply(packages[i]);
                views[i].transform.localPosition = Slot(i);
            }
        }

        Vector3 Slot(int index)
        {
            int perLayer = columns * rows;
            int layer = index / perLayer;
            int local = index % perLayer;
            float x = (local % columns - (columns - 1) * 0.5f) * spacing.x;
            float z = (local / columns - (rows - 1) * 0.5f) * spacing.y;
            return new Vector3(x, layer * layerHeight, z);
        }
    }
}
