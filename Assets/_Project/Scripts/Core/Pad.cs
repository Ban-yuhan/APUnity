using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APProto
{
    public enum PadType { TruckUnload, DockPut, ScanAction, DockTake, SortPut, SortInspect, SortTake, OutboundPut, OutboundTake, TruckLoad, Emergency }

    // 원형 상호작용 발판. 반경 안에 서 있으면 PlayerInteractor 가 동작을 처리.
    public class Pad : MonoBehaviour
    {
        public static readonly List<Pad> All = new();

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] PadType type;
        [Tooltip("이 발판이 다루는 시설 (InboundTruck / InboundDock / SortingTable / OutboundRack / OutboundTruck)")]
        [SerializeField] MonoBehaviour station;
        [SerializeField] float radius = 1.2f;
        [SerializeField] Color color = Color.cyan;
        [SerializeField] string label;

        [Header("Visual")]
        [SerializeField] Renderer disc;
        [SerializeField] Image progressImage;
        [SerializeField] TMP_Text labelText;

        public PadType Type => type;
        public MonoBehaviour Station => station;
        public float Radius => radius;

        // 활성 발판 중 type + station 이 일치하는 것 (알바 AI 의 이동 목표)
        public static Pad Find(PadType type, MonoBehaviour station) =>
            All.Find(p => p.type == type && p.station == station);

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            SetProgress(0);
        }

        public void SetProgress(float progress)
        {
            if (!progressImage) return;
            progressImage.enabled = progress > 0;
            progressImage.fillAmount = Mathf.Clamp01(progress);
        }

        void OnValidate() => ApplyVisual();

        public void ApplyVisual()
        {
            if (disc)
            {
                disc.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(BaseColor, new Color(color.r, color.g, color.b, 0.45f));
                disc.SetPropertyBlock(mpb);
            }
            if (progressImage)
            {
                progressImage.transform.localScale = Vector3.one * radius * 2.44f;  // sizeDelta 는 OnValidate 에서 경고 발생
                progressImage.enabled = false;
            }
            if (labelText) labelText.text = label;
        }
    }
}
