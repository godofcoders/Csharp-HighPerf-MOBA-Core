using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Runtime fallback HUD composer for the Match scene. It only installs
    /// when no authored match HUD exists, so a designer-made prefab can replace
    /// this without creating duplicate score/countdown/feed widgets.
    /// </summary>
    public sealed class MatchHUDBootstrap : MonoBehaviour
    {
        private const string MatchSceneName = "Match";
        private const int CanvasSortingOrder = 80;

        private static readonly Color HudPanel = new Color(0.012f, 0.026f, 0.066f, 0.88f);
        private static readonly Color HudPanelSolid = new Color(0.015f, 0.034f, 0.088f, 0.96f);
        private static readonly Color HudPanelRaised = new Color(0.055f, 0.096f, 0.190f, 0.92f);
        private static readonly Color HudPanelInset = new Color(0.004f, 0.012f, 0.034f, 0.72f);
        private static readonly Color HudGold = new Color(1.000f, 0.745f, 0.145f, 0.98f);
        private static readonly Color HudCyan = new Color(0.160f, 0.780f, 1.000f, 0.98f);
        private static readonly Color HudMutedText = new Color(0.760f, 0.850f, 1.000f, 0.88f);
        private static readonly Color HudSoftText = new Color(0.920f, 0.960f, 1.000f, 0.96f);
        private static readonly Color BlueTeamAccent = new Color(0.180f, 0.460f, 1.000f, 0.98f);
        private static readonly Color RedTeamAccent = new Color(1.000f, 0.220f, 0.280f, 0.98f);
        private static readonly Color GemMagenta = new Color(1.000f, 0.225f, 0.920f, 0.98f);

        private static Font _runtimeFont;

        private bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstallForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallForScene(scene);
        }

        private static void TryInstallForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MatchSceneName)
                return;

            if (HasAuthoredMatchHUD())
                return;

            GameObject host = new GameObject("MatchHUDBootstrap");
            MatchHUDBootstrap bootstrap = host.AddComponent<MatchHUDBootstrap>();
            bootstrap.Install();
        }

        private static bool HasAuthoredMatchHUD()
        {
            return Object.FindObjectOfType<MatchHUDBootstrap>() != null ||
                   Object.FindObjectOfType<MatchHUD>() != null ||
                   Object.FindObjectOfType<MatchCountdownOverlay>() != null ||
                   Object.FindObjectOfType<GemGrabCountdownOverlay>() != null ||
                   Object.FindObjectOfType<CombatLogHUD>() != null ||
                   Object.FindObjectOfType<DeathOverlay>() != null;
        }

        private void Start()
        {
            Install();
        }

        public void Install()
        {
            if (_installed)
                return;

            _installed = true;

            GameObject canvasGo = new GameObject(
                "MatchHUDCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform canvasTransform = canvasGo.transform;
            CreateMatchStatus(canvasTransform);
            CreatePlayerResourceHUD(canvasTransform);
            CreateCombatFeed(canvasTransform);
            CreateCountdownOverlay(canvasTransform);
            CreateGemGrabCountdownOverlay(canvasTransform);
            CreateDeathOverlay(canvasTransform);

            SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));
        }

        private static void CreateMatchStatus(Transform parent)
        {
            GameObject root = CreateController(parent, "MatchStatusRoot");

            Text blueLabel;
            Text blueGemText = CreateTeamScoreWing(
                root.transform,
                "BlueScoreWing",
                new Vector2(30f, -24f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                "BLUE",
                BlueTeamAccent,
                true,
                out blueLabel,
                out GameObject blueLeaderHighlight);

            Text redLabel;
            Text redGemText = CreateTeamScoreWing(
                root.transform,
                "RedScoreWing",
                new Vector2(-30f, -24f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                "RED",
                RedTeamAccent,
                false,
                out redLabel,
                out GameObject redLeaderHighlight);

            GameObject timerCapsule = CreateRectPanel(
                root.transform,
                "MatchTimerCapsule",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -20f),
                new Vector2(150f, 44f),
                new Color(0.004f, 0.011f, 0.028f, 0.78f));
            AddPanelShadow(timerCapsule, new Vector2(0f, -3f), 0.24f);

            Text timerText = CreateText(
                timerCapsule.transform,
                "MatchTimerText",
                "--:--",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                30,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            Text statusText = CreateText(
                root.transform,
                "HoldStatusText",
                string.Empty,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -74f),
                new Vector2(620f, 30f),
                17,
                TextAnchor.MiddleCenter,
                HudSoftText,
                FontStyle.Bold);

            GameObject knockoutRoot = CreateController(root.transform, "KnockoutWidgets");
            CreateKnockoutTeamSlots(
                knockoutRoot.transform,
                "BlueKnockout",
                true,
                new Color(0.18f, 0.46f, 1f, 0.38f),
                out Image[] bluePortraits,
                out GameObject[] blueCrosses,
                out Text[] blueLabels);

            CreateKnockoutTeamSlots(
                knockoutRoot.transform,
                "RedKnockout",
                false,
                new Color(1f, 0.22f, 0.28f, 0.38f),
                out Image[] redPortraits,
                out GameObject[] redCrosses,
                out Text[] redLabels);

            Image[] roundMarkers = CreateKnockoutRoundMarkers(knockoutRoot.transform);
            knockoutRoot.SetActive(false);

            MatchHUD hud = root.AddComponent<MatchHUD>();
            hud.BindTextTargets(null, statusText);
            hud.BindGemScoreWidgets(
                null,
                blueGemText,
                null,
                redGemText,
                null,
                timerText,
                blueLeaderHighlight,
                redLeaderHighlight);
            hud.BindTopLayoutWidgets(
                blueGemText.transform.parent.gameObject,
                redGemText.transform.parent.gameObject,
                timerCapsule,
                statusText.gameObject,
                blueLabel,
                redLabel);
            hud.BindKnockoutWidgets(
                knockoutRoot,
                bluePortraits,
                blueCrosses,
                blueLabels,
                redPortraits,
                redCrosses,
                redLabels,
                roundMarkers);
        }

        private static void CreateKnockoutTeamSlots(
            Transform parent,
            string prefix,
            bool leftSide,
            Color teamColor,
            out Image[] portraits,
            out GameObject[] crosses,
            out Text[] labels)
        {
            const int slotCount = 3;
            portraits = new Image[slotCount];
            crosses = new GameObject[slotCount];
            labels = new Text[slotCount];

            Vector2 anchor = leftSide ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            Vector2 pivot = leftSide ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            float direction = leftSide ? 1f : -1f;

            for (int i = 0; i < slotCount; i++)
            {
                GameObject slot = CreateRectPanel(
                    parent,
                    $"{prefix}Slot{i + 1}",
                    anchor,
                    anchor,
                    pivot,
                    new Vector2(direction * (32f + i * 46f), -92f),
                    new Vector2(39f, 39f),
                    HudPanelSolid);
                AddPanelShadow(slot, new Vector2(0f, -2f), 0.22f);

                CreateRectPanel(
                    slot.transform,
                    $"{prefix}SlotAccent{i + 1}",
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f),
                    Vector2.zero,
                    new Vector2(0f, 3f),
                    teamColor);

                portraits[i] = CreateImage(
                    slot.transform,
                    $"{prefix}Portrait{i + 1}",
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero,
                    new Color(teamColor.r, teamColor.g, teamColor.b, 0.72f));

                labels[i] = CreateText(
                    slot.transform,
                    $"{prefix}Label{i + 1}",
                    string.Empty,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 1f),
                    Vector2.zero,
                    8,
                    TextAnchor.LowerCenter,
                    HudSoftText,
                    FontStyle.Bold);

                crosses[i] = CreateKnockoutCross(slot.transform, $"{prefix}Cross{i + 1}");
                crosses[i].SetActive(false);
            }
        }

        private static GameObject CreateKnockoutCross(Transform parent, string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());

            GameObject slashA = CreateRectPanel(
                root.transform,
                "SlashA",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(44f, 5f),
                new Color(1f, 0.05f, 0.09f, 0.96f));
            slashA.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            GameObject slashB = CreateRectPanel(
                root.transform,
                "SlashB",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(44f, 5f),
                new Color(1f, 0.05f, 0.09f, 0.96f));
            slashB.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            return root;
        }

        private static Image[] CreateKnockoutRoundMarkers(Transform parent)
        {
            const int markerCount = 3;
            Image[] markers = new Image[markerCount];

            for (int i = 0; i < markerCount; i++)
            {
                GameObject marker = CreateRectPanel(
                    parent,
                    $"KnockoutRoundMarker{i + 1}",
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(-34f + i * 34f, -78f),
                    new Vector2(26f, 13f),
                    new Color(1f, 1f, 1f, 0.16f));

                markers[i] = marker.GetComponent<Image>();
            }

            return markers;
        }

        private static Text CreateTeamScoreWing(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 anchor,
            Vector2 pivot,
            string label,
            Color teamColor,
            bool leftSide,
            out Text labelText,
            out GameObject leaderHighlight)
        {
            GameObject card = CreateRectPanel(
                parent,
                name,
                anchor,
                anchor,
                pivot,
                anchoredPosition,
                new Vector2(318f, 58f),
                new Color(0.004f, 0.010f, 0.030f, 0.76f));
            AddPanelShadow(card, new Vector2(0f, -3f), 0.22f);

            leaderHighlight = CreateRectPanel(
                card.transform,
                name + "LeaderHighlight",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(teamColor.r, teamColor.g, teamColor.b, 0.22f));
            leaderHighlight.SetActive(false);

            Vector2 sideAnchor = leftSide ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            Vector2 sideAnchorTop = leftSide ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            Vector2 sidePivot = leftSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            float direction = leftSide ? 1f : -1f;

            CreateRectPanel(
                card.transform,
                name + "SideAccent",
                sideAnchor,
                sideAnchorTop,
                sidePivot,
                Vector2.zero,
                new Vector2(7f, 0f),
                teamColor);

            CreateRectPanel(
                card.transform,
                name + "TopSheen",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 4f),
                new Color(1f, 1f, 1f, 0.10f));

            Image gemIcon = CreateImage(
                card.transform,
                name + "GemIcon",
                leftSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f),
                leftSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(direction * 36f, 0f),
                new Vector2(30f, 30f),
                GemMagenta);
            gemIcon.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            TextAnchor labelAnchor = leftSide ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            Vector2 textAnchor = leftSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            Vector2 textPivot = leftSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);

            labelText = CreateText(
                card.transform,
                name + "Label",
                label,
                textAnchor,
                textAnchor,
                textPivot,
                new Vector2(direction * 72f, 11f),
                new Vector2(186f, 20f),
                13,
                labelAnchor,
                HudMutedText,
                FontStyle.Bold);

            return CreateText(
                card.transform,
                name + "CountText",
                "--",
                textAnchor,
                textAnchor,
                textPivot,
                new Vector2(direction * 72f, -10f),
                new Vector2(186f, 34f),
                28,
                labelAnchor,
                Color.white,
                FontStyle.Bold);
        }

        private static void CreatePlayerResourceHUD(Transform parent)
        {
            GameObject controller = CreateController(parent, "PlayerHUDController");

            GameObject superRoot = CreateScreenAbilityButton(
                parent,
                "SuperButton",
                new Vector2(1f, 0f),
                new Vector2(-126f, 122f),
                112f,
                HudGold,
                "SUPER",
                out Image superFill,
                out GameObject superReadyVisual);

            Text superText = FindButtonValueText(superRoot);

            GameObject hyperRoot = CreateScreenAbilityButton(
                parent,
                "HyperchargeButton",
                new Vector2(1f, 0f),
                new Vector2(-260f, 122f),
                104f,
                new Color(0.68f, 0.22f, 1f, 0.96f),
                "HYPER",
                out Image hyperchargeFill,
                out GameObject hyperchargeActiveVisual);

            Text hyperchargeText = FindButtonValueText(hyperRoot);

            GameObject gadgetRoot = CreateScreenAbilityButton(
                parent,
                "GadgetButton",
                new Vector2(1f, 0f),
                new Vector2(-94f, 244f),
                76f,
                new Color(0.24f, 0.78f, 0.34f, 0.95f),
                "GADGET",
                out Image gadgetCooldownOverlay,
                out GameObject gadgetReadyVisual);

            gadgetCooldownOverlay.color = new Color(0f, 0f, 0f, 0.58f);
            gadgetCooldownOverlay.fillAmount = 0f;

            Text gadgetCharges = FindButtonValueText(gadgetRoot);

            gadgetRoot.SetActive(false);

            PlayerHUD playerHUD = controller.AddComponent<PlayerHUD>();
            playerHUD.BindAmmoWidgets(null, null, null, null);
            playerHUD.BindSuperWidgets(superFill, superReadyVisual, null, superText);
            playerHUD.BindHyperchargeWidgets(hyperRoot, hyperchargeFill, hyperchargeActiveVisual, null, hyperchargeText);
            playerHUD.BindGadgetWidgets(gadgetRoot, gadgetCooldownOverlay, null, gadgetCharges, gadgetReadyVisual);
            playerHUD.BindCarrierWidgets(null, null, null);
        }

        private static GameObject CreateScreenAbilityButton(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 anchoredPosition,
            float size,
            Color accentColor,
            string label,
            out Image fill,
            out GameObject readyVisual)
        {
            Color baseColor = new Color(
                accentColor.r * 0.30f,
                accentColor.g * 0.30f,
                accentColor.b * 0.30f,
                0.72f);

            GameObject root = CreateCirclePanel(
                parent,
                name,
                anchor,
                anchor,
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                new Vector2(size, size),
                baseColor);
            AddPanelShadow(root, new Vector2(0f, -8f), 0.42f);

            CreateCirclePanel(
                root.transform,
                "ButtonOuterGlow",
                new Vector2(-0.05f, -0.05f),
                new Vector2(1.05f, 1.05f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.16f));

            CreateCirclePanel(
                root.transform,
                "ButtonRim",
                new Vector2(0.04f, 0.04f),
                new Vector2(0.96f, 0.96f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0f, 0f, 0f, 0.66f));

            fill = CreateFilledImage(
                root.transform,
                "ButtonFill",
                new Vector2(0.10f, 0.10f),
                new Vector2(0.90f, 0.90f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.84f),
                Image.FillMethod.Radial360,
                (int)Image.Origin360.Bottom);
            fill.sprite = RuntimeUISpriteUtility.GetSoftCircleSprite();
            fill.fillClockwise = true;
            fill.fillAmount = 0f;

            readyVisual = CreateCirclePanel(
                root.transform,
                "ButtonReadyVisual",
                new Vector2(-0.12f, -0.12f),
                new Vector2(1.12f, 1.12f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 0.96f, 0.40f, 0.26f));
            readyVisual.SetActive(false);

            CreateText(
                root.transform,
                "ButtonValueText",
                "0%",
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                size >= 100f ? 22 : 18,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            CreateText(
                root.transform,
                "ButtonLabel",
                label,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(size + 22f, 22f),
                size >= 100f ? 13 : 11,
                TextAnchor.UpperCenter,
                HudSoftText,
                FontStyle.Bold);

            return root;
        }

        private static Text FindButtonValueText(GameObject buttonRoot)
        {
            if (buttonRoot == null)
                return null;

            Transform valueTransform = buttonRoot.transform.Find("ButtonValueText");
            return valueTransform != null ? valueTransform.GetComponent<Text>() : null;
        }

        private static void CreateCombatFeed(Transform parent)
        {
            GameObject controller = CreateController(parent, "CombatFeedController");
            GameObject panel = CreateRectPanel(
                parent,
                "KillFeedPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -92f),
                new Vector2(486f, 158f),
                new Color(0f, 0f, 0f, 0f));
            AddPanelShadow(panel, new Vector2(0f, -4f), 0.20f);

            const int rowCount = 3;
            GameObject[] rowRoots = new GameObject[rowCount];
            Image[] rowAccents = new Image[rowCount];
            Image[] rowBadges = new Image[rowCount];
            Text[] rowIcons = new Text[rowCount];
            Text[] rowTexts = new Text[rowCount];

            for (int i = 0; i < rowCount; i++)
            {
                GameObject row = CreateRectPanel(
                    panel.transform,
                    $"KillFeedRow{i + 1}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -(i * 50f)),
                    new Vector2(452f, 42f),
                    new Color(0.008f, 0.020f, 0.052f, 0.66f));
                AddPanelShadow(row, new Vector2(0f, -2f), 0.24f);

                Image accent = CreateImage(
                    row.transform,
                    "KillFeedAccent",
                    Vector2.zero,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(6f, 0f),
                    new Color(1f, 0.22f, 0.30f, 0.96f));

                GameObject badge = CreateCirclePanel(
                    row.transform,
                    "KillFeedBadge",
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(30f, 0f),
                    new Vector2(30f, 30f),
                    new Color(1f, 0.22f, 0.30f, 0.88f));

                Text icon = CreateText(
                    badge.transform,
                    "KillFeedIcon",
                    "KO",
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero,
                    10,
                    TextAnchor.MiddleCenter,
                    Color.white,
                    FontStyle.Bold);

                Text line = CreateText(
                    row.transform,
                    "KillFeedText",
                    string.Empty,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(26f, 0f),
                    new Vector2(-38f, 0f),
                    16,
                    TextAnchor.MiddleLeft,
                    HudSoftText,
                    FontStyle.Bold);
                line.supportRichText = true;
                line.horizontalOverflow = HorizontalWrapMode.Wrap;

                rowRoots[i] = row;
                rowAccents[i] = accent;
                rowBadges[i] = badge.GetComponent<Image>();
                rowIcons[i] = icon;
                rowTexts[i] = line;
                row.SetActive(false);
            }

            CombatLogHUD combatLog = controller.AddComponent<CombatLogHUD>();
            combatLog.BindTextTargets(null, null, panel);
            combatLog.BindGraphicRows(rowRoots, rowAccents, rowBadges, rowIcons, rowTexts);
            panel.SetActive(false);
        }

        private static void CreateCountdownOverlay(Transform parent)
        {
            GameObject controller = CreateController(parent, "CountdownOverlayController");
            GameObject root = CreateRectPanel(
                parent,
                "CountdownOverlay",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-42f, 126f),
                new Vector2(430f, 96f),
                HudPanelSolid);
            AddPanelShadow(root, new Vector2(0f, -6f), 0.32f);

            CreateRectPanel(
                root.transform,
                "CountdownAccent",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 5f),
                HudGold);

            Text countdownText = CreateText(
                root.transform,
                "CountdownText",
                string.Empty,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                38,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            countdownText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countdownText.verticalOverflow = VerticalWrapMode.Truncate;

            MatchCountdownOverlay countdown = controller.AddComponent<MatchCountdownOverlay>();
            countdown.BindOverlay(root, null, countdownText);
        }

        private static void CreateGemGrabCountdownOverlay(Transform parent)
        {
            GameObject controller = CreateController(parent, "GemGrabCountdownOverlayController");
            GameObject root = CreateRectPanel(
                parent,
                "GemGrabCountdownOverlay",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -178f),
                new Vector2(800f, 72f),
                new Color(0.012f, 0.026f, 0.066f, 0.82f));
            AddPanelShadow(root, new Vector2(0f, -4f), 0.24f);

            CreateRectPanel(
                root.transform,
                "GemCountdownAccent",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 5f),
                GemMagenta);

            Text countdownText = CreateText(
                root.transform,
                "GemGrabCountdownText",
                string.Empty,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                40,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            countdownText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countdownText.verticalOverflow = VerticalWrapMode.Truncate;

            GemGrabCountdownOverlay countdown = controller.AddComponent<GemGrabCountdownOverlay>();
            countdown.BindOverlay(root, null, countdownText);
        }

        private static void CreateDeathOverlay(Transform parent)
        {
            GameObject controller = CreateController(parent, "DeathOverlayController");
            GameObject root = CreatePanel(
                parent,
                "DeathOverlay",
                new Color(0.004f, 0.010f, 0.026f, 0.68f));

            GameObject card = CreateRectPanel(
                root.transform,
                "DeathOverlayCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(640f, 230f),
                HudPanelSolid);
            AddPanelShadow(card, new Vector2(0f, -8f), 0.38f);

            CreateRectPanel(
                card.transform,
                "DeathOverlayAccent",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 6f),
                new Color(1f, 0.22f, 0.28f, 0.94f));

            Text titleText = CreateText(
                card.transform,
                "DeathTitleText",
                "You died",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 62f),
                new Vector2(580f, 72f),
                50,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            Text countdownText = CreateText(
                card.transform,
                "RespawnCountdownText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 2f),
                new Vector2(580f, 54f),
                30,
                TextAnchor.MiddleCenter,
                HudMutedText,
                FontStyle.Bold);

            Text killerText = CreateText(
                card.transform,
                "KilledByText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -54f),
                new Vector2(580f, 44f),
                22,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.82f, 0.82f, 0.92f),
                FontStyle.Bold);

            DeathOverlay deathOverlay = controller.AddComponent<DeathOverlay>();
            deathOverlay.BindOverlay(
                root,
                null,
                titleText,
                null,
                countdownText,
                null,
                killerText);
        }

        private static GameObject CreateController(Transform parent, string name)
        {
            GameObject controller = new GameObject(name, typeof(RectTransform));
            controller.transform.SetParent(parent, false);
            RectTransform rect = controller.GetComponent<RectTransform>();
            Stretch(rect);
            return controller;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Stretch(rect);

            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.sprite = ResolveUISprite();
            image.raycastTarget = false;

            return panel;
        }

        private static GameObject CreateRectPanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.sprite = ResolveUISprite();
            image.raycastTarget = false;

            return panel;
        }

        private static GameObject CreateCirclePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject panel = CreateRectPanel(
                parent,
                name,
                anchorMin,
                anchorMax,
                pivot,
                anchoredPosition,
                size,
                color);

            Image image = panel.GetComponent<Image>();
            if (image != null)
                image.sprite = RuntimeUISpriteUtility.GetSoftCircleSprite();

            return panel;
        }

        private static void AddPanelShadow(GameObject target, Vector2 distance, float alpha)
        {
            if (target == null || target.GetComponent<Shadow>() != null)
                return;

            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static GameObject CreateAbilityMeter(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Color baseColor,
            out Image fill,
            out GameObject readyVisual)
        {
            GameObject root = CreateRectPanel(
                parent,
                name,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                anchoredPosition,
                new Vector2(82f, 82f),
                HudPanelRaised);
            AddPanelShadow(root, new Vector2(0f, -3f), 0.22f);

            CreateRectPanel(
                root.transform,
                name + "Accent",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 5f),
                new Color(baseColor.r, baseColor.g, baseColor.b, 0.92f));

            CreateRectPanel(
                root.transform,
                name + "Inset",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                HudPanelInset);

            fill = CreateFilledImage(
                root.transform,
                name + "Fill",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(baseColor.r, baseColor.g, baseColor.b, 0.56f),
                Image.FillMethod.Radial360,
                (int)Image.Origin360.Bottom);

            fill.fillClockwise = true;
            fill.fillAmount = 0f;

            readyVisual = CreateRectPanel(
                root.transform,
                name + "ReadyVisual",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 0.92f, 0.22f, 0.20f));

            readyVisual.SetActive(false);
            return root;
        }

        private static Image CreateFilledImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            Image.FillMethod fillMethod,
            int fillOrigin)
        {
            Image image = CreateImage(
                parent,
                name,
                anchorMin,
                anchorMax,
                pivot,
                anchoredPosition,
                size,
                color);

            image.type = Image.Type.Filled;
            image.fillMethod = fillMethod;
            image.fillOrigin = fillOrigin;
            image.fillAmount = 0f;
            return image;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = ResolveUISprite();
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle fontStyle)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));

            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text uiText = go.GetComponent<Text>();
            uiText.text = text;
            uiText.font = ResolveFont();
            uiText.fontSize = fontSize;
            uiText.fontStyle = fontStyle;
            uiText.alignment = alignment;
            uiText.color = color;
            uiText.raycastTarget = false;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.74f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            outline.useGraphicAlpha = true;

            return uiText;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Font ResolveFont()
        {
            if (_runtimeFont != null)
                return _runtimeFont;

            _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
                _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return _runtimeFont;
        }

        private static Sprite ResolveUISprite()
        {
            return RuntimeUISpriteUtility.GetSolidWhiteSprite();
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0 || root == null)
                return;

            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
            }
        }
    }
}
