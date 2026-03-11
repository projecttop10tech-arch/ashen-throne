using UnityEngine;
using UnityEngine.UI;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Overlay indicator for building actions: upgrade available, construction timer,
    /// resource collection ready. Shown as a small icon in the building's top-right corner.
    /// </summary>
    public class BuildingActionIndicator : MonoBehaviour
    {
        [SerializeField] private GameObject upgradeIcon;
        [SerializeField] private GameObject timerGroup;
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject collectIcon;

        public void ShowUpgrade()
        {
            HideAll();
            if (upgradeIcon != null) upgradeIcon.SetActive(true);
        }

        public void ShowTimer(string timeDisplay)
        {
            HideAll();
            if (timerGroup != null) timerGroup.SetActive(true);
            if (timerText != null) timerText.text = timeDisplay;
        }

        public void ShowCollect()
        {
            HideAll();
            if (collectIcon != null) collectIcon.SetActive(true);
        }

        public void HideAll()
        {
            if (upgradeIcon != null) upgradeIcon.SetActive(false);
            if (timerGroup != null) timerGroup.SetActive(false);
            if (collectIcon != null) collectIcon.SetActive(false);
        }
    }
}
