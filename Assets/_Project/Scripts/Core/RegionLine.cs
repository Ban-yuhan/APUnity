using UnityEngine;

namespace APProto
{
    // 지역 1개 라인 (분류대 + 출고 선반 + 출고 트럭 + 발판). 비활성 = 미해금.
    public class RegionLine : MonoBehaviour
    {
        [SerializeField] Region region;
        [SerializeField] OutboundTruck truck;
        [Tooltip("중형 물류센터(2단계)부터 활성")]
        [SerializeField] GameObject conveyor;
        [Tooltip("메가허브(3단계)에서 동쪽으로 이동하는 트럭 상차 발판")]
        [SerializeField] Transform truckLoadPad;

        float loadPadBaseX = float.NaN;

        public Region Region => region;
        public OutboundTruck Truck => truck;

        SortingTable table;
        OutboundRack rack;
        public SortingTable Table => table ? table : table = GetComponentInChildren<SortingTable>(true);
        public OutboundRack Rack => rack ? rack : rack = GetComponentInChildren<OutboundRack>(true);

        public void Unlock()
        {
            gameObject.SetActive(true);
            truck.BeginArrival();
        }

        public void ApplyFacility(bool hasConveyor, bool megahub)
        {
            var cfg = GameManager.Instance.Config;
            if (conveyor) conveyor.SetActive(hasConveyor);
            truck.DockOffset = megahub ? cfg.megahubTruckShift : 0;
            if (float.IsNaN(loadPadBaseX)) loadPadBaseX = truckLoadPad.position.x;
            var p = truckLoadPad.position;
            p.x = loadPadBaseX + (megahub ? cfg.megahubLoadPadShift : 0);
            truckLoadPad.position = p;
        }
    }
}
