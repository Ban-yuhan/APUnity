using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace APProto.EditorTools
{
    // 4단계: 탈것 (Cart / Forklift 프리팹, 씬 배치, Player 에 PlayerVehicles, HUD 탑승 버튼)
    public static partial class SceneBuilder
    {
        [MenuItem("APProto/Add Phase4 (카트·지게차) to Main Scene")]
        public static void AddPhase4()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LoadShared();
            BuildPhase4Content(AssetDatabase.LoadAssetAtPath<PackageView>(PrefabDir + "/Package.prefab"));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] phase4 done");
        }

        static void BuildPhase4Content(PackageView packagePrefab)
        {
            var cartPrefab = BuildCartPrefab(packagePrefab);
            var forkPrefab = BuildForkliftPrefab();
            AddPlayerVehicles();

            foreach (var name in new[] { "Vehicles", "RideButton" })
            {
                var old = GameObject.Find(name);
                if (old) Object.DestroyImmediate(old);
            }

            var vehicles = new GameObject("Vehicles").transform;
            var carts = Node("Carts", vehicles, Vector3.zero);
            foreach (var x in new[] { -19.5f, -18f, -16.5f })
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab, carts.transform);
                c.transform.position = W(x, 0, 8);
            }
            var forks = Node("Forklifts", vehicles, Vector3.zero);
            for (int i = 0; i < 2; i++)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(forkPrefab, forks.transform);
                f.name = $"Forklift_{i + 1}";
                f.transform.SetPositionAndRotation(W(19.5f, 0, i == 0 ? -3.5f : 3.5f), Quaternion.Euler(0, -90, 0));
            }
            carts.SetActive(false);
            forks.SetActive(false);

            var um = Object.FindAnyObjectByType<UpgradeManager>();
            Set(um, "carts", carts);
            Set(um, "forklifts", forks);

            // 노선 출발/해금 버튼: 화면 중앙 오른쪽은 메가허브 트럭을 가려서 우상단으로
            var hud = GameObject.Find("HUD").transform;
            int row = 0;
            foreach (var region in new[] { Region.Daegu, Region.Busan, Region.Seoul })
            {
                foreach (var prefix in new[] { "Depart_", "Unlock_" })
                {
                    var rt = (RectTransform)hud.Find(prefix + region);
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-20, -20 - row * 84);
                }
                row++;
            }

            // 터치용 탑승 버튼 (버튼이 꺼져도 Update 가 돌도록 부모에 스크립트)
            var holder = UIRect("RideButton", hud, new Vector2(1, 0), new Vector2(-40, 140), new Vector2(320, 90));
            var (btn, label) = UIButton("Button", holder, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 90), Hex(0xea580c), 28);
            var ride = holder.gameObject.AddComponent<RideButton>();
            Set(ride, "vehicles", GameObject.FindWithTag("Player").GetComponent<PlayerVehicles>());
            Set(ride, "button", btn);
            Set(ride, "label", label);
        }

        static void AddPlayerVehicles()
        {
            string path = PrefabDir + "/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var pv = root.GetComponent<PlayerVehicles>() ? root.GetComponent<PlayerVehicles>() : root.AddComponent<PlayerVehicles>();
            Set(pv, "model", root.transform.Find("Model").gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static GameObject BuildCartPrefab(PackageView packagePrefab)
        {
            var root = new GameObject("Cart");
            var col = Box(root, new Vector3(0, 0.6f, 0), new Vector3(1.6f, 1.2f, 2.4f));
            col.isTrigger = true;  // 밀고 다닐 때 플레이어를 막지 않도록

            var model = Node("Model", root.transform, Vector3.zero).transform;
            Prim(PrimitiveType.Cube, "Base", model, new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.15f, 2.4f), metal);
            Prim(PrimitiveType.Cube, "SideL", model, new Vector3(-0.78f, 0.55f, 0), new Vector3(0.06f, 0.4f, 2.3f), metal);
            Prim(PrimitiveType.Cube, "SideR", model, new Vector3(0.78f, 0.55f, 0), new Vector3(0.06f, 0.4f, 2.3f), metal);
            // 손잡이: 미는 사람 쪽(뒤, -Z)
            Cyl("HandleL", model, new Vector3(-0.7f, 1f, -1.15f), 0.04f, 1.4f, metal);
            Cyl("HandleR", model, new Vector3(0.7f, 1f, -1.15f), 0.04f, 1.4f, metal);
            Prim(PrimitiveType.Cube, "HandleBar", model, new Vector3(0, 1.7f, -1.15f), new Vector3(1.48f, 0.08f, 0.08f), metal);
            var wheelMat = Lit("TruckWheel", 0x222222);
            foreach (var x in new[] { -0.8f, 0.8f })
            foreach (var z in new[] { -0.9f, 0.9f })
                Cyl("Wheel", model, new Vector3(x, 0.2f, z), 0.2f, 0.1f, wheelMat).transform.localRotation = Quaternion.Euler(0, 0, 90);

            var stack = Stack("Cargo", root.transform, new Vector3(0, 0.43f, 0.1f), packagePrefab, 2, 2, new Vector2(0.75f, 0.8f), 0.5f);
            Set(root.AddComponent<Cart>(), "cargoStack", stack);
            return SavePrefab(root, "Cart");
        }

        static GameObject BuildForkliftPrefab()
        {
            var root = new GameObject("Forklift");
            Box(root, new Vector3(0, 1f, 0.4f), new Vector3(1.8f, 2f, 3.6f));

            // 지게발이 앞(+Z)
            var model = Node("Model", root.transform, Vector3.zero).transform;
            Prim(PrimitiveType.Cube, "Body", model, new Vector3(0, 0.9f, -0.2f), new Vector3(1.8f, 1.2f, 2.8f), Lit("ForkliftBody", 0xf39c12));
            var frame = Lit("ForkliftFrame", 0x222222);
            foreach (var x in new[] { -0.75f, 0.75f })
            foreach (var z in new[] { -1.15f, 0.35f })
                Prim(PrimitiveType.Cube, "CagePost", model, new Vector3(x, 2.2f, z), new Vector3(0.08f, 1.5f, 0.08f), frame);
            Prim(PrimitiveType.Cube, "CageRoof", model, new Vector3(0, 2.95f, -0.4f), new Vector3(1.6f, 0.08f, 1.6f), frame);
            Prim(PrimitiveType.Cube, "MastL", model, new Vector3(-0.7f, 1.5f, 1.3f), new Vector3(0.15f, 2.6f, 0.15f), metal);
            Prim(PrimitiveType.Cube, "MastR", model, new Vector3(0.7f, 1.5f, 1.3f), new Vector3(0.15f, 2.6f, 0.15f), metal);
            Prim(PrimitiveType.Cube, "ForkL", model, new Vector3(-0.45f, 0.25f, 2f), new Vector3(0.12f, 0.08f, 1.6f), metal);
            Prim(PrimitiveType.Cube, "ForkR", model, new Vector3(0.45f, 0.25f, 2f), new Vector3(0.12f, 0.08f, 1.6f), metal);
            Prim(PrimitiveType.Cube, "PalletBase", model, new Vector3(0, 0.35f, 2f), new Vector3(1.6f, 0.12f, 1.6f), Lit("PalletWood", 0x8b5a2b));
            var wheelMat = Lit("TruckWheel", 0x222222);
            foreach (var x in new[] { -1f, 1f })
            foreach (var z in new[] { -1.1f, 0.8f })
                Cyl("Wheel", model, new Vector3(x, 0.4f, z), 0.4f, 0.35f, wheelMat).transform.localRotation = Quaternion.Euler(0, 0, 90);

            // 파렛트 화물: 2x2x2 상자 + 랩
            var load = Node("PalletLoad", root.transform, new Vector3(0, 0.41f, 2f));
            var boxes = new Object[8];
            int bi = 0;
            foreach (var y in new[] { 0.24f, 0.72f })
            foreach (var x in new[] { -0.38f, 0.38f })
            foreach (var z in new[] { -0.38f, 0.38f })
                boxes[bi++] = Prim(PrimitiveType.Cube, "Box", load.transform, new Vector3(x, y, z), new Vector3(0.68f, 0.48f, 0.68f), litWhite).GetComponent<Renderer>();
            Prim(PrimitiveType.Cube, "Wrap", load.transform, new Vector3(0, 0.5f, 0), new Vector3(1.5f, 0.98f, 1.5f), UnlitTransparent("PalletWrap", 0xffffff, 0.28f));

            var label = Label("StatusLabel", root.transform, new Vector3(0, 3.8f, 0), 4.5f, "지게차");
            var fork = root.AddComponent<Forklift>();
            Set(fork, "palletLoad", load);
            Set(fork, "palletBoxes", boxes);
            Set(fork, "statusLabel", label);
            return SavePrefab(root, "Forklift");
        }
    }
}
