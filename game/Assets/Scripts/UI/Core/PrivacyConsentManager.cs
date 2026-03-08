using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.UI
{
    /// <summary>
    /// Manages GDPR/privacy consent state. Shows consent dialog on first launch.
    /// Must be shown before any data collection begins (compliance check #44, #48).
    /// </summary>
    public class PrivacyConsentManager : MonoBehaviour
    {
        private const string ConsentKey = "privacy_consent_given";
        private const string ConsentVersionKey = "privacy_consent_version";
        private const int CurrentConsentVersion = 1;

        public bool HasConsented { get; private set; }
        public bool NeedsConsent => !HasConsented || GetStoredConsentVersion() < CurrentConsentVersion;

        [SerializeField] private GameObject _consentDialog;
        [SerializeField] private string _privacyPolicyUrl = "https://ashenthrone.com/privacy";
        [SerializeField] private string _termsOfServiceUrl = "https://ashenthrone.com/tos";

        private void Awake()
        {
            ServiceLocator.Register<PrivacyConsentManager>(this);
            HasConsented = PlayerPrefs.GetInt(ConsentKey, 0) == 1
                && GetStoredConsentVersion() >= CurrentConsentVersion;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<PrivacyConsentManager>();
        }

        public void ShowConsentDialogIfNeeded()
        {
            if (NeedsConsent && _consentDialog != null)
                _consentDialog.SetActive(true);
        }

        /// <summary>User accepted privacy policy and terms.</summary>
        public void AcceptConsent()
        {
            HasConsented = true;
            PlayerPrefs.SetInt(ConsentKey, 1);
            PlayerPrefs.SetInt(ConsentVersionKey, CurrentConsentVersion);
            PlayerPrefs.Save();

            if (_consentDialog != null)
                _consentDialog.SetActive(false);

            EventBus.Publish(new PrivacyConsentAcceptedEvent());
        }

        /// <summary>User declined — disable analytics/tracking.</summary>
        public void DeclineConsent()
        {
            HasConsented = false;
            PlayerPrefs.SetInt(ConsentKey, 0);
            PlayerPrefs.Save();

            if (_consentDialog != null)
                _consentDialog.SetActive(false);

            EventBus.Publish(new PrivacyConsentDeclinedEvent());
        }

        /// <summary>Reset consent (for testing or account deletion).</summary>
        public void ResetConsent()
        {
            PlayerPrefs.DeleteKey(ConsentKey);
            PlayerPrefs.DeleteKey(ConsentVersionKey);
            HasConsented = false;
        }

        public void OpenPrivacyPolicy()
        {
            Application.OpenURL(_privacyPolicyUrl);
        }

        public void OpenTermsOfService()
        {
            Application.OpenURL(_termsOfServiceUrl);
        }

        private int GetStoredConsentVersion()
        {
            return PlayerPrefs.GetInt(ConsentVersionKey, 0);
        }
    }

    public readonly struct PrivacyConsentAcceptedEvent { }
    public readonly struct PrivacyConsentDeclinedEvent { }
}
