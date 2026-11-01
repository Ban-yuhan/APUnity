using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace APProto.Tests
{
    // 입고 -> 스캔 -> 분류 -> 검수 -> 출고 선반 -> 트럭 상차 -> 출발 정산 한 바퀴
    public class Phase1LoopTests
    {
        Transform player;
        Carrier carrier;

        [UnityTest]
        public IEnumerator FullLoop_EarnsMoney()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            Time.timeScale = 3f;

            var gm = GameManager.Instance;
            player = GameObject.FindWithTag("Player").transform;
            carrier = player.GetComponent<Carrier>();
            var dock = gm.InboundDock;
            var line = gm.GetLine(Region.Daegu);
            var table = line.GetComponentInChildren<SortingTable>();
            var rack = line.GetComponentInChildren<OutboundRack>();
            var truck = line.Truck;

            for (int i = 0; i < 2; i++)
            {
                yield return StandUntil("Pad_TruckUnload", () => carrier.IsFull);
                yield return StandUntil("Pad_DockPut", () => carrier.Count == 0);
            }
            Assert.AreEqual(8, dock.Packages.Count);

            yield return StandUntil("Pad_Scan", () => !dock.HasUnscanned);
            Assert.IsTrue(dock.Packages.TrueForAll(p => p.Region == Region.Daegu));

            for (int i = 0; i < 2; i++)
            {
                yield return StandUntil("Pad_DockTake", () => carrier.IsFull);
                yield return StandUntil("Pad_SortPut", () => carrier.Count == 0);
            }
            Assert.AreEqual(8, table.Uninspected.Count);
            Capture("phase1_table.png");

            yield return StandUntil("Pad_Inspect", () => table.Uninspected.Count == 0);
            Assert.AreEqual(8, table.Inspected.Count);

            yield return StandUntil("Pad_SortTake", () => carrier.IsFull);
            yield return StandUntil("Pad_OutboundPut", () => carrier.Count == 0);
            Assert.AreEqual(4, rack.Packages.Count);
            yield return StandUntil("Pad_SortTake", () => carrier.IsFull);
            Capture("phase1_carry.png");
            yield return StandUntil("Pad_TruckLoad", () => carrier.Count == 0);
            yield return StandUntil("Pad_OutboundTake", () => carrier.IsFull);
            yield return StandUntil("Pad_TruckLoad", () => carrier.Count == 0);
            Assert.AreEqual(8, truck.Cargo.Count);

            float before = gm.Money;
            Assert.IsTrue(truck.TryDepart());
            // 8 * 320 * 1.0 - 150 = 2410
            Assert.AreEqual(before + 2410f, gm.Money, 1f);
            Time.timeScale = 1f;
        }

        IEnumerator StandUntil(string padName, Func<bool> done, float timeout = 10f)
        {
            player.position = GameObject.Find(padName).transform.position;
            Physics.SyncTransforms();
            float t = 0;
            while (!done())
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail($"{padName} timed out (carrying {carrier.Count})");
                yield return null;
            }
        }

        static void Capture(string file)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            cam.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../Logs", file), tex.EncodeToPNG());
        }
    }
}
