using UnityEngine;

namespace APProto
{
    // 렌더러 점멸 (긴급 작업 발판 강조)
    public class Blinker : MonoBehaviour
    {
        [SerializeField] Renderer target;
        [SerializeField] float period = 0.5f;

        void LateUpdate() => target.enabled = Mathf.FloorToInt(Time.unscaledTime / period) % 2 == 0;
    }
}
