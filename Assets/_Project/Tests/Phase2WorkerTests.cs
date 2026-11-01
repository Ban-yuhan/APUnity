using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace APProto.Tests
{
    public class Phase2WorkerTests
    {
        [UnityTest]
        public IEnumerator HireCard_ChargesCostAndRespectsCap()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var gm = GameManager.Instance;
            var wm = WorkerManager.Instance;
            gm.AddMoney(100000);

            float before = gm.Money;
            var role = wm.Cards[0].Role;
            Assert.IsTrue(wm.Hire(0));
            Assert.AreEqual(1, wm.Workers.Count);
            Assert.AreEqual(before - gm.Config.GetRole(role).hireCost, gm.Money, 0.5f);

            // 대구만 해금: 검수 알바 정원 2
            Assert.AreEqual(2, wm.MaxCountOf(WorkerRole.Inspector));
            while (!wm.IsRoleFull(WorkerRole.Inspector)) Assert.IsTrue(wm.HireVeteran(WorkerRole.Inspector));
            Assert.IsFalse(wm.HireVeteran(WorkerRole.Inspector));
        }

        // 플레이어 없이 알바 5종만으로 입고 -> 출고 트럭 적재까지 돌아가는지
        [UnityTest]
        public IEnumerator FiveRoles_RunPipelineWithoutPlayer()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var gm = GameManager.Instance;
            var wm = WorkerManager.Instance;
            gm.AddMoney(100000);
            foreach (WorkerRole role in System.Enum.GetValues(typeof(WorkerRole)))
                wm.Spawn(role, role.ToString(), false);

            var truck = gm.GetLine(Region.Daegu).Truck;
            Time.timeScale = 8f;
            float t = 0;
            while (truck.Cargo.Count < 5)
            {
                t += Time.unscaledDeltaTime;
                if (t > 60f) Assert.Fail($"timeout: dock {gm.InboundDock.Packages.Count}, truck {truck.Cargo.Count}");
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.IsTrue(truck.Cargo.TrueForAll(p => p.IsInspected && p.Region == Region.Daegu));
        }
    }
}
