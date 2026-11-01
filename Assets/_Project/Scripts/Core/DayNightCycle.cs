using UnityEngine;

namespace APProto
{
    // 하루 = 낮 90초 + 밤 30초. 실외 태양광만 주야간 변화, 실내 조명은 24시간 (뇌우 정전 시만 암전).
    // 밤에는 알바비/배송 이익 1.5배 (원본 updateDayNightCycle)
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [SerializeField] Light sun;
        [SerializeField] Light[] indoorLights;
        [SerializeField] Light emergencyLight;
        [SerializeField] float indoorIntensity = 1.1f;
        [SerializeField] float emergencyIntensity = 1.35f;
        [SerializeField] Color ambient = new(0.55f, 0.55f, 0.55f);
        [SerializeField] Color blackoutAmbient = new(0.12f, 0.12f, 0.12f);

        public int Day { get; private set; } = 1;
        public float Elapsed { get; private set; }
        public bool IsNight => Elapsed >= cfg.dayLength;
        public float NightProfitMultiplier => IsNight ? cfg.nightProfitMultiplier : 1f;
        public float NightWageMultiplier => IsNight ? cfg.nightWageMultiplier : 1f;

        GameConfig cfg;
        Camera cam;

        void Awake()
        {
            Instance = this;
            cfg = GameManager.Instance.Config;
            cam = Camera.main;
        }

        // 테스트/디버그: 하루 중 시점 지정
        public void SetElapsed(float seconds) => Elapsed = seconds;

        void Update()
        {
            Elapsed += Time.deltaTime;
            if (Elapsed >= cfg.dayLength + cfg.nightLength)
            {
                Elapsed = 0;
                Day++;
                GameManager.Instance.Notify($"새로운 하루가 밝았습니다! (DAY {Day})", Palette.Gold);
                if (WeatherManager.Instance) WeatherManager.Instance.OnNewDay();
            }

            bool night = IsNight;
            sun.color = night ? cfg.nightSunColor : cfg.daySunColor;
            sun.intensity = night ? cfg.nightSunIntensity : cfg.daySunIntensity;
            var bg = night ? cfg.nightBackground : cfg.dayBackground;
            cam.backgroundColor = bg;
            RenderSettings.fogColor = bg;

            bool blackout = WeatherManager.Instance && WeatherManager.Instance.IsBlackout;
            foreach (var l in indoorLights)
                l.intensity = blackout ? (Random.value < 0.1f ? 0.1f : 0.02f) : indoorIntensity;  // 정전: 희미한 깜빡임
            emergencyLight.intensity = blackout ? emergencyIntensity : 0;
            RenderSettings.ambientLight = blackout ? blackoutAmbient : ambient;
        }
    }
}
