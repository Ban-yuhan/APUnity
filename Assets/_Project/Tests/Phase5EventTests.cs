using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace APProto.Tests
{
    public class Phase5EventTests
    {
        GameManager gm;
        WeatherManager wm;

        IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            gm = GameManager.Instance;
            wm = WeatherManager.Instance;
        }

        // 5개 실은 대구 트럭 출발 정산액
        float DepartFive()
        {
            var truck = gm.GetLine(Region.Daegu).Truck;
            for (int i = 0; i < 5; i++) truck.Load(new Package { Region = Region.Daegu, IsInspected = true });
            float before = gm.Money;
            Assert.IsTrue(truck.TryDepart());
            return gm.Money - before;
        }

        [UnityTest]
        public IEnumerator NightAndFever_MultiplyRevenue()
        {
            yield return Load();
            var cfg = gm.Config;
            DayNightCycle.Instance.SetElapsed(cfg.dayLength + 1);
            yield return null;
            Assert.IsTrue(DayNightCycle.Instance.IsNight);

            Assert.IsTrue(gm.TryStartFever());
            Assert.IsFalse(gm.TryStartFever());
            float expected = 5 * cfg.basePrice * cfg.nightProfitMultiplier * cfg.feverProfitMultiplier - cfg.fuelCost;
            Assert.AreEqual(expected, DepartFive(), 1f);
        }

        [UnityTest]
        public IEnumerator Snow_BlocksInboundUntilPlayerClears()
        {
            yield return Load();
            wm.Trigger(Weather.Snow);
            wm.ForceEmergency();
            yield return null;
            Assert.IsFalse(wm.CanInbound);

            var pad = GameObject.Find("Pad_Snow");
            Assert.IsNotNull(pad, "긴급 발령 시 제설 발판 활성");
            var player = GameObject.FindWithTag("Player").transform;
            player.position = pad.transform.position;
            Physics.SyncTransforms();

            Time.timeScale = 4f;
            float t = 0;
            while (!wm.EmergencyResolved)
            {
                t += Time.unscaledDeltaTime;
                if (t > 5f) Assert.Fail("제설 미완료");
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.IsTrue(wm.CanInbound);
            Assert.IsNull(GameObject.Find("Pad_Snow"));
        }

        [UnityTest]
        public IEnumerator RainAndThunder_ApplyPenaltiesUntilResolved()
        {
            yield return Load();
            var cfg = gm.Config;
            wm.Trigger(Weather.Rain);
            Assert.AreEqual(1f, wm.SpeedMultiplier, "발령 전에는 패널티 없음");
            wm.ForceEmergency();
            yield return null;
            Assert.AreEqual(cfg.rainSpeedMultiplier, wm.SpeedMultiplier);
            wm.Resolve();
            Assert.AreEqual(1f, wm.SpeedMultiplier);

            wm.Trigger(Weather.Thunder);
            wm.ForceEmergency();
            yield return null;
            Assert.IsTrue(wm.IsBlackout);
            Assert.AreEqual(cfg.blackoutWorkMultiplier, wm.WorkMultiplier);
            wm.Trigger(Weather.Clear);
            Assert.AreEqual(1f, wm.WorkMultiplier);
        }
    }
}
