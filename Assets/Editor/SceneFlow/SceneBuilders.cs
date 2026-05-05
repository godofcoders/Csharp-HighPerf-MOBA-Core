#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MOBA.Core.Infrastructure;

namespace MOBA.Editor
{
    /// <summary>
    /// Editor-only scene builders. Each [MenuItem] entry creates a fresh
    /// scene, populates a starter hierarchy (Canvas + EventSystem + screen
    /// controller + placeholder buttons/text), and saves it under
    /// Assets/Scenes/.
    ///
    /// All buttons/text are LEGACY UnityEngine.UI to avoid pulling TMP into
    /// the editor assembly. Swap to TMP_Text in the inspector if you
    /// prefer; the runtime controllers support both.
    ///
    /// Sprites and fonts are NOT assigned by these builders — buttons get
    /// the default Unity background, text gets the default font. Polish
    /// pass (assigning brand sprites, custom fonts) is on you.
    ///
    /// Run order:
    ///   1. MOBA → Scene Flow → Setup Build Settings   (one-time)
    ///   2. MOBA → Scene Flow → Build Loading Scene
    ///      … and the same for the other 4 menu screens
    ///   3. (Match scene already exists; don't run a builder for it.)
    /// </summary>
    public static class SceneBuilders
    {
        private const string ScenesFolder = "Assets/Scenes";

        // ---------- Build Settings setup ----------

        [MenuItem("MOBA/Scene Flow/Setup Build Settings")]
        public static void SetupBuildSettings()
        {
            EnsureScenesFolder();

            string[] sceneNames = { "Loading", "MainMenu", "BrawlerSelect", "GameModeSelect", "Match", "Results" };
            var entries = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string name in sceneNames)
            {
                string path = $"{ScenesFolder}/{name}.unity";
                entries.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = entries.ToArray();
            Debug.Log("[SceneBuilders] Build Settings populated with 6 scenes (skipping any that don't exist on disk yet — they're added when you build them).");
        }

        // ---------- Per-scene builders ----------

        [MenuItem("MOBA/Scene Flow/Build Loading Scene")]
        public static void BuildLoadingScene()
        {
            Scene s = NewScene("Loading");

            // SceneFlow + EventSystem (only the Loading scene needs to host
            // SceneFlow; DontDestroyOnLoad carries it through).
            CreateSceneFlow();
            CreateEventSystem();

            (Canvas canvas, _) = CreateCanvas("Canvas");
            CreateBackground(canvas.transform, new Color(0.10f, 0.12f, 0.18f));

            CreateText(canvas.transform, "TitleText", "MOBA",
                new Vector2(0, 200), new Vector2(800, 120), 64);

            // Progress bar: a black background bar + a filled inner bar.
            GameObject bg = CreateImageRect(canvas.transform, "ProgressBg",
                new Vector2(0, -100), new Vector2(600, 30), new Color(0, 0, 0, 0.5f));
            GameObject fill = CreateImageRect(bg.transform, "ProgressFill",
                Vector2.zero, new Vector2(600, 30), new Color(0.30f, 0.55f, 1.00f));
            Image fillImg = fill.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;

            GameObject statusGo = CreateText(canvas.transform, "StatusText", "Loading...",
                new Vector2(0, -160), new Vector2(800, 40), 24);

            // Wire LoadingScreen.
            LoadingScreen ls = canvas.gameObject.AddComponent<LoadingScreen>();
            SerializedObject so = new SerializedObject(ls);
            so.FindProperty("_progressFill").objectReferenceValue = fillImg;
            so.FindProperty("_statusTextLegacy").objectReferenceValue = statusGo.GetComponent<Text>();
            so.ApplyModifiedProperties();

            SaveScene(s);
        }

        [MenuItem("MOBA/Scene Flow/Build MainMenu Scene")]
        public static void BuildMainMenuScene()
        {
            Scene s = NewScene("MainMenu");
            CreateEventSystem();

            (Canvas canvas, _) = CreateCanvas("Canvas");
            CreateBackground(canvas.transform, new Color(0.08f, 0.10f, 0.16f));

            CreateText(canvas.transform, "TitleText", "MOBA",
                new Vector2(0, 200), new Vector2(800, 140), 96);

            Button play = CreateButton(canvas.transform, "PlayButton", "Play",
                new Vector2(0, 0), new Vector2(280, 70));
            Button quit = CreateButton(canvas.transform, "QuitButton", "Quit",
                new Vector2(0, -100), new Vector2(280, 70));

            MainMenuScreen mm = canvas.gameObject.AddComponent<MainMenuScreen>();
            SerializedObject so = new SerializedObject(mm);
            so.FindProperty("_playButton").objectReferenceValue = play;
            so.FindProperty("_quitButton").objectReferenceValue = quit;
            so.ApplyModifiedProperties();

            SaveScene(s);
        }

