using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.UI.Tutorial
{
    // ---------------------------------------------------------------------------
    // Tutorial action enum — maps to game actions the player must perform
    // ---------------------------------------------------------------------------

    /// <summary>Actions the player can perform to complete a tutorial step.</summary>
    public enum TutorialAction
    {
        None,
        TapAnywhere,
        PlayCard,
        BuildBuilding,
        CollectResource,
        UpgradeBuilding,
        RecruitHero,
        JoinAlliance,
        CompleteQuest
    }

    // ---------------------------------------------------------------------------
    // TutorialStep — pure C# definition for a single tutorial step
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Immutable definition of a single tutorial step.
    /// Defined as ScriptableObject so designers can author steps in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialStep", menuName = "AshenThrone/Tutorial/TutorialStep")]
    public class TutorialStep : ScriptableObject
    {
        /// <summary>Unique step identifier (e.g. "welcome", "first_combat").</summary>
        [field: SerializeField] public string StepId { get; private set; } = "";

        /// <summary>Zero-based step index in the sequence.</summary>
        [field: SerializeField] public int StepIndex { get; private set; }

        /// <summary>Localization key for the instruction text.</summary>
        [field: SerializeField] public string InstructionTextKey { get; private set; } = "";

        /// <summary>Optional UI element tag to highlight (e.g. "BuildButton", "CardHand").</summary>
        [field: SerializeField] public string HighlightTargetTag { get; private set; } = "";

        /// <summary>Action the player must complete to advance.</summary>
        [field: SerializeField] public TutorialAction RequiredAction { get; private set; } = TutorialAction.TapAnywhere;

        /// <summary>Whether the player can skip this step.</summary>
        [field: SerializeField] public bool IsSkippable { get; private set; } = true;

        /// <summary>Optional audio clip key for voice-over narration.</summary>
        [field: SerializeField] public string VoiceOverClipKey { get; private set; } = "";
    }

    // ---------------------------------------------------------------------------
    // TutorialSaveData — serializable save state
    // ---------------------------------------------------------------------------

    /// <summary>Serializable tutorial progress for persistence.</summary>
    [Serializable]
    public class TutorialSaveData
    {
        /// <summary>Index of the last completed step. -1 means tutorial not started.</summary>
        public int LastCompletedStepIndex = -1;

        /// <summary>Whether the entire tutorial has been completed or skipped.</summary>
        public bool IsComplete;
    }

    // ---------------------------------------------------------------------------
    // TutorialManager — orchestrates the 8-step interactive tutorial
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages the first-time user experience (FTUE) tutorial.
    ///
    /// Architecture:
    /// - Steps defined as TutorialStep ScriptableObjects, injected via Inspector.
    /// - Progress saved via TutorialSaveData (flushed to PlayFab on step complete/skip).
    /// - Listens to EventBus for completion triggers matching the current step's RequiredAction.
    /// - Fires TutorialStepStartedEvent, TutorialStepCompletedEvent, TutorialCompletedEvent.
    /// - Integrates with ServiceLocator pattern; no direct cross-system references.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private List<TutorialStep> _steps = new();

        private int _currentStepIndex = -1;
        private bool _isComplete;
        private bool _isActive;

        // EventBus subscriptions for step completion triggers
        private EventSubscription _cardPlayedSub;
        private EventSubscription _buildingBuiltSub;
        private EventSubscription _resourceCollectedSub;
        private EventSubscription _buildingUpgradedSub;
        private EventSubscription _heroRecruitedSub;
        private EventSubscription _allianceJoinedSub;
        private EventSubscription _questCompletedSub;

        /// <summary>The step currently being presented to the player, or null if inactive.</summary>
        public TutorialStep CurrentStep =>
            _isActive && _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
                ? _steps[_currentStepIndex]
                : null;

        /// <summary>Zero-based index of the current step. -1 if not started.</summary>
        public int CurrentStepIndex => _currentStepIndex;

        /// <summary>Total number of configured tutorial steps.</summary>
        public int TotalSteps => _steps.Count;

        /// <summary>Whether the tutorial has been completed or fully skipped.</summary>
        public bool IsComplete => _isComplete;

        /// <summary>Whether the tutorial is currently in progress.</summary>
        public bool IsActive => _isActive;

        private void Awake()
        {
            ServiceLocator.Register<TutorialManager>(this);
        }

        private void OnEnable()
        {
            SubscribeToGameEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromGameEvents();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TutorialManager>();
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Initialize the tutorial from saved data. Resumes from last completed step.
        /// Pass null to start fresh.
        /// </summary>
        public void Initialize(TutorialSaveData save)
        {
            if (_steps == null || _steps.Count == 0)
            {
                Debug.LogWarning("[TutorialManager] No tutorial steps configured.");
                _isComplete = true;
                return;
            }

            int lastCompleted = save?.LastCompletedStepIndex ?? -1;
            _isComplete = save?.IsComplete ?? false;

            if (_isComplete)
            {
                _isActive = false;
                _currentStepIndex = _steps.Count; // past end
                return;
            }

            // Resume from the step after the last completed one
            _currentStepIndex = lastCompleted + 1;

            if (_currentStepIndex >= _steps.Count)
            {
                MarkTutorialComplete();
                return;
            }

            _isActive = true;
            FireStepStarted();
        }

        /// <summary>
        /// Inject tutorial steps programmatically (for testing or dynamic content).
        /// Replaces any Inspector-assigned steps.
        /// </summary>
        public void SetSteps(List<TutorialStep> steps)
        {
            _steps = steps ?? new List<TutorialStep>();
        }

        /// <summary>
        /// Report that the player performed an action. If it matches the current step's
        /// RequiredAction, advances the tutorial.
        /// </summary>
        public void ReportAction(TutorialAction action)
        {
            if (!_isActive || _isComplete) return;
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            TutorialStep step = _steps[_currentStepIndex];
            if (step.RequiredAction != action) return;

            CompleteCurrentStep();
        }

        /// <summary>
        /// Skip the current step. Only allowed if the step is marked skippable.
        /// Returns true if the skip was performed.
        /// </summary>
        public bool SkipCurrentStep()
        {
            if (!_isActive || _isComplete) return false;
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return false;

            TutorialStep step = _steps[_currentStepIndex];
            if (!step.IsSkippable) return false;

            CompleteCurrentStep();
            return true;
        }

        /// <summary>
        /// Skip the entire remaining tutorial. Marks as complete immediately.
        /// </summary>
        public void SkipAll()
        {
            if (_isComplete) return;
            MarkTutorialComplete();
        }

        /// <summary>
        /// Build save data for persistence.
        /// </summary>
        public TutorialSaveData BuildSaveData()
        {
            return new TutorialSaveData
            {
                LastCompletedStepIndex = _currentStepIndex - 1,
                IsComplete = _isComplete
            };
        }

        // -------------------------------------------------------------------
        // Private — step progression
        // -------------------------------------------------------------------

        private void CompleteCurrentStep()
        {
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;

            TutorialStep completed = _steps[_currentStepIndex];
            EventBus.Publish(new TutorialStepCompletedEvent(completed.StepId, _currentStepIndex));

            _currentStepIndex++;

            if (_currentStepIndex >= _steps.Count)
            {
                MarkTutorialComplete();
            }
            else
            {
                FireStepStarted();
            }
        }

        private void MarkTutorialComplete()
        {
            _isComplete = true;
            _isActive = false;
            EventBus.Publish(new TutorialCompletedEvent());
        }

        private void FireStepStarted()
        {
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Count) return;
            TutorialStep step = _steps[_currentStepIndex];
            EventBus.Publish(new TutorialStepStartedEvent(step.StepId, _currentStepIndex, step.HighlightTargetTag));
        }

        // -------------------------------------------------------------------
        // EventBus wiring — listen for game events that match tutorial actions
        // -------------------------------------------------------------------

        private void SubscribeToGameEvents()
        {
            _cardPlayedSub = EventBus.Subscribe<TutorialActionEvent>(OnTutorialAction);
        }

        private void UnsubscribeFromGameEvents()
        {
            _cardPlayedSub?.Dispose();
        }

        private void OnTutorialAction(TutorialActionEvent evt)
        {
            ReportAction(evt.Action);
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>Fired by game systems to notify the tutorial that a player action occurred.</summary>
    public readonly struct TutorialActionEvent
    {
        public readonly TutorialAction Action;
        public TutorialActionEvent(TutorialAction action) { Action = action; }
    }

    /// <summary>Fired when a tutorial step begins presentation.</summary>
    public readonly struct TutorialStepStartedEvent
    {
        public readonly string StepId;
        public readonly int StepIndex;
        public readonly string HighlightTargetTag;
        public TutorialStepStartedEvent(string id, int idx, string tag)
        { StepId = id; StepIndex = idx; HighlightTargetTag = tag; }
    }

    /// <summary>Fired when a tutorial step is completed or skipped.</summary>
    public readonly struct TutorialStepCompletedEvent
    {
        public readonly string StepId;
        public readonly int StepIndex;
        public TutorialStepCompletedEvent(string id, int idx) { StepId = id; StepIndex = idx; }
    }

    /// <summary>Fired when the entire tutorial is finished.</summary>
    public readonly struct TutorialCompletedEvent { }
}
