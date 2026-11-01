using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace APProto.EditorTools
{
    // 2단계: 알바 시스템 (Worker / WorkerRow 프리팹, WorkerManager, 알바 관리창)
    public static partial class SceneBuilder
    {
        [MenuItem("APProto/Add Phase2 (알바) to Main Scene")]
        public static void AddPhase2()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LoadShared();
            var packagePrefab = AssetDatabase.LoadAssetAtPath<PackageView>(PrefabDir + "/Package.prefab");
            BuildPhase2Content(packagePrefab);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] phase2 done");
        }

        static void BuildPhase2Content(PackageView packagePrefab)
        {
            var workerPrefab = BuildWorkerPrefab(packagePrefab);
            var rowPrefab = BuildWorkerRowPrefab();

            // 이전에 추가한 2단계 오브젝트 제거 후 재생성
            foreach (var name in new[] { "WorkerManager", "WorkerPanel", "WorkerButton" })
            {
                var old = GameObject.Find(name);
                if (old) Object.DestroyImmediate(old);
            }

            var wmGo = new GameObject("WorkerManager");
            wmGo.transform.SetSiblingIndex(1);
            var spawn = Node("SpawnPoint", wmGo.transform, W(-18, 0, 5));
            var wm = wmGo.AddComponent<WorkerManager>();
            Set(wm, "workerPrefab", workerPrefab);
            Set(wm, "spawnPoint", spawn.transform);

            BuildWorkerPanel(GameObject.Find("HUD").transform, rowPrefab);
        }

        static Worker BuildWorkerPrefab(PackageView packagePrefab)
        {
            var root = new GameObject("Worker");
            var col = root.AddComponent<CapsuleCollider>();
            col.isTrigger = true;  // 플레이어 이동을 막지 않도록
            col.center = new Vector3(0, 1, 0);
            col.height = 2;
            col.radius = 0.45f;

            var model = Node("Model", root.transform, Vector3.zero).transform;
            var body = Cyl("Body", model, new Vector3(0, 0.95f, 0), 0.43f, 1.1f, litWhite);
            Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0, 1.75f, 0), Vector3.one * 0.7f, Lit("Skin", 0xffdbac));
            Cyl("Cap", model, new Vector3(0, 1.95f, 0), 0.42f, 0.2f, Lit("WorkerCap", 0x34495e));
            var legMat = Lit("Pants", 0x2c3e50);
            Cyl("LeftLeg", model, new Vector3(-0.2f, 0.35f, 0), 0.14f, 0.7f, legMat);
            Cyl("RightLeg", model, new Vector3(0.2f, 0.35f, 0), 0.14f, 0.7f, legMat);
            // 숙련도 표시 바닥 원판
            var aura = Cyl("Aura", model, new Vector3(0, 0.03f, 0), 0.62f, 0.01f, UnlitTransparent("Aura", 0xffffff, 0.7f));

            var hand = Stack("HandAnchor", root.transform, new Vector3(0, 1.2f, 0.45f), packagePrefab, 1, 1, Vector2.zero, 0.5f);
            var label = Label("NameLabel", root.transform, new Vector3(0, 2.6f, 0), 4f, "알바");

            var carrier = root.AddComponent<Carrier>();
            Set(carrier, "handStack", hand);
            var worker = root.AddComponent<Worker>();
            Set(worker, "uniform", body.GetComponent<Renderer>());
            Set(worker, "aura", aura.GetComponent<Renderer>());
            Set(worker, "nameLabel", label);
            return SavePrefab(root, "Worker").GetComponent<Worker>();
        }

        // ---------------------------------------------------------------- UI helpers

        static RectTransform UIRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image UIPanel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var img = UIRect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        static (Button, TextMeshProUGUI) UIButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, float fontSize)
        {
            var img = UIPanel(name, parent, anchor, pos, size, color);
            var btn = img.gameObject.AddComponent<Button>();
            var text = UIText("Label", img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16, 8), fontSize, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.Normal;
            return (btn, text);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- 알바 관리창

        static WorkerRow BuildWorkerRowPrefab()
        {
            var bg = UIPanel("WorkerRow", null, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 46), new Color(1, 1, 1, 0.06f));
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
            var info = UIText("Info", bg.transform, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(1100, 40), 22, TextAlignmentOptions.Left);
            var (fire, _) = UIButton("FireButton", bg.transform, new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(110, 38), Hex(0xb91c1c), 20);
            fire.GetComponentInChildren<TextMeshProUGUI>().text = "퇴사";

            var row = bg.gameObject.AddComponent<WorkerRow>();
            Set(row, "info", info);
            Set(row, "fireButton", fire);
            return SavePrefab(bg.gameObject, "WorkerRow").GetComponent<WorkerRow>();
        }

        static void BuildWorkerPanel(Transform canvas, WorkerRow rowPrefab)
        {
            var (open, openLabel) = UIButton("WorkerButton", canvas, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(300, 80), Hex(0x2563eb), 30);
            openLabel.text = "알바 관리";

            var panelGo = new GameObject("WorkerPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvas, false);
            Stretch((RectTransform)panelGo.transform);

            // 창 (열려 있는 동안 일시정지)
            var dim = UIPanel("Window", panelGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.55f));
            Stretch(dim.rectTransform);
            var box = UIPanel("Box", dim.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 920), new Color(0.06f, 0.09f, 0.16f, 0.97f)).transform;

            var top = new Vector2(0.5f, 1);
            var title = UIText("Title", box, new Vector2(0, 1), new Vector2(30, -20), new Vector2(600, 50), 38, TextAlignmentOptions.Left);
            title.text = "알바 관리";
            var (close, closeLabel) = UIButton("CloseButton", box, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(140, 56), Hex(0x475569), 26);
            closeLabel.text = "닫기";
            var summary = UIText("Summary", box, new Vector2(0, 1), new Vector2(30, -74), new Vector2(1100, 36), 24, TextAlignmentOptions.Left);
            summary.color = Hex(0xfacc15);

            // 고용 카드 3장
            var panel = panelGo.AddComponent<WorkerPanel>();
            var so = new SerializedObject(panel);
            var cardsProp = so.FindProperty("cards");
            cardsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var card = UIPanel($"Card{i}", box, top, new Vector2(-450 + i * 450, -125), new Vector2(420, 340), new Color(1, 1, 1, 0.07f)).transform;
                var cTitle = UIText("Title", card, top, new Vector2(0, -12), new Vector2(390, 80), 32, TextAlignmentOptions.Center);
                cTitle.textWrappingMode = TextWrappingModes.Normal;
                var cBody = UIText("Body", card, top, new Vector2(0, -96), new Vector2(390, 170), 20, TextAlignmentOptions.TopLeft);
                cBody.textWrappingMode = TextWrappingModes.Normal;
                var (hire, hireLabel) = UIButton("HireButton", card, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(380, 56), Hex(0x16a34a), 26);
                var e = cardsProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("title").objectReferenceValue = cTitle;
                e.FindPropertyRelative("body").objectReferenceValue = cBody;
                e.FindPropertyRelative("hireButton").objectReferenceValue = hire;
                e.FindPropertyRelative("hireLabel").objectReferenceValue = hireLabel;
            }
            var (reroll, rerollLabel) = UIButton("RerollButton", box, top, new Vector2(0, -478), new Vector2(360, 54), Hex(0xd97706), 24);

            // 단기 베테랑 버튼 (직종별)
            var vTitle = UIText("VeteranTitle", box, new Vector2(0, 1), new Vector2(30, -548), new Vector2(900, 36), 24, TextAlignmentOptions.Left);
            vTitle.text = $"단기 베테랑 알바  (계약 {config.veteranDuration:0}초 · 이동/작업 2배속 · 즉시 베테랑 등급)";
            var roles = (WorkerRole[])System.Enum.GetValues(typeof(WorkerRole));
            var vetProp = so.FindProperty("veterans");
            vetProp.arraySize = roles.Length;
            for (int i = 0; i < roles.Length; i++)
            {
                var (btn, label) = UIButton($"Veteran_{roles[i]}", box, top, new Vector2(-560 + i * 280, -590), new Vector2(260, 76), Hex(0xa16207), 22);
                var e = vetProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("role").enumValueIndex = (int)roles[i];
                e.FindPropertyRelative("button").objectReferenceValue = btn;
                e.FindPropertyRelative("label").objectReferenceValue = label;
            }

            // 근무자 목록 (스크롤)
            var listTitle = UIText("ListTitle", box, new Vector2(0, 1), new Vector2(30, -684), new Vector2(600, 36), 24, TextAlignmentOptions.Left);
            listTitle.text = "근무 중인 알바";
            var scrollRt = UIRect("WorkerList", box, top, new Vector2(0, -722), new Vector2(1340, 180));
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30;
            var viewport = UIRect("Viewport", scrollRt, top, Vector2.zero, Vector2.zero);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIRect("Content", viewport, top, Vector2.zero, new Vector2(0, 0));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            var empty = UIText("EmptyText", scrollRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 40), 22, TextAlignmentOptions.Center);
            empty.text = "현재 고용된 알바가 없습니다.";
            empty.color = new Color(1, 1, 1, 0.5f);

            so.FindProperty("window").objectReferenceValue = dim.gameObject;
            so.FindProperty("openButton").objectReferenceValue = open;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("rerollButton").objectReferenceValue = reroll;
            so.FindProperty("rerollLabel").objectReferenceValue = rerollLabel;
            so.FindProperty("summaryText").objectReferenceValue = summary;
            so.FindProperty("listContent").objectReferenceValue = content;
            so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            so.FindProperty("emptyText").objectReferenceValue = empty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