        [MenuItem("MOBA/Scene Flow/Build BrawlerSelect Scene")]
        public static void BuildBrawlerSelectScene()
        {
            Scene s = NewScene("BrawlerSelect");
            CreateEventSystem();

            (Canvas canvas, _) = CreateCanvas("Canvas");
            CreateBackground(canvas.transform, new Color(0.10f, 0.12f, 0.18f));

            CreateText(canvas.transform, "TitleText", "Choose Your Brawler",
                new Vector2(0, 250), new Vector2(900, 80), 48);

            // Card container with HorizontalLayoutGroup. BrawlerSelectScreen
            // instantiates the assigned card prefab into this container.
            GameObject container = new GameObject("CardContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            container.transform.SetParent(canvas.transform, false);
            RectTransform crt = container.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(900, 240);
            crt.anchoredPosition = new Vector2(0, 0);
            HorizontalLayoutGroup hlg = container.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            Button back = CreateButton(canvas.transform, "BackButton", "Back",
                new Vector2(-450, -250), new Vector2(140, 50));

            BrawlerSelectScreen bss = canvas.gameObject.AddComponent<BrawlerSelectScreen>();
            SerializedObject so = new SerializedObject(bss);
            so.FindProperty("_cardContainer").objectReferenceValue = container.transform;
            so.FindProperty("_backButton").objectReferenceValue = back;
            // _availableBrawlers and _cardPrefab → designer wires manually.
            so.ApplyModifiedProperties();

            SaveScene(s);
        }

        [MenuItem("MOBA/Scene Flow/Build GameModeSelect Scene")]
        public static void BuildGameModeSelectScene()
        {
            Scene s = NewScene("GameModeSelect");
            CreateEventSystem();

            (Canvas canvas, _) = CreateCanvas("Canvas");
            CreateBackground(canvas.transform, new Color(0.10f, 0.12f, 0.18f));

            CreateText(canvas.transform, "TitleText", "Choose a Mode",
                new Vector2(0, 200), new Vector2(900, 80), 48);

            Button gemGrab = CreateButton(canvas.transform, "GemGrabButton", "Gem Grab",
                new Vector2(0, 20), new Vector2(360, 90));
            Button back = CreateButton(canvas.transform, "BackButton", "Back",
                new Vector2(-450, -250), new Vector2(140, 50));

            GameModeSelectScreen gms = canvas.gameObject.AddComponent<GameModeSelectScreen>();
            SerializedObject so = new SerializedObject(gms);
            so.FindProperty("_gemGrabButton").objectReferenceValue = gemGrab;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.ApplyModifiedProperties();

            SaveScene(s);
        }

        [MenuItem("MOBA/Scene Flow/Build Results Scene")]
        public static void BuildResultsScene()
        {
            Scene s = NewScene("Results");
            CreateEventSystem();

            (Canvas canvas, _) = CreateCanvas("Canvas");
            CreateBackground(canvas.transform, new Color(0.06f, 0.08f, 0.14f));

            GameObject winnerGo = CreateText(canvas.transform, "WinnerText", "Match Over",
                new Vector2(0, 180), new Vector2(900, 120), 64);
            GameObject scoreGo = CreateText(canvas.transform, "ScoreText", "Blue 0 — Red 0",
                new Vector2(0, 60), new Vector2(900, 70), 36);

            Button cont = CreateButton(canvas.transform, "ContinueButton", "Continue",
                new Vector2(-160, -100), new Vector2(280, 70));
            Button rematch = CreateButton(canvas.transform, "RematchButton", "Rematch",
                new Vector2(160, -100), new Vector2(280, 70));

            ResultsScreen rs = canvas.gameObject.AddComponent<ResultsScreen>();
            SerializedObject so = new SerializedObject(rs);
            so.FindProperty("_winnerTextLegacy").objectReferenceValue = winnerGo.GetComponent<Text>();
            so.FindProperty("_scoreTextLegacy").objectReferenceValue = scoreGo.GetComponent<Text>();
            so.FindProperty("_continueButton").objectReferenceValue = cont;
            so.FindProperty("_rematchButton").objectReferenceValue = rematch;
            so.ApplyModifiedProperties();

            SaveScene(s);
        }

        // ---------- Card prefab builder ----------

        [MenuItem("MOBA/Scene Flow/Build Brawler Card Prefab")]
        public static void BuildBrawlerCardPrefab()
        {
            const string folder = "Assets/Prefabs/UI";
            const string path = folder + "/BrawlerCard.prefab";

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            // Root: card frame (Image + Button + LayoutElement). BrawlerCardView
            // wires the structured widgets below.
            GameObject root = new GameObject("BrawlerCard",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement),
                typeof(BrawlerCardView));

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220, 320);

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.14f, 0.17f, 0.24f);
            bg.sprite = DefaultUISprite;

            LayoutElement le = root.GetComponent<LayoutElement>();
            le.preferredWidth = 220;
            le.preferredHeight = 320;

