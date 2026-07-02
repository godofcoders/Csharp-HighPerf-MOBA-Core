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
        private const int RuntimeAmmoSlotCount = 5;

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
            GameObject panel = CreateRectPanel(
                parent,
                "GemScorePanel",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(780f, 128f),
                new Color(0f, 0f, 0f, 0.32f));

            Text blueGemText = CreateTeamGemCard(
                panel.transform,
                "BlueGemScore",
                new Vector2(18f, 20f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                "BLUE",
                new Color(0.22f, 0.48f, 1f, 0.34f),
                out GameObject blueLeaderHighlight);

            Text redGemText = CreateTeamGemCard(
                panel.transform,
                "RedGemScore",
                new Vector2(-18f, 20f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                "RED",
                new Color(1f, 0.22f, 0.28f, 0.34f),
                out GameObject redLeaderHighlight);

            Text timerText = CreateText(
                panel.transform,
                "MatchTimerText",
                "--:--",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 28f),
                new Vector2(150f, 32f),
                28,
                TextAnchor.UpperCenter,
                Color.white,
                FontStyle.Bold);

            Text statusText = CreateText(
                panel.transform,
                "HoldStatusText",
                string.Empty,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 38f),
                new Vector2(300f, 22f),
                16,
                TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.82f),
                FontStyle.Bold);

            GameObject knockoutRoot = CreateController(panel.transform, "KnockoutWidgets");
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

            MatchHUD hud = panel.AddComponent<MatchHUD>();
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

            Vector2 anchor = leftSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            Vector2 pivot = leftSide ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            float direction = leftSide ? 1f : -1f;

            for (int i = 0; i < slotCount; i++)
            {
                GameObject slot = CreateRectPanel(
                    parent,
                    $"{prefix}Slot{i + 1}",
                    anchor,
                    anchor,
                    pivot,
                    new Vector2(direction * (24f + i * 43f), -34f),
                    new Vector2(36f, 36f),
                    new Color(0f, 0f, 0f, 0.35f));

                portraits[i] = CreateImage(
                    slot.transform,
                    $"{prefix}Portrait{i + 1}",
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero,
                    teamColor);

                labels[i] = CreateText(
                    slot.transform,
                    $"{prefix}Label{i + 1}",
                    string.Empty,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 1f),
                    Vector2.zero,
                    9,
                    TextAnchor.LowerCenter,
                    Color.white,
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
                new Color(1f, 0.08f, 0.08f, 0.94f));
            slashA.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            GameObject slashB = CreateRectPanel(
                root.transform,
                "SlashB",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(44f, 5f),
                new Color(1f, 0.08f, 0.08f, 0.94f));
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
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-30f + i * 30f, -35f),
                    new Vector2(22f, 14f),
                    new Color(1f, 1f, 1f, 0.18f));

                markers[i] = marker.GetComponent<Image>();
            }

            return markers;
        }

        private static Text CreateTeamGemCard(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 anchor,
            Vector2 pivot,
            string label,
            Color teamColor,
            out GameObject leaderHighlight)
        {
            GameObject card = CreateRectPanel(
                parent,
                name,
                anchor,
                anchor,
                pivot,
                anchoredPosition,
                new Vector2(160f, 52f),
                new Color(0f, 0f, 0f, 0.24f));

            leaderHighlight = CreateRectPanel(
                card.transform,
                name + "LeaderHighlight",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                teamColor);
            leaderHighlight.SetActive(false);

            Image gemIcon = CreateImage(
                card.transform,
                name + "GemIcon",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(26f, 0f),
                new Vector2(22f, 22f),
                new Color(1f, 0.24f, 0.92f, 0.94f));
            gemIcon.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            CreateText(
                card.transform,
                name + "Label",
                label,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(52f, 10f),
                new Vector2(86f, 18f),
                12,
                TextAnchor.MiddleLeft,
                new Color(1f, 1f, 1f, 0.68f),
                FontStyle.Bold);

            return CreateText(
                card.transform,
                name + "CountText",
                "--",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(52f, -9f),
                new Vector2(92f, 30f),
                24,
                TextAnchor.MiddleLeft,
                Color.white,
                FontStyle.Bold);
        }

        private static void CreatePlayerResourceHUD(Transform parent)
        {
            GameObject controller = CreateController(parent, "PlayerHUDController");
            GameObject panel = CreateRectPanel(
                parent,
                "PlayerResourcePanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(590f, 124f),
                new Color(0f, 0f, 0f, 0.34f));

            CreateText(
                panel.transform,
                "AmmoLabel",
                "AMMO",
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 92f),
                new Vector2(146f, 22f),
                16,
                TextAnchor.MiddleLeft,
                new Color(1f, 1f, 1f, 0.72f),
                FontStyle.Bold);

            Text ammoCount = CreateText(
                panel.transform,
                "AmmoCountText",
                "0/0",
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(154f, 92f),
                new Vector2(74f, 22f),
                17,
                TextAnchor.MiddleRight,
                new Color(1f, 0.88f, 0.34f, 0.95f),
                FontStyle.Bold);

            Image[] ammoSlots = new Image[RuntimeAmmoSlotCount];
            GameObject[] ammoSlotRoots = new GameObject[RuntimeAmmoSlotCount];
            for (int i = 0; i < RuntimeAmmoSlotCount; i++)
            {
                GameObject slotRoot = CreateRectPanel(
                    panel.transform,
                    $"AmmoSlot{i + 1}",
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(24f + (i * 45f), 68f),
                    new Vector2(35f, 16f),
                    new Color(1f, 1f, 1f, 0.14f));

                Image fill = CreateFilledImage(
                    slotRoot.transform,
                    $"AmmoSlot{i + 1}Fill",
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero,
                    new Color(1f, 0.84f, 0.22f, 0.96f),
                    Image.FillMethod.Horizontal,
                    (int)Image.OriginHorizontal.Left);

                fill.fillAmount = 1f;
                ammoSlotRoots[i] = slotRoot;
                ammoSlots[i] = fill;
            }

            GameObject carrierBadge = CreateRectPanel(
                panel.transform,
                "CarrierGemBadge",
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 18f),
                new Vector2(92f, 38f),
                new Color(0.85f, 0.16f, 0.74f, 0.28f));

            CreateRectPanel(
                carrierBadge.transform,
                "CarrierGemIcon",
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f),
                new Vector2(20f, 19f),
                new Vector2(20f, 20f),
                new Color(1f, 0.26f, 0.92f, 0.92f));

            Text carriedGemCount = CreateText(
                carrierBadge.transform,
                "CarriedGemCountText",
                "0",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(14f, 0f),
                new Vector2(-28f, 0f),
                21,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            carrierBadge.SetActive(false);

            GameObject superRoot = CreateAbilityMeter(
                panel.transform,
                "SuperMeter",
                new Vector2(326f, 24f),
                new Color(0.18f, 0.36f, 0.92f, 0.42f),
                out Image superFill,
                out GameObject superReadyVisual);

            Text superText = CreateText(
                superRoot.transform,
                "SuperChargeText",
                "0%",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                15,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            CreateText(
                superRoot.transform,
                "SuperLabel",
                "SUPER",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(82f, 18f),
                12,
                TextAnchor.UpperCenter,
                new Color(1f, 1f, 1f, 0.7f),
                FontStyle.Bold);

            GameObject hyperRoot = CreateAbilityMeter(
                panel.transform,
                "HyperchargeMeter",
                new Vector2(416f, 24f),
                new Color(0.64f, 0.20f, 1f, 0.36f),
                out Image hyperchargeFill,
                out GameObject hyperchargeActiveVisual);

            Text hyperchargeText = CreateText(
                hyperRoot.transform,
                "HyperchargeText",
                "0%",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                14,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            CreateText(
                hyperRoot.transform,
                "HyperchargeLabel",
                "HYPER",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(82f, 18f),
                12,
                TextAnchor.UpperCenter,
                new Color(1f, 1f, 1f, 0.7f),
                FontStyle.Bold);

            hyperRoot.SetActive(false);

            GameObject gadgetRoot = CreateAbilityMeter(
                panel.transform,
                "GadgetMeter",
                new Vector2(506f, 24f),
                new Color(0.10f, 0.72f, 0.42f, 0.35f),
                out Image gadgetCooldownOverlay,
                out GameObject gadgetReadyVisual);

            gadgetCooldownOverlay.color = new Color(0f, 0f, 0f, 0.58f);
            gadgetCooldownOverlay.fillAmount = 0f;

            Text gadgetCharges = CreateText(
                gadgetRoot.transform,
                "GadgetChargesText",
                "0",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                22,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            CreateText(
                gadgetRoot.transform,
                "GadgetLabel",
                "GADGET",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(88f, 18f),
                12,
                TextAnchor.UpperCenter,
                new Color(1f, 1f, 1f, 0.7f),
                FontStyle.Bold);

            gadgetRoot.SetActive(false);

            PlayerHUD playerHUD = controller.AddComponent<PlayerHUD>();
            playerHUD.BindAmmoWidgets(ammoSlots, ammoSlotRoots, null, ammoCount);
            playerHUD.BindSuperWidgets(superFill, superReadyVisual, null, superText);
            playerHUD.BindHyperchargeWidgets(hyperRoot, hyperchargeFill, hyperchargeActiveVisual, null, hyperchargeText);
            playerHUD.BindGadgetWidgets(gadgetRoot, gadgetCooldownOverlay, null, gadgetCharges, gadgetReadyVisual);
            playerHUD.BindCarrierWidgets(carrierBadge, null, carriedGemCount);
        }

        private static void CreateCombatFeed(Transform parent)
        {
            GameObject controller = CreateController(parent, "CombatFeedController");
            GameObject panel = CreateRectPanel(
                parent,
                "CombatFeedPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -84f),
                new Vector2(570f, 198f),
                new Color(0f, 0f, 0f, 0.28f));

            Text feedText = CreateText(
                panel.transform,
                "CombatFeedText",
                string.Empty,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -10f),
                new Vector2(542f, 176f),
                19,
                TextAnchor.UpperLeft,
                new Color(1f, 1f, 1f, 0.95f),
                FontStyle.Bold);

            feedText.horizontalOverflow = HorizontalWrapMode.Wrap;
            feedText.verticalOverflow = VerticalWrapMode.Truncate;

            CombatLogHUD combatLog = controller.AddComponent<CombatLogHUD>();
            combatLog.BindTextTargets(null, feedText, panel);
        }

        private static void CreateCountdownOverlay(Transform parent)
        {
            GameObject controller = CreateController(parent, "CountdownOverlayController");
            GameObject root = CreatePanel(
                parent,
                "CountdownOverlay",
                new Color(0f, 0f, 0f, 0.16f));

            Text countdownText = CreateText(
                root.transform,
                "CountdownText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(680f, 180f),
                96,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

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
                new Vector2(0f, -150f),
                new Vector2(760f, 86f),
                new Color(0f, 0f, 0f, 0.34f));

            Text countdownText = CreateText(
                root.transform,
                "GemGrabCountdownText",
                string.Empty,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                48,
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
                new Color(0f, 0f, 0f, 0.56f));

            Text titleText = CreateText(
                root.transform,
                "DeathTitleText",
                "You died",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 72f),
                new Vector2(720f, 80f),
                46,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            Text countdownText = CreateText(
                root.transform,
                "RespawnCountdownText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f),
                new Vector2(720f, 54f),
                28,
                TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.92f),
                FontStyle.Bold);

            Text killerText = CreateText(
                root.transform,
                "KilledByText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -44f),
                new Vector2(720f, 44f),
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
                new Vector2(64f, 64f),
                baseColor);

            fill = CreateFilledImage(
                root.transform,
                name + "Fill",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 1f, 1f, 0.32f),
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
                new Color(1f, 0.96f, 0.44f, 0.22f));

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
