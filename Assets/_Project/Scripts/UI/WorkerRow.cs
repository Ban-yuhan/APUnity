using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APProto
{
    // 알바 관리창의 근무자 1줄 (근무자 수가 가변이라 프리팹)
    public class WorkerRow : MonoBehaviour
    {
        [SerializeField] TMP_Text info;
        [SerializeField] Button fireButton;

        Worker worker;

        public void Bind(Worker w)
        {
            if (worker != w)
            {
                worker = w;
                fireButton.onClick.RemoveAllListeners();
                fireButton.onClick.AddListener(() => WorkerManager.Instance.Fire(w));
            }

            var cfg = GameManager.Instance.Config;
            var tier = cfg.proficiencyTiers[w.Tier];
            string timer = w.IsVeteran ? $"  (남은계약: {Mathf.CeilToInt(w.VeteranRemaining)}초)" : "";
            info.text = $"{w.DisplayName}  [{cfg.GetRole(w.Role).displayName}]  <color=#{ColorUtility.ToHtmlStringRGB(tier.color)}>{tier.displayName}</color>{timer}" +
                        $"    {Mathf.RoundToInt(w.WagePerSecond * 60):N0}원/분";
        }
    }
}