            // Accent strip across the top (tinted by archetype at runtime).
            GameObject accent = CreateImageRect(root.transform, "AccentStrip",
                new Vector2(0, 145), new Vector2(220, 12), new Color(0.35f, 0.55f, 0.85f));

            // Portrait area — large square in upper half.
            GameObject portrait = CreateImageRect(root.transform, "Portrait",
                new Vector2(0, 50), new Vector2(180, 160), Color.white);

            // Name label — under portrait.
            GameObject nameGo = CreateText(root.transform, "NameText", "Brawler",
                new Vector2(0, -55), new Vector2(200, 36), 22);

            // Archetype label — small, below name.
            GameObject archetypeGo = CreateText(root.transform, "ArchetypeText", "ARCHETYPE",
                new Vector2(0, -82), new Vector2(200, 22), 14);
            Text archetypeText = archetypeGo.GetComponent<Text>();
            archetypeText.color = new Color(0.65f, 0.75f, 0.90f);

            // Stat strip (HP + DMG) at bottom.
            GameObject hpLabel = CreateText(root.transform, "HpLabel", "HP",
                new Vector2(-50, -120), new Vector2(40, 18), 12);
            hpLabel.GetComponent<Text>().color = new Color(0.6f, 0.6f, 0.6f);
            GameObject hpValue = CreateText(root.transform, "HpValue", "3000",
                new Vector2(-50, -140), new Vector2(60, 22), 16);

            GameObject dmgLabel = CreateText(root.transform, "DmgLabel", "DMG",
                new Vector2(50, -120), new Vector2(40, 18), 12);
            dmgLabel.GetComponent<Text>().color = new Color(0.6f, 0.6f, 0.6f);
            GameObject dmgValue = CreateText(root.transform, "DmgValue", "500",
                new Vector2(50, -140), new Vector2(60, 22), 16);

            // Wire BrawlerCardView's serialised fields.
            BrawlerCardView view = root.GetComponent<BrawlerCardView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_portraitImage").objectReferenceValue = portrait.GetComponent<Image>();
            so.FindProperty("_accentImage").objectReferenceValue = accent.GetComponent<Image>();
            so.FindProperty("_nameTextLegacy").objectReferenceValue = nameGo.GetComponent<Text>();
            so.FindProperty("_archetypeTextLegacy").objectReferenceValue = archetypeText;
            so.FindProperty("_healthValueLegacy").objectReferenceValue = hpValue.GetComponent<Text>();
            so.FindProperty("_damageValueLegacy").objectReferenceValue = dmgValue.GetComponent<Text>();
            so.ApplyModifiedProperties();

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"[SceneBuilders] Built {path}");
            EditorGUIUtility.PingObject(prefabAsset);
            Selection.activeObject = prefabAsset;
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
                AssetDatabase.Refresh();
            }
        }

        private static Scene NewScene(string name)
        {
            EnsureScenesFolder();
            Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            s.name = name;
            return s;
        }

        private static void SaveScene(Scene s)
        {
            string path = $"{ScenesFolder}/{s.name}.unity";
            EditorSceneManager.SaveScene(s, path);
            Debug.Log($"[SceneBuilders] Saved {path}");

            // Auto-add to Build Settings if not already.
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool present = false;
            foreach (var e in list)
                if (e.path == path) { present = true; break; }
            if (!present)
            {
                list.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }

        private static GameObject CreateSceneFlow()
        {
            GameObject go = new GameObject("SceneFlow");
            go.AddComponent<SceneFlow>();
            return go;
        }

        private static void CreateEventSystem()
        {
            // Use InputSystemUIInputModule (not StandaloneInputModule) because
            // the project's Player Settings have Active Input Handling set
            // to "Input System Package". StandaloneInputModule throws
            // InvalidOperationException at runtime in that mode.
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private static (Canvas canvas, CanvasScaler scaler) CreateCanvas(string name)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
            return (c, cs);
        }

        // Default Unity UI sprite. Without a sprite assigned, an Image with
        // Type = Filled draws nothing (no quad to fill). Cache once.
        private static Sprite _defaultUISprite;
        private static Sprite DefaultUISprite
        {
            get
            {
                if (_defaultUISprite == null)
                    _defaultUISprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                return _defaultUISprite;
            }
        }

        private static void CreateBackground(Transform parent, Color color)
        {
            GameObject go = new GameObject("Background", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = DefaultUISprite;
        }

        private static GameObject CreateText(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Text t = go.GetComponent<Text>();
            t.text = text;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.20f, 0.30f, 0.50f);
            img.sprite = DefaultUISprite;

            // Child text.
            CreateText(go.transform, "Label", label, Vector2.zero, size, Mathf.Max(18, (int)(size.y * 0.45f)));

            return go.GetComponent<Button>();
        }

        private static GameObject CreateImageRect(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = DefaultUISprite;
            return go;
        }
    }
}
#endif
