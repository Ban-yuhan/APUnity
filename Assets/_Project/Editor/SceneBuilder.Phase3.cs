using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace APProto.EditorTools
{
    // 3단계: 업그레이드 (UpgradeManager, 메가허브 확장부, 입고/출고 선반 증축, 컨베이어, 업그레이드 창)
    public static partial class SceneBuilder
    {
        [MenuItem("APProto/Add Phase3 (업그레이드) to Main Scene")]
        public static void AddPhase3()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LoadShared();
            BuildPhase3Content(AssetDatabase.LoadAssetAtPath<PackageView>(PrefabDir + "/Package.prefab"));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] phase3 done");
        }

        static void DestroyChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t) Object.DestroyImmediate(t.gameObject);
        }

        static void BuildPhase3Content(PackageView packagePrefab)
        {
            foreach (var name in new[] { "UpgradeManager", "UpgradePanel", "UpgradeButton" })
            {
                var old = GameObject.Find(name);
                if (old) Object.DestroyImmediate(old);
            }

            // 메가허브 확장 하역장 (X = 21 ~ 33)
            var env = GameObject.Find("Environment").transform;
            DestroyChild(env, "MegahubExtension");
            var mega = Node("MegahubExtension", env, Vector3.zero);
            Prim(PrimitiveType.Plane, "Floor", mega.transform, W(27, 0.005f, 0), new Vector3(1.2f, 1, 2.8f), Lit("MegahubFloor", 0x2c524b), true);
            var wallMat = Lit("Wall", 0x7f8c8d);
            Prim(PrimitiveType.Cube, "Wall_North", mega.transform, W(27, 3, -14), new Vector3(12, 6, 0.6f), wallMat, true);
            Prim(PrimitiveType.Cube, "Wall_South", mega.transform, W(27, 3, 14), new Vector3(12, 6, 0.6f), wallMat, true);
            mega.SetActive(false);

            var umGo = new GameObject("UpgradeManager");
            umGo.transform.SetSiblingIndex(1);
            Set(umGo.AddComponent<UpgradeManager>(), "megahubExtension", mega);

            BuildDockExpansion(packagePrefab);
            foreach (var line in Object.FindObjectsByType<RegionLine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                BuildLineExpansion(line);

            BuildUpgradePanel(GameObject.Find("HUD").transform);

            var cam = Camera.main.gameObject;
            var follow = cam.GetComponent<CameraFollow>() ? cam.GetComponent<CameraFollow>() : cam.AddComponent<CameraFollow>();
            Set(follow, "target", GameObject.FindWithTag("Player").transform);
        }

        // 입고 선반: 1호기 2단 + 2호기(남쪽 2.8m) 1·2단
        static void BuildDockExpansion(PackageView packagePrefab)
        {
            var dock = Object.FindAnyObjectByType<InboundDock>();
            var model = dock.transform.Find("Model");
            DestroyChild(model, "Shelf2");
            DestroyChild(dock.transform, "Rack2");

            var shelf2 = Prim(PrimitiveType.Cube, "Shelf2", model, new Vector3(0, 1.3f, 0), new Vector3(4, 0.15f, 2.2f), metal);

            var rack2 = Node("Rack2", dock.transform, W(0, 0, 2.8f));
            Box(rack2, new Vector3(0, 1.1f, 0), new Vector3(4, 2.2f, 2.2f));
            Prim(PrimitiveType.Cube, "Shelf1", rack2.transform, new Vector3(0, 0.3f, 0), new Vector3(4, 0.15f, 2.2f), metal);
            var r2Shelf2 = Prim(PrimitiveType.Cube, "Shelf2", rack2.transform, new Vector3(0, 1.3f, 0), new Vector3(4, 0.15f, 2.2f), metal);
            foreach (var x in new[] { -1.9f, 1.9f })
            foreach (var z in new[] { -1f, 1f })
                Cyl("Leg", rack2.transform, new Vector3(x, 1.1f, z), 0.08f, 2.2f, metal);
            var r2Stack = Stack("Packages", rack2.transform, new Vector3(0, 0.375f, 0), packagePrefab, 5, 2, new Vector2(0.75f, 0.8f), 1f);

            Set(dock, "shelf2", shelf2);
            Set(dock, "rack2", rack2);
            Set(dock, "rack2Shelf2", r2Shelf2);
            Set(dock, "rack2Stack", r2Stack);
            shelf2.SetActive(false);
            rack2.SetActive(false);
        }

        static void BuildLineExpansion(RegionLine line)
        {
            // 출고 선반: 4단까지 + 높이 1 기준 기둥 묶음
            var rack = line.GetComponentInChildren<OutboundRack>(true);
            DestroyChild(rack.transform, "Model");
            var model = Node("Model", rack.transform, Vector3.zero).transform;
            var shelves = new Object[4];
            for (int i = 0; i < 4; i++)
                shelves[i] = Prim(PrimitiveType.Cube, $"Shelf{i + 1}", model, new Vector3(0, 0.25f + i * 0.95f, 0), new Vector3(4.2f, 0.15f, 2.2f), metal);
            var legs = Node("Legs", model, Vector3.zero).transform;
            var legRenderers = new Object[4];
            int li = 0;
            foreach (var x in new[] { -2f, 2f })
            foreach (var z in new[] { -1f, 1f })
                legRenderers[li++] = Cyl("Leg", legs, new Vector3(x, 0.5f, z), 0.08f, 1f, litWhite).GetComponent<Renderer>();
            legs.localScale = new Vector3(1, 2.25f, 1);
            var tint = model.gameObject.AddComponent<RegionTint>();
            Set(tint, "config", config);
            Set(tint, "renderers", legRenderers);
            Set(rack, "shelves", shelves);
            Set(rack, "legs", legs);

            // 컨베이어: 분류대 오른쪽 끝 -> 출고 선반 왼쪽 끝, 라인 남쪽 가장자리
            DestroyChild(line.transform, "Conveyor");
            var tableZ = line.GetComponentInChildren<SortingTable>(true).transform.position.z;
            var conv = Node("Conveyor", line.transform, Vector3.zero);
            conv.transform.position = new Vector3(5.75f, 0, tableZ - 1.7f);
            Prim(PrimitiveType.Cube, "Belt", conv.transform, new Vector3(0, 0.6f, 0), new Vector3(6.3f, 0.12f, 0.7f), Lit("ConveyorBelt", 0x2c3e50));
            var rollerMat = Lit("ConveyorRoller", 0x95a5a6);
            Prim(PrimitiveType.Cube, "RailN", conv.transform, new Vector3(0, 0.62f, 0.38f), new Vector3(6.3f, 0.1f, 0.06f), rollerMat);
            Prim(PrimitiveType.Cube, "RailS", conv.transform, new Vector3(0, 0.62f, -0.38f), new Vector3(6.3f, 0.1f, 0.06f), rollerMat);
            foreach (var x in new[] { -2.9f, 0f, 2.9f })
                Cyl("Leg", conv.transform, new Vector3(x, 0.27f, 0), 0.06f, 0.54f, rollerMat);
            conv.AddComponent<Conveyor>();
            conv.SetActive(false);

            Set(line, "conveyor", conv);
            Set(line, "truckLoadPad", line.transform.Find("Pad_TruckLoad"));
        }

        static void BuildUpgradePanel(Transform canvas)
        {
            var (open, openLabel) = UIButton("UpgradeButton", canvas, new Vector2(0.5f, 0), new Vector2(320, 24), new Vector2(300, 80), Hex(0x7c3aed), 30);
            openLabel.text = "업그레이드";

            var panelGo = new GameObject("UpgradePanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvas, false);
            Stretch((RectTransform)panelGo.transform);
            var dim = UIPanel("Window", panelGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.55f));
            Stretch(dim.rectTransform);
            var box = UIPanel("Box", dim.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 860), new Color(0.06f, 0.09f, 0.16f, 0.97f)).transform;

            var title = UIText("Title", box, new Vector2(0, 1), new Vector2(30, -20), new Vector2(600, 50), 38, TextAlignmentOptions.Left);
            title.text = "업그레이드";
            var (close, closeLabel) = UIButton("CloseButton", box, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(140, 56), Hex(0x475569), 26);
            closeLabel.text = "닫기";
            var (test, testLabel) = UIButton("TestMoneyButton", box, new Vector2(1, 1), new Vector2(-180, -20), new Vector2(300, 56), Hex(0xa16207), 22);
            testLabel.text = $"테스트 지원금 +{config.testMoneyAmount:N0}원";
            var summary = UIText("Summary", box, new Vector2(0, 1), new Vector2(30, -74), new Vector2(1100, 36), 24, TextAlignmentOptions.Left);
            summary.color = Hex(0xfacc15);

            var panel = panelGo.AddComponent<UpgradePanel>();
            var so = new SerializedObject(panel);
            var types = new[]
            {
                UpgradeType.PlayerSpeed, UpgradeType.PlayerCapacity, UpgradeType.PlayerWorkSpeed, UpgradeType.WarehouseTier,
                UpgradeType.TruckInterval, UpgradeType.DockCapacity, UpgradeType.RackCapacity,
            };
            var cardsProp = so.FindProperty("cards");
            cardsProp.arraySize = types.Length;
            var top = new Vector2(0.5f, 1);
            for (int i = 0; i < types.Length; i++)
            {
                int row = i / 4, col = i % 4;
                int perRow = row == 0 ? 4 : 3;
                float x = (col - (perRow - 1) * 0.5f) * 335;
                var card = UIPanel($"Card_{types[i]}", box, top, new Vector2(x, -125 - row * 360), new Vector2(320, 340),
                    types[i] == UpgradeType.WarehouseTier ? new Color(0.49f, 0.23f, 0.93f, 0.18f) : new Color(1, 1, 1, 0.07f)).transform;
                var cTitle = UIText("Title", card, top, new Vector2(0, -14), new Vector2(296, 70), 24, TextAlignmentOptions.Center);
                cTitle.textWrappingMode = TextWrappingModes.Normal;
                var cBody = UIText("Body", card, top, new Vector2(0, -90), new Vector2(296, 170), 19, TextAlignmentOptions.TopLeft);
                cBody.textWrappingMode = TextWrappingModes.Normal;
                var (btn, btnLabel) = UIButton("Button", card, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(290, 56), Hex(0x16a34a), 22);
                var e = cardsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").enumValueIndex = (int)types[i];
                e.FindPropertyRelative("title").objectReferenceValue = cTitle;
                e.FindPropertyRelative("body").objectReferenceValue = cBody;
                e.FindPropertyRelative("button").objectReferenceValue = btn;
                e.FindPropertyRelative("buttonLabel").objectReferenceValue = btnLabel;
            }

            so.FindProperty("window").objectReferenceValue = dim.gameObject;
            so.FindProperty("openButton").objectReferenceValue = open;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("testMoneyButton").objectReferenceValue = test;
            so.FindProperty("summaryText").objectReferenceValue = summary;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
