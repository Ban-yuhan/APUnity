using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace APProto.EditorTools
{
    // 5단계: 주야간 / 기상 악화 / 앙팡데이 피버타임
    public static partial class SceneBuilder
    {
        [MenuItem("APProto/Add Phase5 (주야간·날씨·피버) to Main Scene")]
        public static void AddPhase5()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LoadShared();
            BuildPhase5Content();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] phase5 done");
        }

        static void BuildPhase5Content()
        {
            var hud = GameObject.Find("HUD").transform;
            foreach (var name in new[] { "World", "Emergency" })
            {
                var old = GameObject.Find(name);
                if (old) Object.DestroyImmediate(old);
            }
            // 비활성 자식도 찾도록 Transform.Find
            foreach (var name in new[] { "ClockText", "WeatherText", "QuestPanel", "FeverButton", "LightningFlash" })
                DestroyChild(hud, name);
            if (hud.GetComponent<EventHud>()) Object.DestroyImmediate(hud.GetComponent<EventHud>());

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);

            // 실내 24시간 조명 + 정전 비상등
            var world = new GameObject("World").transform;
            world.SetSiblingIndex(1);
            var lights = Node("IndoorLights", world, Vector3.zero).transform;
            var indoor = new Object[5];
            var lightPos = new[] { W(-15, 12, 0), W(0, 12, -8), W(0, 12, 0), W(0, 12, 8), W(12, 12, 0) };
            for (int i = 0; i < lightPos.Length; i++)
                indoor[i] = PointLight($"Indoor_{i + 1}", lights, lightPos[i], Hex(0xfffaed), 1.1f, 32);
            var emergencyLight = PointLight("EmergencyLight", world, W(0, 10, 0), Hex(0xff1100), 0, 45);

            var cycle = world.gameObject.AddComponent<DayNightCycle>();
            Set(cycle, "sun", GameObject.Find("Directional Light").GetComponent<Light>());
            Set(cycle, "indoorLights", indoor);
            Set(cycle, "emergencyLight", emergencyLight);

            // 긴급 시설 (원본 위치)
            var weather = world.gameObject.AddComponent<WeatherManager>();
            var em = new GameObject("Emergency").transform;
            var red = Hex(0xef4444);

            var snowStation = Node("SnowStation", em, W(-26, 0, -9)).transform;
            var snowMat = Lit("Snow", 0xf8fafc);
            Prim(PrimitiveType.Sphere, "Pile1", snowStation, new Vector3(0, 0.1f, 0), new Vector3(2.4f, 0.7f, 1.8f), snowMat);
            Prim(PrimitiveType.Sphere, "Pile2", snowStation, new Vector3(-0.9f, 0.05f, 0.6f), new Vector3(1.4f, 0.5f, 1.2f), snowMat);
            Prim(PrimitiveType.Cube, "Shovel", snowStation, new Vector3(0.9f, 0.6f, -0.5f), new Vector3(0.08f, 1.2f, 0.08f), Lit("ShovelHandle", 0x8b5a2b));
            var snowPad = MakePad(em, "Pad_Snow", PadType.Emergency, weather, W(-24.5f, 0, -9), 1.3f, red, "제설 작업");

            var generator = Node("Generator", em, W(-18.5f, 0, -11.5f)).transform;
            Prim(PrimitiveType.Cube, "Body", generator, new Vector3(0, 0.8f, 0), new Vector3(2f, 1.6f, 1.2f), Lit("Generator", 0xf59e0b));
            Prim(PrimitiveType.Cube, "Panel", generator, new Vector3(0, 0.9f, -0.61f), new Vector3(1.2f, 0.8f, 0.02f), Lit("GeneratorPanel", 0x334155));
            var beacon = Prim(PrimitiveType.Sphere, "Beacon", generator, new Vector3(0.7f, 1.8f, 0), Vector3.one * 0.35f, litWhite);
            var thunderPad = MakePad(em, "Pad_Generator", PadType.Emergency, weather, W(-18.5f, 0, -9.2f), 1.3f, red, "발전기 가동");

            var rainStation = Node("BarrierStack", em, W(-20.5f, 0, -3.8f)).transform;
            var barrierMat = Lit("FloodBarrier", 0x94a3b8);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Panel", rainStation, new Vector3(0, 0.1f + i * 0.14f, 0), new Vector3(0.9f, 0.12f, 1.6f), barrierMat);
            var rainPad = MakePad(em, "Pad_Rain", PadType.Emergency, weather, W(-20.5f, 0, -2f), 1.3f, red, "차수판 설치");

            foreach (var pad in new[] { snowPad, thunderPad, rainPad })
            {
                Set(pad.gameObject.AddComponent<Blinker>(), "target", pad.transform.Find("Disc").GetComponent<Renderer>());
                pad.gameObject.SetActive(false);
            }

            // 서쪽 출입구 차수판 (설치 완료 시 표시, 통행은 막지 않음)
            var barrier = Prim(PrimitiveType.Cube, "FloodBarrier", em, W(-21, 0.35f, 0), new Vector3(0.3f, 0.7f, 10f), barrierMat);
            barrier.SetActive(false);
            // 제설 필요 시 실외 도로에 쌓인 눈 (X = -52 ~ -21)
            var snowGround = Prim(PrimitiveType.Plane, "SnowGround", em, W(-36.5f, 0.02f, 0), new Vector3(3.1f, 1, 2.8f), snowMat);
            snowGround.SetActive(false);

            Set(weather, "snowPad", snowPad);
            Set(weather, "rainPad", rainPad);
            Set(weather, "thunderPad", thunderPad);
            Set(weather, "snowFx", Particles("SnowFx", world, 0xffffff, 0.25f, 5f, 4f, 300, false));
            Set(weather, "rainFx", Particles("RainFx", world, 0x88c0d0, 0.08f, 25f, 0.8f, 900, true));
            Set(weather, "snowGround", snowGround);
            Set(weather, "floodBarrier", barrier);
            Set(weather, "generatorBeacon", beacon.GetComponent<Renderer>());

            BuildEventHud(hud);
        }

        static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var l = Node(name, parent, pos).AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            return l;
        }

        static ParticleSystem Particles(string name, Transform parent, uint color, float size, float fallSpeed, float lifetime, float rate, bool stretched)
        {
            var go = Node(name, parent, new Vector3(0, 18, 0));  // 카메라(높이 32) 바로 앞에서 커 보이지 않게
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = 0;
            main.startSize = size;
            main.startColor = Hex(color);
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80, 1, 60);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = vel.z = 0;
            vel.y = -fallSpeed;

            var r = go.GetComponent<ParticleSystemRenderer>();
            string matPath = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat)
            {
                // 원형 + 반투명 (기본값은 불투명 사각형)
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd").texture);
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            r.sharedMaterial = mat;
            if (stretched)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.05f;
                r.lengthScale = 2f;
            }
            return ps;
        }

        static void BuildEventHud(Transform hud)
        {
            // 번개 섬광: HUD 맨 뒤 (UI 아래, 월드 위)
            var flash = UIPanel("LightningFlash", hud, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0));
            Stretch(flash.rectTransform);
            flash.sprite = null;
            flash.raycastTarget = false;
            flash.transform.SetAsFirstSibling();

            var tl = new Vector2(0, 1);
            var clock = UIText("ClockText", hud, tl, new Vector2(24, -184), new Vector2(700, 40), 28, TextAlignmentOptions.Left);
            clock.fontSharedMaterial = outlineMat;
            var weatherText = UIText("WeatherText", hud, tl, new Vector2(24, -226), new Vector2(700, 40), 26, TextAlignmentOptions.Left);
            weatherText.fontSharedMaterial = outlineMat;

            // 우상단 노선 버튼과 겹치지 않게 알림/긴급 문구 폭을 줄이고 자동 크기
            var notice = (RectTransform)hud.Find("Notification");
            notice.sizeDelta = new Vector2(980, 84);
            var noticeText = notice.GetComponent<TextMeshProUGUI>();
            noticeText.textWrappingMode = TextWrappingModes.Normal;
            noticeText.enableAutoSizing = true;
            noticeText.fontSizeMin = 20;
            noticeText.fontSizeMax = 30;
            foreach (var region in new[] { Region.Daegu, Region.Busan, Region.Seoul })
            foreach (var prefix in new[] { "Depart_", "Unlock_" })
            {
                var rt = (RectTransform)hud.Find(prefix + region);
                rt.sizeDelta = new Vector2(420, rt.sizeDelta.y);
                ((RectTransform)rt.Find("Label")).sizeDelta = new Vector2(400, 64);
                var label = rt.GetComponentInChildren<TextMeshProUGUI>();
                label.enableAutoSizing = true;
                label.fontSizeMin = 16;
                label.fontSizeMax = 26;
            }

            var quest = UIPanel("QuestPanel", hud, new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(980, 56), new Color(0.6f, 0.1f, 0.1f, 0.9f));
            var questText = UIText("Text", quest.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 52), 24, TextAlignmentOptions.Center);
            questText.enableAutoSizing = true;
            questText.fontSizeMin = 16;
            questText.fontSizeMax = 24;
            quest.gameObject.SetActive(false);

            var (fever, feverLabel) = UIButton("FeverButton", hud, new Vector2(0.5f, 0), new Vector2(-320, 24), new Vector2(300, 80), Hex(0xea580c), 26);

            // 업그레이드 창 아래쪽에 날씨 테스트 버튼 (원본 디버그 버튼)
            var box = (RectTransform)hud.Find("UpgradePanel/Window/Box");
            box.sizeDelta = new Vector2(box.sizeDelta.x, 960);
            foreach (var n in new[] { "WeatherTestTitle", "WeatherTest_Clear", "WeatherTest_Snow", "WeatherTest_Rain", "WeatherTest_Thunder" })
                DestroyChild(box, n);
            var title = UIText("WeatherTestTitle", box, new Vector2(0, 1), new Vector2(30, -868), new Vector2(300, 50), 22, TextAlignmentOptions.Left);
            title.text = "날씨 테스트";
            var tests = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                var w = (Weather)i;
                var (btn, label) = UIButton($"WeatherTest_{w}", box, new Vector2(0.5f, 1), new Vector2(-330 + i * 230, -862), new Vector2(210, 60), Hex(0x334155), 22);
                label.text = WeatherManager.Name(w);
                tests[i] = btn;
            }

            var ev = hud.gameObject.AddComponent<EventHud>();
            Set(ev, "clockText", clock);
            Set(ev, "weatherText", weatherText);
            Set(ev, "questPanel", quest.gameObject);
            Set(ev, "questText", questText);
            Set(ev, "feverButton", fever);
            Set(ev, "feverLabel", feverLabel);
            Set(ev, "lightningFlash", flash);
            Set(ev, "weatherTestButtons", tests);
        }
    }
}
