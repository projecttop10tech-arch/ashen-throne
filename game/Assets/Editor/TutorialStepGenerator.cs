#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using AshenThrone.UI.Tutorial;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates 8 TutorialStep ScriptableObject assets for the FTUE tutorial.
    /// Safe to re-run — overwrites existing assets.
    /// </summary>
    public static class TutorialStepGenerator
    {
        private const string OutputPath = "Assets/Data/Tutorial";

        [MenuItem("Ashen Throne/Generate Tutorial Steps")]
        public static void Generate()
        {
            EnsureDirectory();
            int count = 0;

            CreateStep("welcome", 0, "tutorial_welcome",
                "", TutorialAction.TapAnywhere, false, "vo_tutorial_welcome");
            CreateStep("first_combat", 1, "tutorial_first_combat",
                "CardHand", TutorialAction.PlayCard, true, "vo_tutorial_combat");
            CreateStep("build_first", 2, "tutorial_build_first",
                "BuildButton", TutorialAction.BuildBuilding, true, "vo_tutorial_build");
            CreateStep("collect_resources", 3, "tutorial_collect_resources",
                "ResourceCollect", TutorialAction.CollectResource, true, "");
            CreateStep("upgrade_building", 4, "tutorial_upgrade_building",
                "UpgradeButton", TutorialAction.UpgradeBuilding, true, "");
            CreateStep("recruit_hero", 5, "tutorial_recruit_hero",
                "HeroRecruitButton", TutorialAction.RecruitHero, true, "");
            CreateStep("join_alliance", 6, "tutorial_join_alliance",
                "AllianceButton", TutorialAction.JoinAlliance, true, "");
            CreateStep("complete_quest", 7, "tutorial_complete_quest",
                "QuestClaimButton", TutorialAction.CompleteQuest, true, "");
            count = 8;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TutorialStepGenerator] Generated {count} TutorialStep assets in {OutputPath}/");
        }

        private static void CreateStep(
            string stepId, int stepIndex, string instructionTextKey,
            string highlightTargetTag, TutorialAction requiredAction,
            bool isSkippable, string voiceOverClipKey)
        {
            string assetPath = $"{OutputPath}/TutorialStep_{stepId}.asset";
            var step = ScriptableObject.CreateInstance<TutorialStep>();

            AssetDatabase.CreateAsset(step, assetPath);

            // TutorialStep uses [field: SerializeField] with private setters
            var so = new SerializedObject(step);
            SetProperty(so, "<StepId>k__BackingField", stepId);
            SetPropertyInt(so, "<StepIndex>k__BackingField", stepIndex);
            SetProperty(so, "<InstructionTextKey>k__BackingField", instructionTextKey);
            SetProperty(so, "<HighlightTargetTag>k__BackingField", highlightTargetTag);
            SetPropertyEnum(so, "<RequiredAction>k__BackingField", (int)requiredAction);
            SetPropertyBool(so, "<IsSkippable>k__BackingField", isSkippable);
            SetProperty(so, "<VoiceOverClipKey>k__BackingField", voiceOverClipKey);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProperty(SerializedObject so, string fieldName, string value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.stringValue = value;
        }

        private static void SetPropertyInt(SerializedObject so, string fieldName, int value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.intValue = value;
        }

        private static void SetPropertyEnum(SerializedObject so, string fieldName, int value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.enumValueIndex = value;
        }

        private static void SetPropertyBool(SerializedObject so, string fieldName, bool value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.boolValue = value;
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(OutputPath))
                Directory.CreateDirectory(OutputPath);
        }
    }
}
#endif
