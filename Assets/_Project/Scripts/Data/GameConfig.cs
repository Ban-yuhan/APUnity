using System;
using UnityEngine;

namespace APProto
{
    // 원본 config.js 의 GAME_CONFIG 수치. Inspector 에서 조절.
    [CreateAssetMenu(menuName = "APProto/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Serializable]
        public class RegionData
        {
            public Region region;
            public string displayName;
            public Color color = Color.white;
            public float priceMultiplier = 1f;
            public int unlockCost;
        }

        [Header("초기값")]
        public int startMoney = 2000;
        public int startReputation = 50;
        public int testMoneyAmount = 1000;

        [Header("지역")]
        public RegionData[] regions =
        {
            new RegionData { region = Region.Daegu, displayName = "대구", color = new Color32(0xf3, 0x9c, 0x12, 0xff), priceMultiplier = 1.0f, unlockCost = 0 },
            new RegionData { region = Region.Busan, displayName = "부산", color = new Color32(0x00, 0x88, 0xff, 0xff), priceMultiplier = 1.35f, unlockCost = 1500 },
            new RegionData { region = Region.Seoul, displayName = "서울", color = new Color32(0x2e, 0xcc, 0x71, 0xff), priceMultiplier = 1.7f, unlockCost = 3500 },
        };

        [Header("택배")]
        public Color unscannedBoxColor = new Color32(0xd4, 0xa3, 0x73, 0xff);

        [Header("입고")]
        public int inboundTruckCapacity = 10;
        public float inboundTruckWaitTime = 20f;
        public float inboundArriveSpeed = 14f;
        public float inboundDepartSpeed = 18f;

        [Header("출고")]
        public int outboundTruckCapacity = 10;
        public int minDepartureCount = 5;
        public int basePrice = 320;
        public int fuelCost = 150;
        public int fullLoadBonus = 600;
        public int fullLoadReputation = 10;
        public float autoDepartDelay = 2f;
        public float outboundAwayTime = 6f;
        public float outboundArriveSpeed = 14f;
        public float outboundDepartSpeed = 20f;

        [Header("작업")]
        [Tooltip("게이지형 발판(스캔/검수) 완료 시간")]
        public float padActionDuration = 3f;
        [Tooltip("들기/두기 1개당 간격")]
        public float transferInterval = 0.18f;

        [Serializable]
        public class UpgradeTrack
        {
            public string displayName;
            public string unit;
            [Tooltip("레벨별 값 (Lv1 = [0])")]
            public float[] values;
            [Tooltip("해당 레벨로 올리는 비용 ([0] 은 미사용)")]
            public int[] costs;
            [Tooltip("비워두면 값+단위로 표시")]
            public string[] descriptions;
        }

        [Serializable]
        public class WarehouseTier
        {
            public string displayName;
            public int cost;
            public float maintenancePerSecond;
            [Tooltip("이 단계의 입고 선반 최소 용량 (용량 업그레이드와 큰 쪽 적용)")]
            public int minDockCapacity;
            [TextArea] public string description;
        }

        [Header("업그레이드")]
        public UpgradeTrack playerSpeed = new() { displayName = "이동 속도", values = new[] { 5.2f, 6.2f, 7.2f, 8.2f, 9.5f }, costs = new[] { 0, 300, 600, 1000, 1600 },
            descriptions = new[] { "기본 속도", "+20% 가속", "+38% 가속", "+58% 가속", "+82% 초고속" } };
        public UpgradeTrack playerCapacity = new() { displayName = "최대 소지량", unit = "개", values = new[] { 4f, 5, 6, 7, 8 }, costs = new[] { 0, 400, 800, 1400, 2200 } };
        public UpgradeTrack playerWorkSpeed = new() { displayName = "작업 속도", unit = "배속", values = new[] { 1f, 1.3f, 1.6f, 2f, 2.6f }, costs = new[] { 0, 350, 700, 1200, 1900 } };
        public UpgradeTrack truckInterval = new() { displayName = "트럭 배차 간격", unit = "초", values = new[] { 25f, 22, 20, 17, 15 }, costs = new[] { 0, 500, 1000, 1800, 2800 } };
        public UpgradeTrack dockCapacity = new() { displayName = "입고 선반 적재 용량", unit = "개", values = new[] { 10f, 20, 30, 35, 40 }, costs = new[] { 0, 400, 800, 1500, 2600 } };
        public UpgradeTrack rackCapacity = new() { displayName = "출고 선반 적재 용량", unit = "개", values = new[] { 10f, 20, 30, 35, 40 }, costs = new[] { 0, 450, 900, 1600, 2800 } };
        [Tooltip("출고 선반 시작 레벨 (원본: Lv.2 = 20개)")]
        public int rackStartLevel = 2;

        public WarehouseTier[] warehouseTiers =
        {
            new WarehouseTier { displayName = "작은 창고", cost = 0, maintenancePerSecond = 0.5f, minDockCapacity = 10, description = "기본 물류 창고. (유지비: 30원/분)" },
            new WarehouseTier { displayName = "중형 물류센터", cost = 3000, maintenancePerSecond = 1.5f, minDockCapacity = 20, description = "컨베이어 벨트 해금: 검수 완료품을 출고 선반으로 자동 이송 (유지비: 90원/분)" },
            new WarehouseTier { displayName = "앙팡 메가허브", cost = 8000, maintenancePerSecond = 3f, minDockCapacity = 30, description = "출고 하역장 8m 확장 (유지비: 180원/분)" },
        };
        [Tooltip("컨베이어 1개 이송 간격 (원본 4 / 1.2)")]
        public float conveyorInterval = 3.33f;
        public float megahubTruckShift = 8f;
        public float megahubLoadPadShift = 8.5f;

        [Header("주야간 (하루 = 낮 + 밤)")]
        public float dayLength = 90f;
        public float nightLength = 30f;
        public float nightWageMultiplier = 1.5f;
        public float nightProfitMultiplier = 1.5f;
        public Color daySunColor = new Color32(0xff, 0xf5, 0xe6, 0xff);
        public float daySunIntensity = 1.25f;
        public Color dayBackground = new Color32(0xdc, 0xe7, 0xf0, 0xff);
        public Color nightSunColor = new Color32(0x3a, 0x50, 0x6b, 0xff);
        public float nightSunIntensity = 0.22f;
        public Color nightBackground = new Color32(0x0b, 0x13, 0x2b, 0xff);

        [Header("앙팡데이 피버타임")]
        public float feverDuration = 300f;
        public float feverCooldown = 180f;
        [Tooltip("입고 배차 간격 배율")]
        public float feverIntervalMultiplier = 0.5f;
        public float feverProfitMultiplier = 2f;

        [Header("기상 악화")]
        public float weatherDuration = 120f;
        [Range(0, 1)] public float badWeatherChance = 0.35f;
        [Tooltip("맑은 날이 이만큼 이어지면 다음 날 확정 발생")]
        public int clearDaysThreshold = 5;
        public Vector2 emergencyDelayRange = new(20f, 40f);
        public int maxEmergenciesPerDay = 2;
        [Tooltip("폭우 미조치: 이동속도 배율")]
        public float rainSpeedMultiplier = 0.55f;
        [Tooltip("뇌우 미조치(정전): 작업속도 배율")]
        public float blackoutWorkMultiplier = 0.4f;

        [Header("탈것 (카트: 2단계, 지게차: 3단계)")]
        public int cartCapacity = 8;
        public float cartSpeedMultiplier = 1.3f;
        public float cartBoardRange = 2.5f;
        public float forkliftBoardRange = 3.2f;
        public float forkliftSpeed = 4.8f;
        public int palletSize = 8;
        [Tooltip("지게차가 출고 선반 꺼내기 / 트럭 상차 발판에서 작업하는 거리")]
        public float forkliftWorkRange = 3.3f;

        [Serializable]
        public class WorkerRoleData
        {
            public WorkerRole role;
            public string displayName;
            [TextArea] public string description;
            public float wagePerSecond;
            public int hireCost;
            [Tooltip("검수 알바는 무시 (해금 검수대 수 x 2)")]
            public int maxCount;
            public Color uniformColor = Color.gray;
        }

        [Serializable]
        public class ProficiencyTier
        {
            public string displayName;
            public float minWorkSeconds;
            public Color color = Color.white;
            public float speedMultiplier = 1f;
            public float workMultiplier = 1f;
            public int capacity = 2;
        }

        [Header("알바")]
        public float workerBaseSpeed = 4.8f;
        public int rerollCost = 50;
        public WorkerRoleData[] workerRoles =
        {
            new WorkerRoleData { role = WorkerRole.Unloader, displayName = "하차 알바", description = "입고 트럭에서 입고 선반으로 택배를 옮깁니다.", wagePerSecond = 2.0f, hireCost = 250, maxCount = 4, uniformColor = new Color32(0xe6, 0x7e, 0x22, 0xff) },
            new WorkerRoleData { role = WorkerRole.Scanner, displayName = "스캔 알바", description = "입고 선반 스캔 발판에 상주하며 송장 스캔을 전담합니다.", wagePerSecond = 2.5f, hireCost = 350, maxCount = 2, uniformColor = new Color32(0x9b, 0x59, 0xb6, 0xff) },
            new WorkerRoleData { role = WorkerRole.Inspector, displayName = "검수 알바", description = "분류대 검수 발판에서 검수를 전담합니다. (해금 검수대당 최대 2인 협업)", wagePerSecond = 2.5f, hireCost = 350, maxCount = 6, uniformColor = new Color32(0x1a, 0xbc, 0x9c, 0xff) },
            new WorkerRoleData { role = WorkerRole.Carrier, displayName = "운반 알바", description = "스캔된 택배를 분류대로, 검수 완료된 택배를 출고 트럭/선반으로 운반합니다.", wagePerSecond = 2.2f, hireCost = 300, maxCount = 4, uniformColor = new Color32(0x34, 0x98, 0xdb, 0xff) },
            new WorkerRoleData { role = WorkerRole.Loader, displayName = "상차 알바", description = "출고 선반의 택배를 배송 트럭에 적재합니다.", wagePerSecond = 2.4f, hireCost = 320, maxCount = 4, uniformColor = new Color32(0x27, 0xae, 0x60, 0xff) },
        };
        public ProficiencyTier[] proficiencyTiers =
        {
            new ProficiencyTier { displayName = "신입", minWorkSeconds = 0, color = Color.white, speedMultiplier = 1f, workMultiplier = 1f, capacity = 2 },
            new ProficiencyTier { displayName = "일반", minWorkSeconds = 60, color = new Color32(0x4d, 0xc9, 0xff, 0xff), speedMultiplier = 1.2f, workMultiplier = 1.25f, capacity = 3 },
            new ProficiencyTier { displayName = "숙련", minWorkSeconds = 180, color = new Color32(0xb3, 0x66, 0xff, 0xff), speedMultiplier = 1.4f, workMultiplier = 1.55f, capacity = 4 },
            new ProficiencyTier { displayName = "베테랑", minWorkSeconds = 360, color = new Color32(0xff, 0xcc, 0x00, 0xff), speedMultiplier = 1.65f, workMultiplier = 1.9f, capacity = 6 },
        };

        [Header("단기 베테랑 알바")]
        public float veteranDuration = 180f;
        public int veteranHireCost = 750;
        public float veteranSpeedMultiplier = 2f;
        public float veteranWorkMultiplier = 2f;
        public float veteranWageMultiplier = 1.5f;

        public string[] workerNames =
        {
            "김민수", "이지훈", "박서연", "최현우", "정다은", "강준혁", "윤서아", "임도윤",
            "한예린", "오승우", "신유진", "배태양", "송하은", "류시원", "권지민",
        };

        public RegionData GetRegion(Region region) => Array.Find(regions, r => r.region == region);
        public WorkerRoleData GetRole(WorkerRole role) => Array.Find(workerRoles, r => r.role == role);
    }
}
