using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace APProto
{
    // 알바 고용/해고, 고용 카드 3장, 인건비, 검수 알바 재분배 (원본 WorkerManager)
    public class WorkerManager : MonoBehaviour
    {
        public static WorkerManager Instance { get; private set; }

        public struct HireCard
        {
            public WorkerRole Role;
            public string Name;
        }

        [SerializeField] Worker workerPrefab;
        [SerializeField] Transform spawnPoint;
        [SerializeField] float spawnSpread = 2f;

        public readonly List<Worker> Workers = new();
        public readonly HireCard[] Cards = new HireCard[3];
        public event Action Changed;

        GameConfig cfg;

        void Awake() => Instance = this;

        void Start()
        {
            cfg = GameManager.Instance.Config;
            GenerateCards();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var gm = GameManager.Instance;

            RebalanceInspectors();

            float wages = 0;
            for (int i = Workers.Count - 1; i >= 0; i--)
            {
                var w = Workers[i];
                wages += w.WagePerSecond * dt * DayNightCycle.Instance.NightWageMultiplier;
                w.Tick(dt);
                if (w.IsVeteran && w.VeteranRemaining <= 0)
                {
                    gm.Notify($"단기 베테랑 알바 [{w.DisplayName}] 님의 계약이 만료되어 퇴장했습니다.", Palette.Orange);
                    Remove(w);
                }
            }
            if (wages > 0) gm.AddMoney(-Mathf.Min(wages, gm.Money));
        }

        // ------------------------------------------------------------ 정원

        public int CountOf(WorkerRole role) => Workers.FindAll(w => w.Role == role).Count;

        public int MaxCountOf(WorkerRole role) =>
            role == WorkerRole.Inspector ? GameManager.Instance.UnlockedRegions.Count * 2 : cfg.GetRole(role).maxCount;

        public bool IsRoleFull(WorkerRole role) => CountOf(role) >= MaxCountOf(role);

        // ------------------------------------------------------------ 고용 카드

        HireCard RandomCard()
        {
            var all = (WorkerRole[])Enum.GetValues(typeof(WorkerRole));
            var open = Array.FindAll(all, r => !IsRoleFull(r));
            // 정원이 남은 직종 85% 우선
            var pool = open.Length > 0 && Random.value < 0.85f ? open : all;
            return new HireCard
            {
                Role = pool[Random.Range(0, pool.Length)],
                Name = cfg.workerNames[Random.Range(0, cfg.workerNames.Length)],
            };
        }

        public void GenerateCards()
        {
            for (int i = 0; i < Cards.Length; i++) Cards[i] = RandomCard();
            SortCards();
            Changed?.Invoke();
        }

        // 마감된 직종 카드는 뒤로
        void SortCards() => Array.Sort(Cards, (a, b) => IsRoleFull(a.Role).CompareTo(IsRoleFull(b.Role)));

        public bool Reroll()
        {
            var gm = GameManager.Instance;
            if (gm.Money < cfg.rerollCost)
            {
                gm.Notify($"소지금이 부족하여 리롤할 수 없습니다. (필요: {cfg.rerollCost}원)", Palette.Red);
                return false;
            }
            gm.AddMoney(-cfg.rerollCost);
            GenerateCards();
            return true;
        }

        public bool Hire(int cardIndex)
        {
            var card = Cards[cardIndex];
            var role = cfg.GetRole(card.Role);
            if (!CanHire(card.Role, role.hireCost)) return false;

            GameManager.Instance.AddMoney(-role.hireCost);
            Spawn(card.Role, card.Name, false);
            Cards[cardIndex] = RandomCard();
            SortCards();
            GameManager.Instance.Notify($"[{role.displayName}] {card.Name} 님이 채용되었습니다!", Palette.Green);
            Changed?.Invoke();
            return true;
        }

        public bool HireVeteran(WorkerRole roleKey)
        {
            if (!CanHire(roleKey, cfg.veteranHireCost)) return false;

            GameManager.Instance.AddMoney(-cfg.veteranHireCost);
            string name = cfg.workerNames[Random.Range(0, cfg.workerNames.Length)];
            Spawn(roleKey, name, true);
            GameManager.Instance.Notify($"단기 베테랑 [{cfg.GetRole(roleKey).displayName}] {name} 님이 합류했습니다! ({cfg.veteranDuration:0}초)", Palette.Gold);
            Changed?.Invoke();
            return true;
        }

        bool CanHire(WorkerRole roleKey, int cost)
        {
            var gm = GameManager.Instance;
            if (gm.Money < cost)
            {
                gm.Notify($"소지금이 부족합니다! (필요: {cost:N0}원)", Palette.Red);
                return false;
            }
            if (IsRoleFull(roleKey))
            {
                gm.Notify(roleKey == WorkerRole.Inspector
                    ? "해금된 검수대 정원이 가득 찼습니다. 노선을 확장하세요!"
                    : $"[{cfg.GetRole(roleKey).displayName}] 정원이 가득 찼습니다! (최대 {MaxCountOf(roleKey)}명)", Palette.Red);
                return false;
            }
            return true;
        }

        // 알바 수가 가변이라 프리팹 Instantiate
        public Worker Spawn(WorkerRole role, string displayName, bool veteran)
        {
            var offset = new Vector3(Random.Range(-spawnSpread, spawnSpread), 0, Random.Range(-spawnSpread, spawnSpread));
            var w = Instantiate(workerPrefab, spawnPoint.position + offset, Quaternion.identity, transform);
            w.name = $"Worker_{role}_{displayName}";
            w.Init(role, displayName, veteran);
            Workers.Add(w);
            return w;
        }

        // 들고 있던 택배는 함께 사라짐 (원본 동일)
        public void Fire(Worker w)
        {
            GameManager.Instance.Notify($"{w.DisplayName} 님을 퇴사 처리했습니다.", Color.gray);
            Remove(w);
        }

        void Remove(Worker w)
        {
            Workers.Remove(w);
            Destroy(w.gameObject);
            Changed?.Invoke();
        }

        public float WagesPerMinute()
        {
            float sum = 0;
            foreach (var w in Workers) sum += w.WagePerSecond;
            return sum * 60;
        }

        // ------------------------------------------------------------ 검수 알바 배치

        // 해금 검수대마다 메인 1명 먼저, 남으면 보조로 2인 1조. 노선 해금 시 보조가 새 검수대 메인으로 이동.
        void RebalanceInspectors()
        {
            var unlocked = GameManager.Instance.UnlockedRegions;
            int idx = 0;
            foreach (var w in Workers)
            {
                if (w.Role != WorkerRole.Inspector) continue;
                w.AssignedRegion = unlocked[idx % unlocked.Count];
                w.IsAssistant = idx >= unlocked.Count;
                idx++;
            }
        }

        public bool HasInspectorAt(Region region, bool assistant) =>
            Workers.Exists(w => w.Role == WorkerRole.Inspector && w.AssignedRegion == region && w.IsAssistant == assistant && w.ReachedTarget);
    }
}
