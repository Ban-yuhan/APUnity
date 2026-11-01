using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APProto
{
    // 알바 관리창. 열려 있는 동안 게임 일시정지 (원본 openModal('worker')).
    public class WorkerPanel : MonoBehaviour
    {
        [Serializable]
        public class CardUI
        {
            public TMP_Text title;
            public TMP_Text body;
            public Button hireButton;
            public TMP_Text hireLabel;
        }

        [Serializable]
        public class VeteranUI
        {
            public WorkerRole role;
            public Button button;
            public TMP_Text label;
        }

        [SerializeField] GameObject window;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button rerollButton;
        [SerializeField] TMP_Text rerollLabel;
        [SerializeField] TMP_Text summaryText;
        [SerializeField] CardUI[] cards;
        [SerializeField] VeteranUI[] veterans;
        [SerializeField] Transform listContent;
        [SerializeField] WorkerRow rowPrefab;
        [SerializeField] TMP_Text emptyText;

        readonly List<WorkerRow> rows = new();

        void Start()
        {
            var wm = WorkerManager.Instance;
            openButton.onClick.AddListener(() => SetOpen(true));
            closeButton.onClick.AddListener(() => SetOpen(false));
            rerollButton.onClick.AddListener(() => wm.Reroll());
            for (int i = 0; i < cards.Length; i++)
            {
                int idx = i;
                cards[i].hireButton.onClick.AddListener(() => wm.Hire(idx));
            }
            foreach (var v in veterans)
            {
                var role = v.role;
                v.button.onClick.AddListener(() => wm.HireVeteran(role));
            }
            wm.Changed += Refresh;
            window.SetActive(false);
        }

        void OnDestroy()
        {
            if (WorkerManager.Instance) WorkerManager.Instance.Changed -= Refresh;
        }

        public void SetOpen(bool open)
        {
            if (open) WorkerManager.Instance.GenerateCards();
            window.SetActive(open);
            Time.timeScale = open ? 0 : 1;
            Refresh();
        }

        void Update()
        {
            // 잔여계약/등급/금액 표시 갱신
            if (window.activeSelf) Refresh();
        }

        void Refresh()
        {
            if (!window.activeSelf) return;
            var wm = WorkerManager.Instance;
            var gm = GameManager.Instance;
            var cfg = gm.Config;

            summaryText.text = $"소지금 {Mathf.FloorToInt(gm.Money):N0}원   ·   근무 {wm.Workers.Count}명   ·   인건비 {Mathf.RoundToInt(wm.WagesPerMinute()):N0}원/분";
            rerollLabel.text = $"카드 새로고침 ({cfg.rerollCost}원)";

            for (int i = 0; i < cards.Length; i++)
            {
                var card = wm.Cards[i];
                var role = cfg.GetRole(card.Role);
                bool full = wm.IsRoleFull(card.Role);
                cards[i].title.text = $"{role.displayName}\n<size=70%>{card.Name}</size>";
                cards[i].body.text = $"{role.description}\n\n현재 정원: {wm.CountOf(card.Role)} / {wm.MaxCountOf(card.Role)}명\n" +
                                     $"채용비용: {role.hireCost:N0}원\n시급: {Mathf.RoundToInt(role.wagePerSecond * 60):N0}원/분";
                cards[i].hireButton.interactable = !full;
                cards[i].hireLabel.text = full ? "정원 마감" : "채용하기";
            }

            foreach (var v in veterans)
            {
                bool full = wm.IsRoleFull(v.role);
                v.button.interactable = !full;
                v.label.text = $"{cfg.GetRole(v.role).displayName}\n<size=75%>{(full ? "마감" : $"{cfg.veteranHireCost}원 · {cfg.veteranDuration:0}초 · 2배속")}</size>";
            }

            while (rows.Count < wm.Workers.Count) rows.Add(Instantiate(rowPrefab, listContent));
            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < wm.Workers.Count;
                rows[i].gameObject.SetActive(active);
                if (active) rows[i].Bind(wm.Workers[i]);
            }
            emptyText.gameObject.SetActive(wm.Workers.Count == 0);
        }
    }
}
