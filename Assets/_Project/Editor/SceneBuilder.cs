using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace APProto.EditorTools
{
    // 씬/프리팹 생성기. 한 번 실행해 에셋을 만든 뒤에는 씬과 프리팹을 직접 편집하면 됨.
    // Rebuild 는 Main 씬과 프리팹을 통째로 덮어쓰므로 주의. 단계별 Add 메뉴는 기존 씬에 해당 단계 오브젝트만 (재)추가.
    // 좌표: 원본 three.js 는 북쪽이 -Z, 여기서는 북쪽이 +Z (z 부호 반전).
    public static partial class SceneBuilder
    {
        const string Root = "Assets/_Project";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string FontPath = Root + "/Fonts/NanumGothic SDF.asset";

        static GameConfig config;
        static TMP_FontAsset font;
        static Material outlineMat;
        static Material litWhite, metal, padMat;

        // 전제: Assets/TextMesh Pro (TMP Essential Resources) 가 임포트되어 있어야 함
        [MenuItem("APProto/Rebuild Main Scene (전체 덮어쓰기)")]
        public static void Build()
        {
            foreach (var d in new[] { MatDir, PrefabDir, Root + "/Scenes", Root + "/Data" })
                Directory.CreateDirectory(d);

            // 새 씬을 먼저 연다 (나중에 열면 미사용 에셋 언로드로 프리팹 참조가 끊김)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LoadShared();

            var packagePrefab = BuildPackagePrefab();
            var playerPrefab = BuildPlayerPrefab(packagePrefab);
            var inboundTruckPrefab = BuildTruckPrefab("InboundTruck", false);
            var outboundTruckPrefab = BuildTruckPrefab("OutboundTruck", true);

            BuildScene(packagePrefab, playerPrefab, inboundTruckPrefab, outboundTruckPrefab);
            BuildPhase2Content(packagePrefab);
            BuildPhase3Content(packagePrefab);
            BuildPhase4Content(packagePrefab);
            BuildPhase5Content();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] rebuild done");
        }

        static void LoadShared()
        {
            config = LoadOrCreate<GameConfig>(Root + "/Data/GameConfig.asset");
            CreateFont();
            CreateMaterials();
        }

        // ---------------------------------------------------------------- assets

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void CreateFont()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (!font)
            {
                var ttf = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NanumGothic.ttf");
                font = TMP_FontAsset.CreateFontAsset(ttf);
                font.name = "NanumGothic SDF";
                AssetDatabase.CreateAsset(font, FontPath);
                font.material.name = "NanumGothic SDF Material";
                font.atlasTextures[0].name = "NanumGothic SDF Atlas";
                AssetDatabase.AddObjectToAsset(font.material, font);
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            }

            outlineMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Fonts/NanumGothic SDF Outline.mat");
            if (!outlineMat)
            {
                outlineMat = new Material(font.material) { name = "NanumGothic SDF Outline" };
                outlineMat.EnableKeyword("OUTLINE_ON");
                outlineMat.SetFloat("_OutlineWidth", 0.25f);
                outlineMat.SetColor("_OutlineColor", new Color(0.06f, 0.09f, 0.16f));
                outlineMat.SetFloat("_FaceDilate", 0.2f);
                AssetDatabase.CreateAsset(outlineMat, Root + "/Fonts/NanumGothic SDF Outline.mat");
            }

            // 새로 만드는 TMP 텍스트 기본 폰트도 한글 폰트로
            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Lit(string name, uint hex)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", Hex(hex));
            mat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material UnlitTransparent(string name, uint hex, float alpha)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var c = Hex(hex);
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void CreateMaterials()
        {
            litWhite = Lit("White", 0xffffff);
            metal = Lit("Metal", 0x7f8c8d);
            padMat = UnlitTransparent("Pad", 0xffffff, 0.45f);
        }

        // ---------------------------------------------------------------- helpers

        static Color Hex(uint hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);

        // 원본(three.js) 월드 좌표 -> Unity (z 반전)
        static Vector3 W(float x, float y, float z) => new(x, y, -z);

        static GameObject Node(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat) =>
            Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2, height / 2, radius * 2), mat);

        static void Set(Object target, string prop, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case Region r: p.enumValueIndex = (int)r; break;
                case PadType t: p.enumValueIndex = (int)t; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case Color c: p.colorValue = c; break;
                case Vector2 v: p.vector2Value = v; break;
                case Object[] arr:
                    p.arraySize = arr.Length;
                    for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static BoxCollider Box(GameObject go, Vector3 center, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            return c;
        }

        // 카메라를 향하는 월드 라벨
        static TextMeshPro Label(string name, Transform parent, Vector3 localPos, float size, string text)
        {
            var go = Node(name, parent, localPos);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSharedMaterial = outlineMat;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(8, 1.5f);
            tmp.text = text;
            go.AddComponent<Billboard>();
            return tmp;
        }

        static PackageStack Stack(string name, Transform parent, Vector3 localPos, PackageView prefab, int cols, int rows, Vector2 spacing, float layerHeight)
        {
            var stack = Node(name, parent, localPos).AddComponent<PackageStack>();
            Set(stack, "packagePrefab", prefab);
            Set(stack, "columns", cols);
            Set(stack, "rows", rows);
            Set(stack, "spacing", spacing);
            Set(stack, "layerHeight", layerHeight);
            return stack;
        }

        static Pad MakePad(Transform parent, string name, PadType type, MonoBehaviour station, Vector3 worldPos, float radius, Color color, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;

            var disc = Prim(PrimitiveType.Cylinder, "Disc", go.transform, new Vector3(0, 0.02f, 0), Vector3.one, padMat);

            var canvasGo = Node("Progress", go.transform, new Vector3(0, 0.035f, 0));
            canvasGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasGo.transform).sizeDelta = Vector2.one;
            var imgGo = Node("Fill", canvasGo.transform, Vector3.zero);
            var img = imgGo.AddComponent<Image>();
            img.rectTransform.sizeDelta = Vector2.one;
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.color = new Color(0f, 1f, 0.53f, 0.9f);
            img.raycastTarget = false;

            var text = Node("Label", go.transform, new Vector3(0, 0.05f, -radius - 0.35f));
            text.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var tmp = text.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSharedMaterial = outlineMat;
            tmp.fontSize = 5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(4, 0.6f);

            var pad = go.AddComponent<Pad>();
            Set(pad, "type", type);
            Set(pad, "station", station);
            Set(pad, "radius", radius);
            Set(pad, "color", color);
            Set(pad, "label", label);
            Set(pad, "disc", disc.GetComponent<Renderer>());
            Set(pad, "progressImage", img);
            Set(pad, "labelText", tmp);
            pad.ApplyVisual();
            return pad;
        }

        static GameObject SavePrefab(GameObject temp, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // ---------------------------------------------------------------- prefabs

        static PackageView BuildPackagePrefab()
        {
            var root = new GameObject("Package");
            var model = Node("Model", root.transform, Vector3.zero).transform;
            var body = Prim(PrimitiveType.Cube, "Body", model, new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.5f, 0.7f), litWhite);
            var waybill = Prim(PrimitiveType.Cube, "Waybill", model, new Vector3(0, 0.505f, 0), new Vector3(0.35f, 0.01f, 0.25f), litWhite);
            var tape = Node("InspectedTape", model, Vector3.zero).transform;
            var tapeMat = Lit("Tape", 0xfacc15);
            Prim(PrimitiveType.Cube, "BandX", tape, new Vector3(0, 0.25f, 0), new Vector3(0.72f, 0.515f, 0.12f), tapeMat);
            Prim(PrimitiveType.Cube, "BandZ", tape, new Vector3(0, 0.25f, 0), new Vector3(0.12f, 0.515f, 0.72f), tapeMat);
            Prim(PrimitiveType.Cylinder, "Seal", tape, new Vector3(0, 0.515f, 0), new Vector3(0.24f, 0.005f, 0.24f), Lit("Seal", 0x10b981));

            var view = root.AddComponent<PackageView>();
            Set(view, "body", body.GetComponent<Renderer>());
            Set(view, "inspectedTape", tape.gameObject);
            Set(view, "waybill", waybill);
            return SavePrefab(root, "Package").GetComponent<PackageView>();
        }

        static GameObject BuildPlayerPrefab(PackageView packagePrefab)
        {
            var root = new GameObject("Player");
            root.tag = "Player";
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1, 0);
            cc.height = 2;
            cc.radius = 0.45f;

            var model = Node("Model", root.transform, Vector3.zero).transform;
            Cyl("Body", model, new Vector3(0, 0.95f, 0), 0.43f, 1.1f, Lit("PlayerUniform", 0x2980b9));
            Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0, 1.75f, 0), Vector3.one * 0.7f, Lit("Skin", 0xffdbac));
            Cyl("Helmet", model, new Vector3(0, 1.95f, 0), 0.42f, 0.2f, Lit("Helmet", 0xf1c40f));
            var legMat = Lit("Pants", 0x2c3e50);
            Cyl("LeftLeg", model, new Vector3(-0.2f, 0.35f, 0), 0.14f, 0.7f, legMat);
            Cyl("RightLeg", model, new Vector3(0.2f, 0.35f, 0), 0.14f, 0.7f, legMat);

            var hand = Stack("HandAnchor", root.transform, new Vector3(0, 1.2f, 0.45f), packagePrefab, 1, 1, Vector2.zero, 0.5f);

            var carrier = root.AddComponent<Carrier>();
            Set(carrier, "handStack", hand);
            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerInteractor>();
            return SavePrefab(root, "Player");
        }

        static GameObject BuildTruckPrefab(string name, bool outbound)
        {
            var root = new GameObject(name);
            Box(root, new Vector3(0, 1.9f, 0), new Vector3(3.6f, 3.8f, 7.5f));

            var model = Node("Model", root.transform, Vector3.zero).transform;
            Prim(PrimitiveType.Cube, "Chassis", model, new Vector3(0, 0.8f, 0), new Vector3(3.6f, 0.4f, 7.5f), Lit("TruckChassis", 0x333333));
            var cabin = Prim(PrimitiveType.Cube, "Cabin", model, new Vector3(0, 2.1f, 2.4f), new Vector3(3.4f, 2.4f, 2.2f), outbound ? litWhite : Lit("TruckCabin", 0xdddddd));
            Prim(PrimitiveType.Cube, "Windshield", model, new Vector3(0, 2.4f, 3.51f), new Vector3(2.8f, 1.2f, 0.02f), Lit("TruckWindow", 0x88c0d0));
            var cargo = Prim(PrimitiveType.Cube, "CargoBox", model, new Vector3(0, 2.3f, -1.2f), new Vector3(3.5f, 2.8f, 4.8f), outbound ? litWhite : Lit("InboundTruckBody", 0x2b5c8f));
            var wheelMat = Lit("TruckWheel", 0x222222);
            foreach (var z in new[] { 2.4f, -1.8f, -2.7f })
            foreach (var x in new[] { -1.85f, 1.85f })
            {
                var w = Cyl("Wheel", model, new Vector3(x, 0.65f, z), 0.65f, 0.5f, wheelMat);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            var lightMat = Lit("Headlight", 0xffffaa);
            Prim(PrimitiveType.Cube, "HeadlightL", model, new Vector3(-1.2f, 1.4f, 3.51f), new Vector3(0.5f, 0.3f, 0.1f), lightMat);
            Prim(PrimitiveType.Cube, "HeadlightR", model, new Vector3(1.2f, 1.4f, 3.51f), new Vector3(0.5f, 0.3f, 0.1f), lightMat);

            var label = Label("StatusLabel", root.transform, new Vector3(0, 4.8f, 0), 7f, name);

            if (outbound)
            {
                var tint = model.gameObject.AddComponent<RegionTint>();
                Set(tint, "config", config);
                Set(tint, "renderers", new Object[] { cabin.GetComponent<Renderer>(), cargo.GetComponent<Renderer>() });
                Set(root.AddComponent<OutboundTruck>(), "statusLabel", label);
            }
            else
            {
                Set(root.AddComponent<InboundTruck>(), "statusLabel", label);
            }
            return SavePrefab(root, name);
        }

        // ---------------------------------------------------------------- scene

        static void BuildScene(PackageView packagePrefab, GameObject playerPrefab, GameObject inboundTruckPrefab, GameObject outboundTruckPrefab)
        {
            // 카메라 / 조명 / 안개 (원본 initThree, initWorldEnvironment)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex(0xdce7f0);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = W(0, 32, 28);
            camGo.transform.LookAt(Vector3.zero);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex(0xfff5e6);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.LookRotation(-W(30, 50, 20));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.005f;  // 원본 0.012 는 Unity 에서 너무 뿌옇게 보임
            RenderSettings.fogColor = Hex(0xdce7f0);

            // 바닥 / 벽 (가로 42 x 세로 28 창고)
            var env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Plane, "OutdoorFloor", env, new Vector3(0, -0.05f, 0), new Vector3(12, 1, 9), Lit("Asphalt", 0x2b303a), true);
            Prim(PrimitiveType.Plane, "IndoorFloor", env, Vector3.zero, new Vector3(4.2f, 1, 2.8f), Lit("Epoxy", 0x3d7068), true);
            var wallMat = Lit("Wall", 0x7f8c8d);
            Prim(PrimitiveType.Cube, "Wall_North", env, W(0, 3, -14), new Vector3(42, 6, 0.6f), wallMat, true);
            Prim(PrimitiveType.Cube, "Wall_South", env, W(0, 3, 14), new Vector3(42, 6, 0.6f), wallMat, true);
            Prim(PrimitiveType.Cube, "Wall_West_N", env, W(-21, 3, -9.5f), new Vector3(0.6f, 6, 9), wallMat, true);
            Prim(PrimitiveType.Cube, "Wall_West_S", env, W(-21, 3, 9.5f), new Vector3(0.6f, 6, 9), wallMat, true);

            // 입고
            var inbound = new GameObject("Inbound").transform;
            var inboundTruck = ((GameObject)PrefabUtility.InstantiatePrefab(inboundTruckPrefab, inbound)).GetComponent<InboundTruck>();
            inboundTruck.transform.SetPositionAndRotation(W(-28, 0, 0), Quaternion.Euler(0, -90, 0));
            MakePad(inbound, "Pad_TruckUnload", PadType.TruckUnload, inboundTruck, W(-24, 0, 0), 1.4f, Hex(0xffaa00), "하차");

            var dock = BuildInboundDock(inbound, packagePrefab);
            MakePad(inbound, "Pad_DockPut", PadType.DockPut, dock, W(-18.5f, 0, 0), 1.2f, Hex(0x2ecc71), "선반 올리기");
            MakePad(inbound, "Pad_Scan", PadType.ScanAction, dock, W(-15, 0, -2.6f), 1.2f, Hex(0xe74c3c), "송장 스캔");
            MakePad(inbound, "Pad_DockTake", PadType.DockTake, dock, W(-11.5f, 0, 0), 1.2f, Hex(0x3498db), "스캔품 들기");

            // 지역 라인 3개
            var lines = new[]
            {
                BuildRegionLine(Region.Daegu, -7.5f, true, packagePrefab, outboundTruckPrefab),
                BuildRegionLine(Region.Busan, 0f, false, packagePrefab, outboundTruckPrefab),
                BuildRegionLine(Region.Seoul, 7.5f, false, packagePrefab, outboundTruckPrefab),
            };

            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetAsFirstSibling();
            var gm = gmGo.AddComponent<GameManager>();
            Set(gm, "config", config);
            Set(gm, "inboundTruck", inboundTruck);
            Set(gm, "inboundDock", dock);
            Set(gm, "regionLines", lines);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = W(-16, 0, 0);

            BuildHud(player, lines);
        }

        static InboundDock BuildInboundDock(Transform parent, PackageView packagePrefab)
        {
            var go = Node("InboundDock", parent, W(-15, 0, 0));
            Box(go, new Vector3(0, 1.1f, 0), new Vector3(4, 2.2f, 2.2f));
            var model = Node("Model", go.transform, Vector3.zero).transform;
            Prim(PrimitiveType.Cube, "Shelf", model, new Vector3(0, 0.3f, 0), new Vector3(4, 0.15f, 2.2f), metal);
            foreach (var x in new[] { -1.9f, 1.9f })
            foreach (var z in new[] { -1f, 1f })
                Cyl("Leg", model, new Vector3(x, 1.1f, z), 0.08f, 2.2f, metal);
            Cyl("ArchPoleL", model, new Vector3(-1.9f, 2.4f, 0), 0.06f, 2.4f, metal);
            Cyl("ArchPoleR", model, new Vector3(1.9f, 2.4f, 0), 0.06f, 2.4f, metal);
            Prim(PrimitiveType.Cube, "ArchTop", model, new Vector3(0, 3.6f, 0), new Vector3(4.2f, 0.12f, 0.12f), metal);
            Prim(PrimitiveType.Cube, "Scanner", model, new Vector3(0, 3.5f, 0), new Vector3(1.2f, 0.25f, 0.5f), Lit("Scanner", 0xe74c3c));

            var stack = Stack("Packages", go.transform, new Vector3(0, 0.375f, 0), packagePrefab, 5, 2, new Vector2(0.75f, 0.8f), 1f);
            var label = Label("CountLabel", go.transform, new Vector3(0, 4.4f, 0), 7f, "입고&스캔");

            var dock = go.AddComponent<InboundDock>();
            Set(dock, "stack", stack);
            Set(dock, "countLabel", label);
            return dock;
        }

        static RegionLine BuildRegionLine(Region region, float threeZ, bool unlocked, PackageView packagePrefab, GameObject truckPrefab)
        {
            var data = config.GetRegion(region);
            var lineGo = new GameObject($"Region_{region}");
            var line = lineGo.AddComponent<RegionLine>();
            Set(line, "region", region);
            var t = lineGo.transform;

            // 분류대 (X=0)
            var tableGo = Node("SortingTable", t, W(0, 0, threeZ));
            Box(tableGo, new Vector3(0, 0.425f, 0), new Vector3(5.2f, 0.85f, 2.6f));
            var tm = Node("Model", tableGo.transform, Vector3.zero).transform;
            Prim(PrimitiveType.Cube, "Base", tm, new Vector3(0, 0.425f, 0), new Vector3(5.2f, 0.85f, 2.6f), Lit("SortingTable", 0x34495e));
            Prim(PrimitiveType.Cube, "Divider", tm, new Vector3(0, 0.93f, 0), new Vector3(0.15f, 0.15f, 2.7f), Lit("Divider", 0xf1c40f));
            var areaMat = UnlitTransparent("RegionArea", 0xffffff, 0.4f);
            var left = Prim(PrimitiveType.Cube, "UninspectedArea", tm, new Vector3(-1.3f, 0.855f, 0), new Vector3(2.29f, 0.01f, 2.34f), areaMat);
            var right = Prim(PrimitiveType.Cube, "InspectedArea", tm, new Vector3(1.3f, 0.855f, 0), new Vector3(2.29f, 0.01f, 2.34f), areaMat);
            var areaTint = tm.gameObject.AddComponent<RegionTint>();
            Set(areaTint, "config", config);
            Set(areaTint, "renderers", new Object[] { left.GetComponent<Renderer>(), right.GetComponent<Renderer>() });
            Set(areaTint, "alpha", 0.45f);
            Label("Sign", tableGo.transform, new Vector3(0, 2.3f, 1.5f), 9f, data.displayName);

            var table = tableGo.AddComponent<SortingTable>();
            Set(table, "uninspectedStack", Stack("Uninspected", tableGo.transform, new Vector3(-1.3f, 0.86f, 0), packagePrefab, 2, 2, new Vector2(0.8f, 0.8f), 0.52f));
            Set(table, "inspectedStack", Stack("Inspected", tableGo.transform, new Vector3(1.3f, 0.86f, 0), packagePrefab, 2, 2, new Vector2(0.8f, 0.8f), 0.52f));

            MakePad(t, "Pad_SortPut", PadType.SortPut, table, W(-3.2f, 0, threeZ), 1.15f, data.color, $"{data.displayName} 적재");
            MakePad(t, "Pad_Inspect", PadType.SortInspect, table, W(0, 0, threeZ - 2f), 1.2f, Hex(0x9b59b6), "검수");
            MakePad(t, "Pad_SortTake", PadType.SortTake, table, W(3.2f, 0, threeZ), 1.15f, Hex(0x2ecc71), "출고용 들기");

            // 출고 선반 (X=11), 2단 = 20개
            var rackGo = Node("OutboundRack", t, W(11, 0, threeZ));
            Box(rackGo, new Vector3(0, 1.125f, 0), new Vector3(4.2f, 2.25f, 2.2f));
            var rm = Node("Model", rackGo.transform, Vector3.zero).transform;
            Prim(PrimitiveType.Cube, "Shelf1", rm, new Vector3(0, 0.25f, 0), new Vector3(4.2f, 0.15f, 2.2f), metal);
            Prim(PrimitiveType.Cube, "Shelf2", rm, new Vector3(0, 1.2f, 0), new Vector3(4.2f, 0.15f, 2.2f), metal);
            var legs = new Object[4];
            int li = 0;
            foreach (var x in new[] { -2f, 2f })
            foreach (var z in new[] { -1f, 1f })
                legs[li++] = Cyl("Leg", rm, new Vector3(x, 1.125f, z), 0.08f, 2.25f, litWhite).GetComponent<Renderer>();
            var legTint = rm.gameObject.AddComponent<RegionTint>();
            Set(legTint, "config", config);
            Set(legTint, "renderers", legs);

            var rack = rackGo.AddComponent<OutboundRack>();
            Set(rack, "stack", Stack("Packages", rackGo.transform, new Vector3(0, 0.325f, 0), packagePrefab, 5, 2, new Vector2(0.75f, 0.8f), 0.95f));
            Set(rack, "countLabel", Label("CountLabel", rackGo.transform, new Vector3(0, 3f, 0), 6f, "출고 선반"));

            MakePad(t, "Pad_OutboundPut", PadType.OutboundPut, rack, W(8.6f, 0, threeZ), 1.25f, data.color, $"{data.displayName} 도크 두기");
            MakePad(t, "Pad_OutboundTake", PadType.OutboundTake, rack, W(13.4f, 0, threeZ), 1.25f, Hex(0x38bdf8), $"{data.displayName} 도크 꺼내기");

            // 출고 트럭 (X=20.5, 동쪽을 바라봄)
            var truck = ((GameObject)PrefabUtility.InstantiatePrefab(truckPrefab, t)).GetComponent<OutboundTruck>();
            truck.name = $"OutboundTruck_{region}";
            truck.transform.SetPositionAndRotation(W(20.5f, 0, threeZ), Quaternion.Euler(0, 90, 0));
            Set(line, "truck", truck);
            MakePad(t, "Pad_TruckLoad", PadType.TruckLoad, truck, W(16.5f, 0, threeZ), 1.3f, Hex(0x2ecc71), $"{data.displayName} 트럭 상차");

            lineGo.SetActive(unlocked);
            return line;
        }

        // ---------------------------------------------------------------- UI

        static TextMeshProUGUI UIText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        static (Button, TextMeshProUGUI) UIButton(string name, Transform parent, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(480, 72);
            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            var btn = go.AddComponent<Button>();
            var text = UIText("Label", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 64), 26, TextAlignmentOptions.Center);
            text.color = Color.white;
            return (btn, text);
        }

        static void BuildHud(GameObject player, RegionLine[] lines)
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var c = canvasGo.transform;

            var panel = new GameObject("StatusPanel", typeof(RectTransform));
            panel.transform.SetParent(c, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(20, -20);
            prt.sizeDelta = new Vector2(340, 150);
            var pimg = panel.AddComponent<Image>();
            pimg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            pimg.type = Image.Type.Sliced;
            pimg.color = new Color(0.06f, 0.09f, 0.16f, 0.85f);
            var tl = new Vector2(0, 1);
            var money = UIText("Money", panel.transform, tl, new Vector2(20, -12), new Vector2(300, 50), 40, TextAlignmentOptions.Left);
            money.color = Hex(0xfacc15);
            var rep = UIText("Reputation", panel.transform, tl, new Vector2(20, -64), new Vector2(300, 36), 26, TextAlignmentOptions.Left);
            var carry = UIText("Carry", panel.transform, tl, new Vector2(20, -102), new Vector2(300, 36), 26, TextAlignmentOptions.Left);

            var notice = UIText("Notification", c, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1400, 60), 30, TextAlignmentOptions.Center);
            notice.fontSharedMaterial = outlineMat;

            var hud = canvasGo.AddComponent<HUD>();
            Set(hud, "moneyText", money);
            Set(hud, "reputationText", rep);
            Set(hud, "carryText", carry);
            Set(hud, "playerCarrier", player.GetComponent<Carrier>());
            Set(hud, "notificationText", notice);

            var so = new SerializedObject(hud);
            var arr = so.FindProperty("regions");
            arr.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var region = lines[i].Region;
                float y = 100 - i * 100;
                var (depart, departLabel) = UIButton($"Depart_{region}", c, new Vector2(-20, y), Hex(0x16a34a));
                var (unlock, unlockLabel) = UIButton($"Unlock_{region}", c, new Vector2(-20, y), Hex(0x475569));
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("region").enumValueIndex = (int)region;
                e.FindPropertyRelative("departButton").objectReferenceValue = depart;
                e.FindPropertyRelative("departLabel").objectReferenceValue = departLabel;
                e.FindPropertyRelative("unlockButton").objectReferenceValue = unlock;
                e.FindPropertyRelative("unlockLabel").objectReferenceValue = unlockLabel;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 가상 조이스틱 (드래그 시에만 표시)
            var knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform JoyImage(string name, float size, float alpha)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(c, false);
                var img = go.AddComponent<Image>();
                img.sprite = knobSprite;
                img.color = new Color(1, 1, 1, alpha);
                img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = Vector2.one * size;
                go.SetActive(false);
                return rt;
            }
            var pc = player.GetComponent<PlayerController>();
            Set(pc, "joystickBase", JoyImage("JoystickBase", 110, 0.25f));
            Set(pc, "joystickKnob", JoyImage("JoystickKnob", 52, 0.8f));

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
