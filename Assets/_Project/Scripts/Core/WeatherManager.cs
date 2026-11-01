using UnityEngine;

namespace APProto
{
    public enum Weather { Clear, Snow, Rain, Thunder }

    // 기상 악화 120초 + 20~40초 뒤 긴급 작업 발령 (하루 최대 2회) (원본 weather.js)
    // 미조치 시: 폭설 = 입고 트럭 진입 정지 / 폭우 = 이동속도 저하 / 뇌우 = 정전, 작업속도 저하
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("긴급 작업 발판 (발령 시에만 활성)")]
        [SerializeField] Pad snowPad;
        [SerializeField] Pad rainPad;
        [SerializeField] Pad thunderPad;

        [Header("연출")]
        [SerializeField] ParticleSystem snowFx;
        [SerializeField] ParticleSystem rainFx;
        [Tooltip("제설 필요 시 쌓이는 실외 눈")]
        [SerializeField] GameObject snowGround;
        [Tooltip("차수판 설치 완료 시 서쪽 출입구에 표시")]
        [SerializeField] GameObject floodBarrier;
        [Tooltip("발전기 경광등")]
        [SerializeField] Renderer generatorBeacon;
        [SerializeField] Color beaconIdle = new Color32(0x47, 0x55, 0x69, 0xff);
        [SerializeField] Color beaconAlarm = new Color32(0xef, 0x44, 0x44, 0xff);
        [SerializeField] Color beaconOk = new Color32(0x10, 0xb9, 0x81, 0xff);

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public Weather Current { get; private set; }
        public float Remaining { get; private set; }
        public bool EmergencyActive { get; private set; }   // 발령되었고 아직 미조치
        public bool EmergencyResolved { get; private set; }
        public int EmergenciesToday { get; private set; }

        public bool CanInbound => !(Current == Weather.Snow && EmergencyActive);
        public bool IsBlackout => Current == Weather.Thunder && EmergencyActive;
        public float SpeedMultiplier => Current == Weather.Rain && EmergencyActive ? cfg.rainSpeedMultiplier : 1f;
        public float WorkMultiplier => IsBlackout ? cfg.blackoutWorkMultiplier : 1f;

        // 뇌우 번개 (HUD 가 화면 섬광으로 표시)
        public event System.Action Lightning;

        GameConfig cfg;
        int clearDays;
        float emergencyDelay, thunderTimer, nextThunder;
        MaterialPropertyBlock mpb;

        void Awake()
        {
            Instance = this;
            cfg = GameManager.Instance.Config;
        }

        void Start() => ApplyVisuals();

        public void OnNewDay()
        {
            EmergenciesToday = 0;
            if (Current != Weather.Clear) return;
            clearDays++;
            if (clearDays >= cfg.clearDaysThreshold || Random.value < cfg.badWeatherChance)
                Trigger((Weather)Random.Range(1, 4));
        }

        public void Trigger(Weather weather)
        {
            if (weather == Weather.Clear)
            {
                Clear();
                return;
            }
            clearDays = 0;
            Current = weather;
            Remaining = cfg.weatherDuration;
            EmergencyActive = false;
            EmergencyResolved = false;
            emergencyDelay = Random.Range(cfg.emergencyDelayRange.x, cfg.emergencyDelayRange.y);
            ApplyVisuals();
            GameManager.Instance.Notify($"기상 예보: [{Name(weather)}] 시작! (기상 악화 주의)", Palette.Red);
        }

        void Clear()
        {
            Current = Weather.Clear;
            Remaining = 0;
            EmergencyActive = false;
            EmergencyResolved = false;
            ApplyVisuals();
            GameManager.Instance.Notify("날씨가 다시 맑아졌습니다.", Palette.Green);
        }

        // 테스트/디버그: 대기 없이 즉시 긴급 발령
        public void ForceEmergency() => emergencyDelay = 0;

        public void Resolve()
        {
            if (!EmergencyActive) return;
            EmergencyActive = false;
            EmergencyResolved = true;
            ApplyVisuals();
            GameManager.Instance.Notify(Current switch
            {
                Weather.Snow => "제설 작업 완료! 도로의 눈이 치워지고 입고 트럭 운행이 재개됩니다.",
                Weather.Rain => "차수판 설치 완료! 침수를 방지했습니다.",
                _ => "비상 발전기 가동 완료! 전력이 정상 복구되었습니다.",
            }, Palette.Green);
        }

        void Update()
        {
            if (Current == Weather.Clear) return;

            Remaining -= Time.deltaTime;
            if (!EmergencyActive && !EmergencyResolved && EmergenciesToday < cfg.maxEmergenciesPerDay)
            {
                emergencyDelay -= Time.deltaTime;
                if (emergencyDelay <= 0)
                {
                    EmergencyActive = true;
                    EmergenciesToday++;
                    ApplyVisuals();
                    GameManager.Instance.Notify($"긴급 경보 (오늘 {EmergenciesToday}/{cfg.maxEmergenciesPerDay}회): {QuestText}", Palette.Red);
                }
            }
            if (Remaining <= 0)
            {
                Clear();
                return;
            }

            if (Current == Weather.Thunder)
            {
                thunderTimer += Time.deltaTime;
                if (thunderTimer > nextThunder)
                {
                    thunderTimer = 0;
                    nextThunder = Random.Range(4f, 12f);
                    Lightning?.Invoke();
                }
            }

            if (IsBlackout && generatorBeacon)
                SetBeacon(Mathf.Sin(Time.time * 15f) > 0 ? beaconAlarm : beaconAlarm * 0.3f);
        }

        public string QuestText => Current switch
        {
            Weather.Snow => "제설 작업 필요! 실외 눈더미 발판에서 제설하세요. (입고 트럭 진입 차단)",
            Weather.Rain => "침수 위기! 서쪽 출입구 차수판을 설치하세요. (이동속도 저하)",
            Weather.Thunder => "정전 비상! 북서쪽 비상 발전기를 가동하세요. (작업속도 저하)",
            _ => "",
        };

        public static string Name(Weather w) => w switch
        {
            Weather.Snow => "폭설",
            Weather.Rain => "폭우",
            Weather.Thunder => "뇌우",
            _ => "맑음",
        };

        void ApplyVisuals()
        {
            Toggle(snowFx, Current == Weather.Snow);
            Toggle(rainFx, Current is Weather.Rain or Weather.Thunder);
            if (snowGround) snowGround.SetActive(Current == Weather.Snow && EmergencyActive);
            if (floodBarrier) floodBarrier.SetActive(Current == Weather.Rain && EmergencyResolved);
            snowPad.gameObject.SetActive(Current == Weather.Snow && EmergencyActive);
            rainPad.gameObject.SetActive(Current == Weather.Rain && EmergencyActive);
            thunderPad.gameObject.SetActive(Current == Weather.Thunder && EmergencyActive);
            if (generatorBeacon)
                SetBeacon(Current == Weather.Thunder && EmergencyResolved ? beaconOk : IsBlackout ? beaconAlarm : beaconIdle);
        }

        static void Toggle(ParticleSystem ps, bool on)
        {
            if (!ps) return;
            if (on && !ps.isPlaying) ps.Play();
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void SetBeacon(Color c)
        {
            mpb ??= new MaterialPropertyBlock();
            mpb.SetColor(BaseColor, c);
            generatorBeacon.SetPropertyBlock(mpb);
        }
    }
}
