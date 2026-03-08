#if UNITY_EDITOR
// Run from Unity Editor menu: AshenThrone → Generate Scene UI
// Populates each of the 6 scenes with proper UI panel hierarchies,
// HUD elements, backgrounds, and interactive buttons.
// Safe to re-run — clears existing UI before rebuilding.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenThrone.Editor
{
    public static class SceneUIGenerator
    {
        [MenuItem("AshenThrone/Generate Scene UI")]
        public static void GenerateAll()
        {
            SetupBootScene();
            SetupLobbyScene();
            SetupCombatScene();
            SetupEmpireScene();
            SetupWorldMapScene();
            SetupAllianceScene();
            Debug.Log("[SceneUIGenerator] All 6 scenes populated with UI.");
        }

        // ---------------------------------------------------------------
        // Boot Scene — loading screen
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Scene UI/Boot")]
        public static void SetupBootScene()
        {
            var scene = OpenScene("Boot");
            var canvas = FindOrCreateCanvas(scene);

            // Dark background
            var bg = AddPanel(canvas, "Background", new Color(0.05f, 0.05f, 0.08f, 1f));
            StretchToParent(bg);

            // Title
            var title = AddText(canvas, "Title", "ASHEN THRONE", 48, TextAnchor.MiddleCenter);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.55f);
            titleRect.anchorMax = new Vector2(0.9f, 0.75f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            title.GetComponent<Text>().color = new Color(0.85f, 0.75f, 0.3f);

            // Subtitle
            var sub = AddText(canvas, "Subtitle", "A Dark Fantasy Strategy RPG", 18, TextAnchor.MiddleCenter);
            var subRect = sub.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.2f, 0.48f);
            subRect.anchorMax = new Vector2(0.8f, 0.55f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
            sub.GetComponent<Text>().color = new Color(0.7f, 0.65f, 0.55f);

            // Loading bar background
            var barBg = AddPanel(canvas, "LoadingBarBg", new Color(0.2f, 0.2f, 0.25f, 1f));
            var barBgRect = barBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.2f, 0.3f);
            barBgRect.anchorMax = new Vector2(0.8f, 0.34f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;

            // Loading bar fill
            var barFill = AddPanel(barBg, "LoadingBarFill", new Color(0.85f, 0.75f, 0.3f, 1f));
            var fillRect = barFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.65f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Loading text
            var loadText = AddText(canvas, "LoadingText", "Loading...", 14, TextAnchor.MiddleCenter);
            var loadRect = loadText.GetComponent<RectTransform>();
            loadRect.anchorMin = new Vector2(0.3f, 0.22f);
            loadRect.anchorMax = new Vector2(0.7f, 0.3f);
            loadRect.offsetMin = Vector2.zero;
            loadRect.offsetMax = Vector2.zero;

            // Version
            var ver = AddText(canvas, "VersionLabel", "v0.1.0-alpha", 12, TextAnchor.LowerRight);
            var verRect = ver.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0.7f, 0);
            verRect.anchorMax = new Vector2(1, 0.05f);
            verRect.offsetMin = new Vector2(0, 4);
            verRect.offsetMax = new Vector2(-8, 0);
            ver.GetComponent<Text>().color = new Color(0.4f, 0.4f, 0.45f);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Boot scene UI populated");
        }

        // ---------------------------------------------------------------
        // Lobby Scene — main menu
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Scene UI/Lobby")]
        public static void SetupLobbyScene()
        {
            var scene = OpenScene("Lobby");
            var canvas = FindOrCreateCanvas(scene);

            // Background
            var bg = AddPanel(canvas, "Background", new Color(0.08f, 0.06f, 0.1f, 1f));
            StretchToParent(bg);

            // Title
            var title = AddText(canvas, "GameTitle", "ASHEN THRONE", 56, TextAnchor.MiddleCenter);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.7f);
            titleRect.anchorMax = new Vector2(0.9f, 0.9f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            title.GetComponent<Text>().color = new Color(0.85f, 0.75f, 0.3f);

            // Button panel
            var btnPanel = AddPanel(canvas, "ButtonPanel", new Color(0, 0, 0, 0));
            var btnRect = btnPanel.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.3f, 0.2f);
            btnRect.anchorMax = new Vector2(0.7f, 0.65f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            var layout = btnPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddMenuButton(btnPanel, "PlayButton", "PLAY", new Color(0.2f, 0.5f, 0.85f));
            AddMenuButton(btnPanel, "HeroesButton", "HEROES", new Color(0.45f, 0.15f, 0.55f));
            AddMenuButton(btnPanel, "AllianceButton", "ALLIANCE", new Color(0.2f, 0.65f, 0.3f));
            AddMenuButton(btnPanel, "ShopButton", "SHOP", new Color(0.85f, 0.75f, 0.3f));
            AddMenuButton(btnPanel, "SettingsButton", "SETTINGS", new Color(0.4f, 0.4f, 0.45f));

            // Player info bar
            var infoBar = AddPanel(canvas, "PlayerInfoBar", new Color(0.1f, 0.1f, 0.15f, 0.9f));
            var infoRect = infoBar.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 0.93f);
            infoRect.anchorMax = new Vector2(1, 1);
            infoRect.offsetMin = Vector2.zero;
            infoRect.offsetMax = Vector2.zero;

            var playerName = AddText(infoBar, "PlayerName", "Commander", 16, TextAnchor.MiddleLeft);
            var pnRect = playerName.GetComponent<RectTransform>();
            pnRect.anchorMin = new Vector2(0.02f, 0);
            pnRect.anchorMax = new Vector2(0.3f, 1);
            pnRect.offsetMin = Vector2.zero;
            pnRect.offsetMax = Vector2.zero;

            var levelLabel = AddText(infoBar, "LevelLabel", "Lv. 1", 14, TextAnchor.MiddleRight);
            var lvRect = levelLabel.GetComponent<RectTransform>();
            lvRect.anchorMin = new Vector2(0.85f, 0);
            lvRect.anchorMax = new Vector2(0.98f, 1);
            lvRect.offsetMin = Vector2.zero;
            lvRect.offsetMax = Vector2.zero;

            SaveScene();
            Debug.Log("[SceneUIGenerator] Lobby scene UI populated");
        }

        // ---------------------------------------------------------------
        // Combat Scene — full battle HUD
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Scene UI/Combat")]
        public static void SetupCombatScene()
        {
            var scene = OpenScene("Combat");
            var canvas = FindOrCreateCanvas(scene);

            // --- Top Bar ---
            var topBar = AddPanel(canvas, "TopBar", new Color(0.1f, 0.1f, 0.15f, 0.85f));
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 0.92f);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.offsetMin = Vector2.zero;
            topRect.offsetMax = Vector2.zero;

            var phaseLabel = AddText(topBar, "PhaseLabel", "ACTION PHASE", 20, TextAnchor.MiddleCenter);
            StretchToParent(phaseLabel);

            // --- Turn Order (right side) ---
            var turnOrder = AddPanel(canvas, "TurnOrderPanel", new Color(0.1f, 0.1f, 0.15f, 0.7f));
            var toRect = turnOrder.GetComponent<RectTransform>();
            toRect.anchorMin = new Vector2(0.88f, 0.3f);
            toRect.anchorMax = new Vector2(0.98f, 0.9f);
            toRect.offsetMin = Vector2.zero;
            toRect.offsetMax = Vector2.zero;

            var toTitle = AddText(turnOrder, "TurnOrderTitle", "TURN", 10, TextAnchor.MiddleCenter);
            var toTitleRect = toTitle.GetComponent<RectTransform>();
            toTitleRect.anchorMin = new Vector2(0, 0.9f);
            toTitleRect.anchorMax = new Vector2(1, 1);
            toTitleRect.offsetMin = Vector2.zero;
            toTitleRect.offsetMax = Vector2.zero;

            var tokenContainer = AddPanel(turnOrder, "TokenContainer", new Color(0, 0, 0, 0));
            var tcRect = tokenContainer.GetComponent<RectTransform>();
            tcRect.anchorMin = new Vector2(0.05f, 0.05f);
            tcRect.anchorMax = new Vector2(0.95f, 0.88f);
            tcRect.offsetMin = Vector2.zero;
            tcRect.offsetMax = Vector2.zero;
            var vlg = tokenContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.UpperCenter;

            // --- Player hero status (left side) ---
            var playerStatus = AddPanel(canvas, "PlayerHeroStatus", new Color(0, 0, 0, 0));
            var psRect = playerStatus.GetComponent<RectTransform>();
            psRect.anchorMin = new Vector2(0.01f, 0.55f);
            psRect.anchorMax = new Vector2(0.12f, 0.9f);
            psRect.offsetMin = Vector2.zero;
            psRect.offsetMax = Vector2.zero;
            var psLayout = playerStatus.AddComponent<VerticalLayoutGroup>();
            psLayout.spacing = 6;

            for (int i = 0; i < 3; i++)
            {
                var heroPanel = AddPanel(playerStatus, $"PlayerHero_{i}", new Color(0.15f, 0.15f, 0.2f, 0.8f));
                var hpLayout = heroPanel.AddComponent<LayoutElement>();
                hpLayout.preferredHeight = 32;

                var portrait = AddPanel(heroPanel, "Portrait", new Color(0.3f + i * 0.1f, 0.3f, 0.4f));
                var porRect = portrait.GetComponent<RectTransform>();
                porRect.anchorMin = new Vector2(0, 0);
                porRect.anchorMax = new Vector2(0.35f, 1);
                porRect.offsetMin = Vector2.zero;
                porRect.offsetMax = Vector2.zero;

                var hpBar = AddPanel(heroPanel, "HealthBar", new Color(0.3f, 0.05f, 0.05f));
                var hpBarRect = hpBar.GetComponent<RectTransform>();
                hpBarRect.anchorMin = new Vector2(0.38f, 0.2f);
                hpBarRect.anchorMax = new Vector2(0.98f, 0.5f);
                hpBarRect.offsetMin = Vector2.zero;
                hpBarRect.offsetMax = Vector2.zero;

                var hpFill = AddPanel(hpBar, "Fill", new Color(0.2f, 0.8f, 0.2f));
                var hpFillRect = hpFill.GetComponent<RectTransform>();
                hpFillRect.anchorMin = Vector2.zero;
                hpFillRect.anchorMax = new Vector2(0.8f - i * 0.15f, 1f);
                hpFillRect.offsetMin = Vector2.zero;
                hpFillRect.offsetMax = Vector2.zero;

                var hpText = AddText(heroPanel, "HPText", $"{800 - i * 200}/1000", 8, TextAnchor.MiddleRight);
                var hpTextRect = hpText.GetComponent<RectTransform>();
                hpTextRect.anchorMin = new Vector2(0.38f, 0.55f);
                hpTextRect.anchorMax = new Vector2(0.98f, 0.95f);
                hpTextRect.offsetMin = Vector2.zero;
                hpTextRect.offsetMax = Vector2.zero;
            }

            // --- Enemy hero status ---
            var enemyStatus = AddPanel(canvas, "EnemyHeroStatus", new Color(0, 0, 0, 0));
            var esRect = enemyStatus.GetComponent<RectTransform>();
            esRect.anchorMin = new Vector2(0.76f, 0.55f);
            esRect.anchorMax = new Vector2(0.87f, 0.9f);
            esRect.offsetMin = Vector2.zero;
            esRect.offsetMax = Vector2.zero;
            var esLayout = enemyStatus.AddComponent<VerticalLayoutGroup>();
            esLayout.spacing = 6;

            for (int i = 0; i < 3; i++)
            {
                var ePanel = AddPanel(enemyStatus, $"EnemyHero_{i}", new Color(0.2f, 0.12f, 0.12f, 0.8f));
                ePanel.AddComponent<LayoutElement>().preferredHeight = 32;

                var ePor = AddPanel(ePanel, "Portrait", new Color(0.5f, 0.2f + i * 0.05f, 0.2f));
                var ePorRect = ePor.GetComponent<RectTransform>();
                ePorRect.anchorMin = new Vector2(0, 0);
                ePorRect.anchorMax = new Vector2(0.35f, 1);
                ePorRect.offsetMin = Vector2.zero;
                ePorRect.offsetMax = Vector2.zero;

                var eBar = AddPanel(ePanel, "HealthBar", new Color(0.3f, 0.05f, 0.05f));
                var eBarRect = eBar.GetComponent<RectTransform>();
                eBarRect.anchorMin = new Vector2(0.38f, 0.2f);
                eBarRect.anchorMax = new Vector2(0.98f, 0.5f);
                eBarRect.offsetMin = Vector2.zero;
                eBarRect.offsetMax = Vector2.zero;

                var eFill = AddPanel(eBar, "Fill", new Color(0.8f, 0.2f, 0.2f));
                StretchToParent(eFill);
            }

            // --- Energy Display ---
            var energyPanel = AddPanel(canvas, "EnergyPanel", new Color(0.1f, 0.1f, 0.15f, 0.8f));
            var enRect = energyPanel.GetComponent<RectTransform>();
            enRect.anchorMin = new Vector2(0.01f, 0.15f);
            enRect.anchorMax = new Vector2(0.12f, 0.22f);
            enRect.offsetMin = Vector2.zero;
            enRect.offsetMax = Vector2.zero;

            var enLabel = AddText(energyPanel, "EnergyLabel", "ENERGY", 9, TextAnchor.MiddleLeft);
            var enLabelRect = enLabel.GetComponent<RectTransform>();
            enLabelRect.anchorMin = new Vector2(0.05f, 0.5f);
            enLabelRect.anchorMax = new Vector2(0.45f, 1);
            enLabelRect.offsetMin = Vector2.zero;
            enLabelRect.offsetMax = Vector2.zero;

            var orbContainer = AddPanel(energyPanel, "OrbContainer", new Color(0, 0, 0, 0));
            var orbRect = orbContainer.GetComponent<RectTransform>();
            orbRect.anchorMin = new Vector2(0.05f, 0.05f);
            orbRect.anchorMax = new Vector2(0.95f, 0.5f);
            orbRect.offsetMin = Vector2.zero;
            orbRect.offsetMax = Vector2.zero;
            var orbLayout = orbContainer.AddComponent<HorizontalLayoutGroup>();
            orbLayout.spacing = 4;

            for (int i = 0; i < 4; i++)
            {
                var orb = AddPanel(orbContainer, $"Orb_{i}", i < 3
                    ? new Color(0.3f, 0.6f, 1f)
                    : new Color(0.15f, 0.15f, 0.2f));
                orb.AddComponent<LayoutElement>().preferredWidth = 16;
            }

            // --- Card Hand ---
            var cardHand = AddPanel(canvas, "CardHandPanel", new Color(0.08f, 0.08f, 0.12f, 0.85f));
            var chRect = cardHand.GetComponent<RectTransform>();
            chRect.anchorMin = new Vector2(0.15f, 0);
            chRect.anchorMax = new Vector2(0.85f, 0.18f);
            chRect.offsetMin = Vector2.zero;
            chRect.offsetMax = Vector2.zero;

            var cardContainer = AddPanel(cardHand, "CardContainer", new Color(0, 0, 0, 0));
            var ccRect = cardContainer.GetComponent<RectTransform>();
            ccRect.anchorMin = new Vector2(0.02f, 0.08f);
            ccRect.anchorMax = new Vector2(0.98f, 0.92f);
            ccRect.offsetMin = Vector2.zero;
            ccRect.offsetMax = Vector2.zero;
            var hlg = cardContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;

            for (int i = 0; i < 5; i++)
            {
                var slot = AddPanel(cardContainer, $"CardSlot_{i}", new Color(0.15f, 0.12f, 0.1f));
                var le = slot.AddComponent<LayoutElement>();
                le.preferredWidth = 100;
                le.preferredHeight = 140;

                var slotCost = AddText(slot, "Cost", $"{i + 1}", 16, TextAnchor.MiddleCenter);
                var scRect = slotCost.GetComponent<RectTransform>();
                scRect.anchorMin = new Vector2(0, 0.75f);
                scRect.anchorMax = new Vector2(0.3f, 1);
                scRect.offsetMin = Vector2.zero;
                scRect.offsetMax = Vector2.zero;
                slotCost.GetComponent<Text>().color = new Color(0.3f, 0.6f, 1f);

                var slotName = AddText(slot, "Name", $"Card {i + 1}", 10, TextAnchor.MiddleCenter);
                var snRect = slotName.GetComponent<RectTransform>();
                snRect.anchorMin = new Vector2(0, 0);
                snRect.anchorMax = new Vector2(1, 0.2f);
                snRect.offsetMin = Vector2.zero;
                snRect.offsetMax = Vector2.zero;
            }

            // --- End Turn button ---
            var endTurn = AddPanel(canvas, "EndTurnButton", new Color(0.7f, 0.25f, 0.15f));
            var etRect = endTurn.GetComponent<RectTransform>();
            etRect.anchorMin = new Vector2(0.87f, 0.01f);
            etRect.anchorMax = new Vector2(0.99f, 0.08f);
            etRect.offsetMin = Vector2.zero;
            etRect.offsetMax = Vector2.zero;
            endTurn.AddComponent<Button>();

            var etLabel = AddText(endTurn, "Label", "END TURN", 12, TextAnchor.MiddleCenter);
            StretchToParent(etLabel);

            // --- Victory/Defeat panels (hidden) ---
            var victoryPanel = AddPanel(canvas, "VictoryPanel", new Color(0.1f, 0.15f, 0.1f, 0.95f));
            StretchToParent(victoryPanel);
            var vicTitle = AddText(victoryPanel, "VictoryTitle", "VICTORY!", 64, TextAnchor.MiddleCenter);
            var vtRect = vicTitle.GetComponent<RectTransform>();
            vtRect.anchorMin = new Vector2(0.1f, 0.5f);
            vtRect.anchorMax = new Vector2(0.9f, 0.75f);
            vtRect.offsetMin = Vector2.zero;
            vtRect.offsetMax = Vector2.zero;
            vicTitle.GetComponent<Text>().color = new Color(0.85f, 0.75f, 0.3f);
            victoryPanel.SetActive(false);

            var defeatPanel = AddPanel(canvas, "DefeatPanel", new Color(0.15f, 0.05f, 0.05f, 0.95f));
            StretchToParent(defeatPanel);
            var defTitle = AddText(defeatPanel, "DefeatTitle", "DEFEAT", 64, TextAnchor.MiddleCenter);
            var dtRect = defTitle.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0.1f, 0.5f);
            dtRect.anchorMax = new Vector2(0.9f, 0.75f);
            dtRect.offsetMin = Vector2.zero;
            dtRect.offsetMax = Vector2.zero;
            defTitle.GetComponent<Text>().color = new Color(0.8f, 0.2f, 0.2f);
            defeatPanel.SetActive(false);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Combat scene UI populated (HUD + card hand + hero status)");
        }

        // ---------------------------------------------------------------
        // Empire Scene — city builder HUD
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Scene UI/Empire")]
        public static void SetupEmpireScene()
        {
            var scene = OpenScene("Empire");
            var canvas = FindOrCreateCanvas(scene);

            // --- Resource HUD ---
            var resBar = AddPanel(canvas, "ResourceHUD", new Color(0.1f, 0.1f, 0.15f, 0.9f));
            var rbRect = resBar.GetComponent<RectTransform>();
            rbRect.anchorMin = new Vector2(0, 0.93f);
            rbRect.anchorMax = new Vector2(1, 1);
            rbRect.offsetMin = Vector2.zero;
            rbRect.offsetMax = Vector2.zero;

            var resLayout = resBar.AddComponent<HorizontalLayoutGroup>();
            resLayout.spacing = 20;
            resLayout.padding = new RectOffset(16, 16, 4, 4);
            resLayout.childAlignment = TextAnchor.MiddleLeft;

            var resData = new (string name, string icon, Color color, string amount)[]
            {
                ("Stone", "S", new Color(0.6f, 0.55f, 0.5f), "12,500"),
                ("Iron", "I", new Color(0.45f, 0.45f, 0.5f), "8,200"),
                ("Grain", "G", new Color(0.85f, 0.75f, 0.2f), "15,000"),
                ("Arcane", "A", new Color(0.5f, 0.2f, 0.8f), "3,400"),
            };

            foreach (var (rName, icon, color, amount) in resData)
            {
                var resGroup = AddPanel(resBar, $"Res_{rName}", new Color(0, 0, 0, 0));
                var rgLayout = resGroup.AddComponent<HorizontalLayoutGroup>();
                rgLayout.spacing = 4;
                rgLayout.childAlignment = TextAnchor.MiddleLeft;
                resGroup.AddComponent<LayoutElement>().preferredWidth = 140;

                var resIcon = AddPanel(resGroup, "Icon", color);
                resIcon.AddComponent<LayoutElement>().preferredWidth = 24;

                var resLabel = AddText(resGroup, "Amount", amount, 14, TextAnchor.MiddleLeft);
                resLabel.AddComponent<LayoutElement>().flexibleWidth = 1;
            }

            // --- Toolbar ---
            var toolbar = AddPanel(canvas, "Toolbar", new Color(0.1f, 0.1f, 0.15f, 0.9f));
            var tbRect = toolbar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0.15f, 0);
            tbRect.anchorMax = new Vector2(0.85f, 0.08f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;
            var tbLayout = toolbar.AddComponent<HorizontalLayoutGroup>();
            tbLayout.spacing = 12;
            tbLayout.childAlignment = TextAnchor.MiddleCenter;
            tbLayout.padding = new RectOffset(8, 8, 4, 4);

            AddToolbarButton(toolbar, "BuildBtn", "BUILD", new Color(0.55f, 0.4f, 0.3f));
            AddToolbarButton(toolbar, "ResearchBtn", "RESEARCH", new Color(0.3f, 0.4f, 0.6f));
            AddToolbarButton(toolbar, "HeroesBtn", "HEROES", new Color(0.45f, 0.15f, 0.55f));
            AddToolbarButton(toolbar, "QuestsBtn", "QUESTS", new Color(0.2f, 0.65f, 0.3f));
            AddToolbarButton(toolbar, "BattleBtn", "BATTLE", new Color(0.7f, 0.25f, 0.15f));

            // --- Build Queue Overlay ---
            var buildQueue = AddPanel(canvas, "BuildQueueOverlay", new Color(0.12f, 0.12f, 0.16f, 0.9f));
            var bqRect = buildQueue.GetComponent<RectTransform>();
            bqRect.anchorMin = new Vector2(0.78f, 0.1f);
            bqRect.anchorMax = new Vector2(0.99f, 0.55f);
            bqRect.offsetMin = Vector2.zero;
            bqRect.offsetMax = Vector2.zero;

            var bqTitle = AddText(buildQueue, "Title", "BUILD QUEUE", 14, TextAnchor.MiddleCenter);
            var bqtRect = bqTitle.GetComponent<RectTransform>();
            bqtRect.anchorMin = new Vector2(0, 0.88f);
            bqtRect.anchorMax = new Vector2(1, 1);
            bqtRect.offsetMin = Vector2.zero;
            bqtRect.offsetMax = Vector2.zero;

            for (int i = 0; i < 2; i++)
            {
                var slot = AddPanel(buildQueue, $"QueueSlot_{i}", new Color(0.18f, 0.18f, 0.22f));
                var slotRect = slot.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.05f, 0.55f - i * 0.38f);
                slotRect.anchorMax = new Vector2(0.95f, 0.85f - i * 0.38f);
                slotRect.offsetMin = Vector2.zero;
                slotRect.offsetMax = Vector2.zero;

                AddText(slot, "BuildingName", i == 0 ? "Barracks Lv.3" : "Empty", 11, TextAnchor.MiddleLeft);
                var timerLabel = AddText(slot, "Timer", i == 0 ? "2:34:15" : "--:--", 10, TextAnchor.LowerRight);
                var timerRect = timerLabel.GetComponent<RectTransform>();
                timerRect.anchorMin = new Vector2(0.5f, 0);
                timerRect.anchorMax = Vector2.one;
                timerRect.offsetMin = Vector2.zero;
                timerRect.offsetMax = Vector2.zero;
            }

            SaveScene();
            Debug.Log("[SceneUIGenerator] Empire scene UI populated (Resource HUD + toolbar + build queue)");
        }

        // ---------------------------------------------------------------
        // WorldMap Scene
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Scene UI/World Map")]
        public static void SetupWorldMapScene()
        {
            var scene = OpenScene("WorldMap");
            var canvas = FindOrCreateCanvas(scene);

            var bg = AddPanel(canvas, "MapBackground", new Color(0.12f, 0.15f, 0.1f, 1f));
            StretchToParent(bg);

            var title = AddText(canvas, "MapTitle", "WORLD MAP", 24, TextAnchor.MiddleCenter);
            var tRect = title.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.3f, 0.92f);
            tRect.anchorMax = new Vector2(0.7f, 1);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var infoPanel = AddPanel(canvas, "TerritoryInfoPanel", new Color(0.1f, 0.1f, 0.15f, 0.9f));
            var ipRect = infoPanel.GetComponent<RectTransform>();
            ipRect.anchorMin = new Vector2(0.01f, 0.01f);
            ipRect.anchorMax = new Vector2(0.25f, 0.3f);
            ipRect.offsetMin = Vector2.zero;
            ipRect.offsetMax = Vector2.zero;

            AddText(infoPanel, "TerritoryName", "Iron Wastes", 16, TextAnchor.UpperLeft);
            var ownerLabel = AddText(infoPanel, "OwnerLabel", "Controlled by: Iron Legion Alliance", 11, TextAnchor.MiddleLeft);
            var olRect = ownerLabel.GetComponent<RectTransform>();
            olRect.anchorMin = new Vector2(0.05f, 0.4f);
            olRect.anchorMax = new Vector2(0.95f, 0.6f);
            olRect.offsetMin = Vector2.zero;
            olRect.offsetMax = Vector2.zero;

            AddMenuButton(infoPanel, "AttackBtn", "ATTACK", new Color(0.7f, 0.25f, 0.15f));

            var backBtn = AddPanel(canvas, "BackButton", new Color(0.3f, 0.3f, 0.35f));
            var bbRect = backBtn.GetComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0.01f, 0.93f);
            bbRect.anchorMax = new Vector2(0.1f, 0.99f);
            bbRect.offsetMin = Vector2.zero;
            bbRect.offsetMax = Vector2.zero;
            backBtn.AddComponent<Button>();
            var bbLabel = AddText(backBtn, "Label", "< BACK", 12, TextAnchor.MiddleCenter);
            StretchToParent(bbLabel);

            SaveScene();
            Debug.Log("[SceneUIGenerator] WorldMap scene UI populated");
        }

        // ---------------------------------------------------------------
        // Alliance Scene
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Scene UI/Alliance")]
        public static void SetupAllianceScene()
        {
            var scene = OpenScene("Alliance");
            var canvas = FindOrCreateCanvas(scene);

            var bg = AddPanel(canvas, "Background", new Color(0.08f, 0.08f, 0.12f, 1f));
            StretchToParent(bg);

            // Tab bar
            var tabBar = AddPanel(canvas, "TabBar", new Color(0.12f, 0.12f, 0.16f, 0.9f));
            var tabRect = tabBar.GetComponent<RectTransform>();
            tabRect.anchorMin = new Vector2(0, 0.9f);
            tabRect.anchorMax = new Vector2(1, 1);
            tabRect.offsetMin = Vector2.zero;
            tabRect.offsetMax = Vector2.zero;
            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4;
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.padding = new RectOffset(8, 8, 4, 4);

            AddToolbarButton(tabBar, "ChatTab", "CHAT", new Color(0.2f, 0.5f, 0.85f));
            AddToolbarButton(tabBar, "MembersTab", "MEMBERS", new Color(0.45f, 0.15f, 0.55f));
            AddToolbarButton(tabBar, "WarTab", "WAR", new Color(0.7f, 0.25f, 0.15f));
            AddToolbarButton(tabBar, "TerritoryTab", "TERRITORY", new Color(0.2f, 0.65f, 0.3f));

            // Chat panel
            var chatPanel = AddPanel(canvas, "ChatPanel", new Color(0.1f, 0.1f, 0.14f, 0.8f));
            var cpRect = chatPanel.GetComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(0.02f, 0.1f);
            cpRect.anchorMax = new Vector2(0.98f, 0.88f);
            cpRect.offsetMin = Vector2.zero;
            cpRect.offsetMax = Vector2.zero;

            var msgArea = AddPanel(chatPanel, "MessageArea", new Color(0.08f, 0.08f, 0.1f));
            var maRect = msgArea.GetComponent<RectTransform>();
            maRect.anchorMin = new Vector2(0.01f, 0.12f);
            maRect.anchorMax = new Vector2(0.99f, 0.98f);
            maRect.offsetMin = Vector2.zero;
            maRect.offsetMax = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                var msg = AddText(msgArea, $"Msg_{i}", $"[Player_{i}]: Sample alliance chat message {i + 1}", 12, TextAnchor.UpperLeft);
                var mRect = msg.GetComponent<RectTransform>();
                mRect.anchorMin = new Vector2(0.02f, 0.75f - i * 0.22f);
                mRect.anchorMax = new Vector2(0.98f, 0.95f - i * 0.22f);
                mRect.offsetMin = Vector2.zero;
                mRect.offsetMax = Vector2.zero;
                msg.GetComponent<Text>().color = new Color(0.7f, 0.7f, 0.75f);
            }

            var inputArea = AddPanel(chatPanel, "InputArea", new Color(0.15f, 0.15f, 0.2f));
            var iaRect = inputArea.GetComponent<RectTransform>();
            iaRect.anchorMin = new Vector2(0.01f, 0.01f);
            iaRect.anchorMax = new Vector2(0.99f, 0.1f);
            iaRect.offsetMin = Vector2.zero;
            iaRect.offsetMax = Vector2.zero;

            var placeholder = AddText(inputArea, "Placeholder", "Type a message...", 13, TextAnchor.MiddleLeft);
            StretchToParent(placeholder);
            placeholder.GetComponent<Text>().color = new Color(0.4f, 0.4f, 0.45f);

            var backBtn = AddPanel(canvas, "BackButton", new Color(0.3f, 0.3f, 0.35f));
            var bbkRect = backBtn.GetComponent<RectTransform>();
            bbkRect.anchorMin = new Vector2(0.01f, 0.01f);
            bbkRect.anchorMax = new Vector2(0.1f, 0.07f);
            bbkRect.offsetMin = Vector2.zero;
            bbkRect.offsetMax = Vector2.zero;
            backBtn.AddComponent<Button>();
            AddText(backBtn, "Label", "< BACK", 12, TextAnchor.MiddleCenter);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Alliance scene UI populated");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static UnityEngine.SceneManagement.Scene OpenScene(string name)
        {
            string path = $"Assets/Scenes/{name}/{name}.unity";
            if (!File.Exists(path))
            {
                Debug.LogError($"[SceneUIGenerator] Scene not found: {path}");
                return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            }
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static GameObject FindOrCreateCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<Canvas>();
                if (existing != null)
                {
                    for (int i = existing.transform.childCount - 1; i >= 0; i--)
                        Object.DestroyImmediate(existing.transform.GetChild(i).gameObject);
                    return existing.gameObject;
                }
            }

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            bool hasES = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<EventSystem>() != null) { hasES = true; break; }
            if (!hasES)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            return canvasGo;
        }

        private static GameObject AddPanel(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static GameObject AddText(GameObject parent, string name, string text, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent.transform, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return go;
        }

        private static void AddMenuButton(GameObject parent, string name, string label, Color color)
        {
            var btn = AddPanel(parent, name, color);
            btn.AddComponent<LayoutElement>().preferredHeight = 48;
            btn.AddComponent<Button>();

            var lbl = AddText(btn, "Label", label, 18, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
        }

        private static void AddToolbarButton(GameObject parent, string name, string label, Color color)
        {
            var btn = AddPanel(parent, name, color);
            var le = btn.AddComponent<LayoutElement>();
            le.preferredHeight = 40;
            le.flexibleWidth = 1;
            btn.AddComponent<Button>();

            var lbl = AddText(btn, "Label", label, 12, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
        }

        private static void StretchToParent(GameObject child)
        {
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SaveScene()
        {
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
#endif
