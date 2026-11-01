using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace APProto.Tests
{
    public class Phase4VehicleTests
    {
        GameManager gm;
        UpgradeManager um;
        Transform player;
        PlayerVehicles vehicles;

        IEnumerator Setup(int tier)
        {
            SceneManager.LoadScene("Main");
            yield return null;
            gm = GameManager.Instance;
            um = UpgradeManager.Instance;
            gm.AddMoney(100000);
            for (int i = 1; i < tier; i++) Assert.IsTrue(um.TryUpgrade(UpgradeType.WarehouseTier));
            player = GameObject.FindWithTag("Player").transform;
            vehicles = player.GetComponent<PlayerVehicles>();
            Assert.IsNotNull(vehicles);
            yield return null;
        }

        void Teleport(Vector3 pos)
        {
            player.position = pos;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator Cart_RaisesCapacityAndShowsCargoInCart()
        {
            yield return Setup(1);
            Assert.AreEqual(0, Cart.Active.Count, "1단계에는 카트 없음");
            Assert.IsTrue(um.TryUpgrade(UpgradeType.WarehouseTier));
            Assert.AreEqual(3, Cart.Active.Count);

            var cart = Cart.Active[0];
            Teleport(cart.transform.position + Vector3.back);  // 카트들이 X 축으로 1.5m 간격이라 Z 로 비킴
            vehicles.Toggle();
            Assert.AreEqual(cart, vehicles.Cart);
            yield return null;
            var carrier = player.GetComponent<Carrier>();
            Assert.AreEqual(gm.Config.cartCapacity, carrier.Capacity);

            carrier.Add(new Package());
            Assert.AreEqual(1, cart.CargoStack.transform.childCount);
            Assert.AreEqual(0, player.Find("HandAnchor").GetComponentsInChildren<PackageView>().Length);

            vehicles.Toggle();
            yield return null;
            Assert.IsNull(vehicles.Cart);
            Assert.AreEqual(um.PlayerCapacity, carrier.Capacity);
            Assert.AreEqual(1, player.Find("HandAnchor").GetComponentsInChildren<PackageView>().Length);
        }

        [UnityTest]
        public IEnumerator PlayerForklift_PalletCountsAsEight()
        {
            yield return Setup(3);
            Assert.AreEqual(2, Forklift.Active.Count);
            var line = gm.GetLine(Region.Daegu);
            for (int i = 0; i < 9; i++) line.Rack.Add(new Package { Region = Region.Daegu, IsInspected = true });

            var fork = Forklift.Active[0];
            Teleport(fork.transform.position + Vector3.left);
            vehicles.Toggle();
            Assert.AreEqual(fork, vehicles.Forklift);

            fork.transform.position = Pad.Find(PadType.OutboundTake, line.Rack).transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.IsNotNull(fork.Pallet);
            Assert.AreEqual(1, line.Rack.Packages.Count);

            fork.transform.position = Pad.Find(PadType.TruckLoad, line.Truck).transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.IsNull(fork.Pallet);
            Assert.AreEqual(1, line.Truck.Cargo.Count);
            Assert.AreEqual(8, line.Truck.TotalCount);

            float before = gm.Money;
            Assert.IsTrue(line.Truck.TryDepart());
            Assert.AreEqual(before + 8 * gm.Config.basePrice - gm.Config.fuelCost, gm.Money, 1f);

            vehicles.Toggle();
            Assert.IsNull(vehicles.Forklift);
        }

        [UnityTest]
        public IEnumerator LoaderWorker_DrivesForkliftAndReturns()
        {
            yield return Setup(3);
            var line = gm.GetLine(Region.Daegu);
            for (int i = 0; i < 8; i++) line.Rack.Add(new Package { Region = Region.Daegu, IsInspected = true });
            var w = WorkerManager.Instance.Spawn(WorkerRole.Loader, "테스트", false);

            Time.timeScale = 6f;
            float t = 0;
            while (line.Truck.TotalCount < 8 || Forklift.Active.Exists(f => f.HasDriver))
            {
                t += Time.unscaledDeltaTime;
                if (t > 40f) Assert.Fail($"truck {line.Truck.TotalCount}, rack {line.Rack.Packages.Count}");
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.AreEqual(1, line.Truck.Cargo.Count);
            Assert.AreEqual(0, line.Rack.Packages.Count);
        }
    }
}
