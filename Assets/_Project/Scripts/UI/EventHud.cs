using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APProto
{
    // 날짜/시간, 날씨, 긴급 작업 안내, 피버타임 버튼, 번개 섬광, 날씨 테스트 버튼
    public class EventHud : MonoBehaviour
    {
        [SerializeField] TMP_Text clockText;
        [SerializeField] TMP_Text weatherText;
        [SerializeField] GameObject questPanel;
        [SerializeField] TMP_Text questText;
        [SerializeField] Button feverButton;
        [SerializeField] TMP_Text feverLabel;
        [SerializeField] Image lightningFlash;
        [SerializeField] float flashDuration = 0.12f;
        [Tooltip("업그레이드 창 안의 날씨 테스트 버튼 (맑음/폭설/폭우/뇌우 순서 = Weather enum)")]
        [SerializeField] Button[] weatherTestButtons;

        float flashTimer;

        void Start()
        {
            var gm = GameManager.Instance;
            var wm = WeatherManager.Instance;
            feverButton.onClick.AddListener(() => gm.TryStartFever());
            wm.Lightning += () => flashTimer = flashDuration;
            for (int i = 0; i < weatherTestButtons.Length; i++)
            {
                var w = (Weather)i;
                weatherTestButtons[i].onClick.AddListener(() => wm.Trigger(w));
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var cfg = gm.Config;
            var clock = DayNightCycle.Instance;
            var wm = WeatherManager.Instance;

            clockText.text = clock.IsNight
                ? $"DAY {clock.Day}  ·  <color=#93c5fd>밤</color> {Mathf.CeilToInt(cfg.dayLength + cfg.nightLength - clock.Elapsed)}초  <size=75%>(알바비·수익 x{cfg.nightProfitMultiplier})</size>"
                : $"DAY {clock.Day}  ·  <color=#fde68a>낮</color> {Mathf.CeilToInt(cfg.dayLength - clock.Elapsed)}초";

            weatherText.text = wm.Current == Weather.Clear
                ? "날씨: 맑음"
                : $"날씨: <color=#f87171>{WeatherManager.Name(wm.Current)}</color> {Mathf.CeilToInt(wm.Remaining)}초" + (wm.EmergencyResolved ? "  (조치 완료)" : "");
            questPanel.SetActive(wm.EmergencyActive);
            if (wm.EmergencyActive) questText.text = wm.QuestText;

            feverButton.interactable = gm.CanStartFever;
            feverLabel.text = gm.IsFever ? $"피버타임! {Mathf.CeilToInt(gm.FeverRemaining)}초"
                : gm.FeverCooldown > 0 ? $"앙팡데이 대기 {Mathf.CeilToInt(gm.FeverCooldown)}초"
                : "앙팡데이 발동!";

            flashTimer -= Time.unscaledDeltaTime;
            var c = lightningFlash.color;
            c.a = flashTimer > 0 ? 0.8f : 0f;
            lightningFlash.color = c;
        }
    }
}
