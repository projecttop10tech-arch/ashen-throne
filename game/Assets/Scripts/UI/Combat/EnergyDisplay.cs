using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Combat;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Renders the player's energy as a row of orb icons (filled/empty).
    /// Subscribes to EnergyChangedEvent to update without polling.
    /// Supports up to MaxEnergy = 4 orbs. Additional orbs hidden automatically.
    /// </summary>
    public class EnergyDisplay : MonoBehaviour
    {
        [SerializeField] private List<Image> _energyOrbs;
        [SerializeField] private Color _filledColor = new Color(0.25f, 0.75f, 1f);
        [SerializeField] private Color _emptyColor = new Color(0.2f, 0.2f, 0.3f);

        private EventSubscription _energySub;

        private void Awake()
        {
            if (_energyOrbs == null || _energyOrbs.Count == 0)
                Debug.LogError("[EnergyDisplay] No energy orb images assigned.", this);

            // Show orbs up to MaxEnergy only
            for (int i = 0; i < _energyOrbs.Count; i++)
            {
                if (_energyOrbs[i] == null) continue;
                _energyOrbs[i].gameObject.SetActive(i < CardHandManager.MaxEnergy);
            }
        }

        private void OnEnable()
        {
            _energySub = EventBus.Subscribe<EnergyChangedEvent>(OnEnergyChanged);
        }

        private void OnDisable()
        {
            _energySub?.Dispose();
        }

        /// <summary>Force an immediate refresh — call after InitializeDeck.</summary>
        public void SetEnergy(int current)
        {
            UpdateOrbs(current);
        }

        private void OnEnergyChanged(EnergyChangedEvent evt)
        {
            UpdateOrbs(evt.CurrentEnergy);
        }

        private void UpdateOrbs(int currentEnergy)
        {
            for (int i = 0; i < _energyOrbs.Count; i++)
            {
                if (_energyOrbs[i] == null) continue;
                _energyOrbs[i].color = i < currentEnergy ? _filledColor : _emptyColor;
            }
        }
    }
}
