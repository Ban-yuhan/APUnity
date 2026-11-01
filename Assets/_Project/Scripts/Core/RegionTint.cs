using UnityEngine;

namespace APProto
{
    // 부모 RegionLine 의 지역 색을 렌더러에 입힘 (에디터에서도 표시)
    [ExecuteAlways]
    public class RegionTint : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] GameConfig config;
        [SerializeField] Renderer[] renderers;
        [SerializeField, Range(0, 1)] float alpha = 1f;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            var line = GetComponentInParent<RegionLine>(true);
            if (!config || !line) return;
            var c = config.GetRegion(line.Region).color;
            c.a = alpha;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColor, c);
            foreach (var r in renderers)
                if (r) r.SetPropertyBlock(mpb);
        }
    }
}
