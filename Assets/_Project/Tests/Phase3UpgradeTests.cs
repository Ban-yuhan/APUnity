using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace APProto.Tests
{
    public class Phase3UpgradeTests
    {
        [UnityTest]
        public IEnumerator Upgrades_ApplyEffects()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var gm = GameManager.Instance;
            var um = UpgradeManager.Instance;
            var cfg = gm.Config;

            // 돈 부족 시 실패
            gm.AddMoney(-gm.Money);
            Assert.IsFalse(um.TryUpgrade(UpgradeType.PlayerCapacity));
            Assert.AreEqual(1, um.Level(UpgradeType.PlayerCapacity));

            gm.AddMoney(100000);
            float before = gm.Money;
            Assert.IsTrue(um.TryUpgrade(UpgradeType.PlayerCapacity));
            Assert.AreEqual(before - cfg.playerCapacity.costs[1], gm.Money, 0.5f);
            Assert.AreEqual(5, um.PlayerCapacity);
            yield return null;
            Assert.AreEqual(5, GameObject.FindWithTag("Player").GetComponent<Carrier>().Capacity);

            // 입고 선반 Lv3 = 30개 -> 2호기 신설
            um.TryUpgrade(UpgradeType.DockCapacity);
            um.TryUpgrade(UpgradeType.DockCapacity);
            Assert.AreEqual(30, gm.InboundDock.Capacity);
            Assert.IsTrue(gm.InboundDock.transform.Find("Rack2").gameObject.activeSelf);

            // 출고 선반 Lv2 -> Lv3 = 3단
            var line = gm.GetLine(Region.Daegu);
            um.TryUpgrade(UpgradeType.RackCapacity);
            Assert.AreEqual(30, line.Rack.Capacity);
            Assert.IsTrue(line.Rack.transform.Find("Model/Shelf3").gameObject.activeSelf);
            Assert.IsFalse(line.Rack.transform.Find("Model/Shelf4").gameObject.activeSelf);

            // 2단계: 컨베이어, 3단계: 메가허브 (트럭/상차 발판 동쪽 이동)
            float truckX = line.Truck.transform.position.x;
            var loadPad = line.transform.Find("Pad_TruckLoad");
            float padX = loadPad.position.x;
            um.TryUpgrade(UpgradeType.WarehouseTier);
            Assert.IsTrue(line.transform.Find("Conveyor").gameObject.activeSelf);
            um.TryUpgrade(UpgradeType.WarehouseTier);
            Assert.IsTrue(um.IsMax(UpgradeType.WarehouseTier));
            Assert.IsFalse(um.TryUpgrade(UpgradeType.WarehouseTier));
            Assert.AreEqual(truckX + cfg.megahubTruckShift, line.Truck.transform.position.x, 0.01f);
            Assert.AreEqual(padX + cfg.megahubLoadPadShift, loadPad.position.x, 0.01f);
            Assert.AreEqual(30, gm.InboundDock.Capacity);  // 3단계 최소 30 vs Lv3 30
        }

        [UnityTest]
        public IEnumerator Conveyor_MovesInspectedToRack()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var gm = GameManager.Instance;
            gm.AddMoney(100000);
            Assert.IsTrue(UpgradeManager.Instance.TryUpgrade(UpgradeType.WarehouseTier));

            var line = gm.GetLine(Region.Daegu);
            line.Table.AddUninspected(new Package { Region = Region.Daegu });
            line.Table.AddUninspected(new Package { Region = Region.Daegu });
            line.Table.InspectAll();

            Time.timeScale = 5f;
            float t = 0;
            while (line.Rack.Packages.Count < 2)
            {
                t += Time.unscaledDeltaTime;
                if (t > 15f) Assert.Fail($"rack {line.Rack.Packages.Count}");
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.AreEqual(0, line.Table.Inspected.Count);
        }
    }
}
