using UnityEngine;

namespace APProto
{
    // 분류대 검수 완료품 -> 같은 라인 출고 선반 자동 이송 (원본 updateConveyorBelts). 2단계 시설부터 활성.
    public class Conveyor : MonoBehaviour
    {
        float timer;

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < GameManager.Instance.Config.conveyorInterval) return;
            timer = 0;

            var line = GetComponentInParent<RegionLine>();
            // 원본은 선반이 가득 차도 계속 쌓았지만 용량 초과 방지
            if (line.Table.Inspected.Count > 0 && !line.Rack.IsFull)
                line.Rack.Add(line.Table.TakeInspected());
        }
    }
}
