using UnityEngine;

namespace APProto
{
    // 월드 라벨이 항상 카메라를 향하도록
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;
        }
    }
}
