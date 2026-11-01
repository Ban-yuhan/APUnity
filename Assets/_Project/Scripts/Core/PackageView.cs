using UnityEngine;

namespace APProto
{
    // 택배 프리팹의 외형. 색 = 지역(미스캔은 골판지색), 검수 완료 시 십자 테이프.
    public class PackageView : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer body;
        [SerializeField] GameObject inspectedTape;
        [SerializeField] GameObject waybill;

        MaterialPropertyBlock mpb;

        public void Apply(Package package)
        {
            var cfg = GameManager.Instance.Config;
            mpb ??= new MaterialPropertyBlock();
            mpb.SetColor(BaseColor, package.Region == Region.None ? cfg.unscannedBoxColor : cfg.GetRegion(package.Region).color);
            body.SetPropertyBlock(mpb);
            inspectedTape.SetActive(package.IsInspected);
            waybill.SetActive(!package.IsInspected);
        }
    }
}
