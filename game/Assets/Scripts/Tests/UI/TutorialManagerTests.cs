using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.UI.Tutorial;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.UI
{
    /// <summary>
    /// Unit tests for TutorialManager.
    /// TutorialStep uses [field: SerializeField] so reflection sets backing fields.
    /// </summary>
    [TestFixture]
    public class TutorialManagerTests
    {
        private GameObject _go;
        private TutorialManager _manager;
        private readonly List<TutorialStep> _createdSteps = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TutorialManagerTest");
            _manager = _go.AddComponent<TutorialManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            foreach (var s in _createdSteps)
                if (s != null) Object.DestroyImmediate(s);
            _createdSteps.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private TutorialStep MakeStep(string id, int index, TutorialAction action = TutorialAction.TapAnywhere,
            bool skippable = true, string highlight = "")
        {
            var step = ScriptableObject.CreateInstance<TutorialStep>();
            SetBacking(step, "StepId", id);
            SetBacking(step, "StepIndex", index);
            SetBacking(step, "InstructionTextKey", $"tut_{id}");
            SetBacking(step, "HighlightTargetTag", highlight);
            SetBacking(step, "RequiredAction", action);
            SetBacking(step, "IsSkippable", skippable);
            SetBacking(step, "VoiceOverClipKey", "");
            _createdSteps.Add(step);
            return step;
        }

        private void InjectSteps(params TutorialStep[] steps)
        {
            _manager.SetSteps(new List<TutorialStep>(steps));
        }

        private static void SetBacking(object target, string propName, object value)
        {
            string name = $"<{propName}>k__BackingField";
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        // -------------------------------------------------------------------
        // Initialize
        // -------------------------------------------------------------------

        [Test]
        public void Initialize_NullSave_StartsAtStepZero()
        {
            var s0 = MakeStep("welcome", 0);
            InjectSteps(s0);
            _manager.Initialize(null);
            Assert.AreEqual(0, _manager.CurrentStepIndex);
            Assert.IsTrue(_manager.IsActive);
            Assert.IsFalse(_manager.IsComplete);
        }

        [Test]
        public void Initialize_NoSteps_MarksComplete()
        {
            _manager.SetSteps(new List<TutorialStep>());
            _manager.Initialize(null);
            Assert.IsTrue(_manager.IsComplete);
            Assert.IsFalse(_manager.IsActive);
        }

        [Test]
        public void Initialize_ResumesFromSave()
        {
            var s0 = MakeStep("welcome", 0);
            var s1 = MakeStep("combat", 1, TutorialAction.PlayCard);
            InjectSteps(s0, s1);
            _manager.Initialize(new TutorialSaveData { LastCompletedStepIndex = 0 });
            Assert.AreEqual(1, _manager.CurrentStepIndex);
            Assert.AreEqual("combat", _manager.CurrentStep.StepId);
        }

        [Test]
        public void Initialize_CompleteSave_StaysComplete()
        {
            var s0 = MakeStep("welcome", 0);
            InjectSteps(s0);
            _manager.Initialize(new TutorialSaveData { IsComplete = true });
            Assert.IsTrue(_manager.IsComplete);
            Assert.IsFalse(_manager.IsActive);
        }

        [Test]
        public void Initialize_AllStepsAlreadyDone_MarksComplete()
        {
            var s0 = MakeStep("welcome", 0);
            InjectSteps(s0);
            _manager.Initialize(new TutorialSaveData { LastCompletedStepIndex = 0 });
            Assert.IsTrue(_manager.IsComplete);
        }

        // -------------------------------------------------------------------
        // ReportAction
        // -------------------------------------------------------------------

        [Test]
        public void ReportAction_MatchingAction_AdvancesStep()
        {
            var s0 = MakeStep("welcome", 0, TutorialAction.TapAnywhere);
            var s1 = MakeStep("combat", 1, TutorialAction.PlayCard);
            InjectSteps(s0, s1);
            _manager.Initialize(null);

            _manager.ReportAction(TutorialAction.TapAnywhere);
            Assert.AreEqual(1, _manager.CurrentStepIndex);
            Assert.AreEqual("combat", _manager.CurrentStep.StepId);
        }

        [Test]
        public void ReportAction_WrongAction_DoesNotAdvance()
        {
            var s0 = MakeStep("welcome", 0, TutorialAction.TapAnywhere);
            InjectSteps(s0);
            _manager.Initialize(null);

            _manager.ReportAction(TutorialAction.PlayCard);
            Assert.AreEqual(0, _manager.CurrentStepIndex);
        }

        [Test]
        public void ReportAction_WhenComplete_IsNoOp()
        {
            var s0 = MakeStep("welcome", 0, TutorialAction.TapAnywhere);
            InjectSteps(s0);
            _manager.Initialize(null);
            _manager.ReportAction(TutorialAction.TapAnywhere); // completes tutorial
            Assert.IsTrue(_manager.IsComplete);

            // Further reports should be no-ops
            _manager.ReportAction(TutorialAction.TapAnywhere);
            Assert.IsTrue(_manager.IsComplete);
        }

        [Test]
        public void ReportAction_LastStep_CompletesTutorial()
        {
            var s0 = MakeStep("only", 0, TutorialAction.BuildBuilding);
            InjectSteps(s0);
            _manager.Initialize(null);

            _manager.ReportAction(TutorialAction.BuildBuilding);
            Assert.IsTrue(_manager.IsComplete);
            Assert.IsFalse(_manager.IsActive);
        }

        [Test]
        public void ReportAction_WhenNotActive_IsNoOp()
        {
            var s0 = MakeStep("welcome", 0);
            InjectSteps(s0);
            // Don't initialize — not active
            _manager.ReportAction(TutorialAction.TapAnywhere);
            Assert.IsFalse(_manager.IsActive);
            Assert.AreEqual(-1, _manager.CurrentStepIndex);
        }

        // -------------------------------------------------------------------
        // SkipCurrentStep
        // -------------------------------------------------------------------

        [Test]
        public void SkipCurrentStep_Skippable_Advances()
        {
            var s0 = MakeStep("welcome", 0, TutorialAction.TapAnywhere, skippable: true);
            var s1 = MakeStep("combat", 1, TutorialAction.PlayCard);
            InjectSteps(s0, s1);
            _manager.Initialize(null);

            bool result = _manager.SkipCurrentStep();
            Assert.IsTrue(result);
            Assert.AreEqual(1, _manager.CurrentStepIndex);
        }

        [Test]
        public void SkipCurrentStep_NotSkippable_ReturnsFalse()
        {
            var s0 = MakeStep("mandatory", 0, TutorialAction.TapAnywhere, skippable: false);
            InjectSteps(s0);
            _manager.Initialize(null);

            bool result = _manager.SkipCurrentStep();
            Assert.IsFalse(result);
            Assert.AreEqual(0, _manager.CurrentStepIndex);
        }

        [Test]
        public void SkipCurrentStep_WhenComplete_ReturnsFalse()
        {
            var s0 = MakeStep("only", 0);
            InjectSteps(s0);
            _manager.Initialize(null);
            _manager.SkipCurrentStep(); // completes
            Assert.IsFalse(_manager.SkipCurrentStep());
        }

        // -------------------------------------------------------------------
        // SkipAll
        // -------------------------------------------------------------------

        [Test]
        public void SkipAll_MarksTutorialComplete()
        {
            var s0 = MakeStep("welcome", 0);
            var s1 = MakeStep("combat", 1);
            InjectSteps(s0, s1);
            _manager.Initialize(null);

            _manager.SkipAll();
            Assert.IsTrue(_manager.IsComplete);
            Assert.IsFalse(_manager.IsActive);
        }

        [Test]
        public void SkipAll_WhenAlreadyComplete_IsNoOp()
        {
            var s0 = MakeStep("only", 0);
            InjectSteps(s0);
            _manager.Initialize(new TutorialSaveData { IsComplete = true });
            _manager.SkipAll(); // should not throw
            Assert.IsTrue(_manager.IsComplete);
        }

        // -------------------------------------------------------------------
        // BuildSaveData
        // -------------------------------------------------------------------

        [Test]
        public void BuildSaveData_ReflectsCurrentState()
        {
            var s0 = MakeStep("welcome", 0, TutorialAction.TapAnywhere);
            var s1 = MakeStep("combat", 1, TutorialAction.PlayCard);
            InjectSteps(s0, s1);
            _manager.Initialize(null);
            _manager.ReportAction(TutorialAction.TapAnywhere); // complete step 0

            var save = _manager.BuildSaveData();
            Assert.AreEqual(0, save.LastCompletedStepIndex);
            Assert.IsFalse(save.IsComplete);
        }

        [Test]
        public void BuildSaveData_WhenComplete_SetsIsComplete()
        {
            var s0 = MakeStep("only", 0, TutorialAction.TapAnywhere);
            InjectSteps(s0);
            _manager.Initialize(null);
            _manager.ReportAction(TutorialAction.TapAnywhere);

            var save = _manager.BuildSaveData();
            Assert.IsTrue(save.IsComplete);
        }

        // -------------------------------------------------------------------
        // CurrentStep
        // -------------------------------------------------------------------

        [Test]
        public void CurrentStep_ReturnsNull_WhenNotActive()
        {
            Assert.IsNull(_manager.CurrentStep);
        }

        [Test]
        public void CurrentStep_ReturnsCorrectStep_WhenActive()
        {
            var s0 = MakeStep("welcome", 0);
            InjectSteps(s0);
            _manager.Initialize(null);
            Assert.IsNotNull(_manager.CurrentStep);
            Assert.AreEqual("welcome", _manager.CurrentStep.StepId);
        }

        // -------------------------------------------------------------------
        // TotalSteps
        // -------------------------------------------------------------------

        [Test]
        public void TotalSteps_ReturnsCount()
        {
            var s0 = MakeStep("a", 0);
            var s1 = MakeStep("b", 1);
            var s2 = MakeStep("c", 2);
            InjectSteps(s0, s1, s2);
            Assert.AreEqual(3, _manager.TotalSteps);
        }

        // -------------------------------------------------------------------
        // Full 8-step walkthrough
        // -------------------------------------------------------------------

        [Test]
        public void FullWalkthrough_8Steps_CompletesSuccessfully()
        {
            var actions = new[]
            {
                TutorialAction.TapAnywhere,
                TutorialAction.PlayCard,
                TutorialAction.BuildBuilding,
                TutorialAction.CollectResource,
                TutorialAction.UpgradeBuilding,
                TutorialAction.RecruitHero,
                TutorialAction.JoinAlliance,
                TutorialAction.CompleteQuest
            };

            var steps = new List<TutorialStep>();
            for (int i = 0; i < 8; i++)
                steps.Add(MakeStep($"step_{i}", i, actions[i]));
            InjectSteps(steps.ToArray());
            _manager.Initialize(null);

            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(i, _manager.CurrentStepIndex, $"Expected step {i}");
                Assert.IsTrue(_manager.IsActive);
                _manager.ReportAction(actions[i]);
            }

            Assert.IsTrue(_manager.IsComplete);
            Assert.IsFalse(_manager.IsActive);
        }
    }
}
