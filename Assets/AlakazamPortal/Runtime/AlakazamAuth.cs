using System;
using UnityEngine;

namespace AlakazamPortal
{
    /// <summary>
    /// Manages Alakazam API key storage and usage tracking.
    /// </summary>
    public static class AlakazamAuth
    {
        private const string ApiKeyPref = "AlakazamPortal_ApiKey";
        private const string ConsentAnalyticsPref = "AlakazamPortal_ConsentAnalytics";
        private const string ConsentTrainingPref = "AlakazamPortal_ConsentTraining";
        private const string ConsentCloudPref = "AlakazamPortal_ConsentCloud";
        private const string SetupCompletePref = "AlakazamPortal_SetupComplete";

        /// <summary>
        /// Current usage information from server.
        /// </summary>
        public static UsageInfo CurrentUsage { get; private set; } = new UsageInfo();

        /// <summary>
        /// Event fired when usage reaches 80% or more.
        /// </summary>
        public static event Action<UsageInfo> OnUsageWarning;

        /// <summary>
        /// Event fired when usage reaches 100%.
        /// </summary>
        public static event Action<UsageInfo> OnUsageLimitReached;

        /// <summary>
        /// Event fired when auth fails.
        /// </summary>
        public static event Action<string> OnAuthFailed;

        #region API Key

        /// <summary>
        /// Returns true if an API key is stored.
        /// </summary>
        public static bool HasApiKey => !string.IsNullOrEmpty(GetApiKey());

        /// <summary>
        /// Returns true if first-time setup has been completed.
        /// </summary>
        public static bool IsSetupComplete
        {
            get => PlayerPrefs.GetInt(SetupCompletePref, 0) == 1;
            set => PlayerPrefs.SetInt(SetupCompletePref, value ? 1 : 0);
        }

        /// <summary>
        /// Get the stored API key.
        /// </summary>
        public static string GetApiKey()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetString(ApiKeyPref, "");
#else
            return PlayerPrefs.GetString(ApiKeyPref, "");
#endif
        }

        /// <summary>
        /// Store the API key.
        /// </summary>
        public static void SetApiKey(string apiKey)
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(ApiKeyPref, apiKey);
#else
            PlayerPrefs.SetString(ApiKeyPref, apiKey);
            PlayerPrefs.Save();
#endif
            Debug.Log("[AlakazamAuth] API key saved");
        }

        /// <summary>
        /// Clear the stored API key.
        /// </summary>
        public static void ClearApiKey()
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.DeleteKey(ApiKeyPref);
#else
            PlayerPrefs.DeleteKey(ApiKeyPref);
            PlayerPrefs.Save();
#endif
            Debug.Log("[AlakazamAuth] API key cleared");
        }

        #endregion

        #region Consent Preferences

        public static bool ShareUsageAnalytics
        {
            get => PlayerPrefs.GetInt(ConsentAnalyticsPref, 0) == 1;
            set { PlayerPrefs.SetInt(ConsentAnalyticsPref, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool ShareDataForTraining
        {
            get => PlayerPrefs.GetInt(ConsentTrainingPref, 0) == 1;
            set { PlayerPrefs.SetInt(ConsentTrainingPref, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool StoreCaputresOnline
        {
            get => PlayerPrefs.GetInt(ConsentCloudPref, 0) == 1;
            set { PlayerPrefs.SetInt(ConsentCloudPref, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        #endregion

        #region Usage Tracking

        /// <summary>
        /// Update usage info from server response.
        /// </summary>
        public static void UpdateUsage(int secondsUsed, int secondsLimit, int secondsRemaining)
        {
            CurrentUsage = new UsageInfo
            {
                SecondsUsed = secondsUsed,
                SecondsLimit = secondsLimit,
                SecondsRemaining = secondsRemaining
            };

            Debug.Log($"[AlakazamAuth] Usage updated: {CurrentUsage.SecondsUsed}/{CurrentUsage.SecondsLimit}s ({CurrentUsage.UsagePercent:F0}%)");

            if (CurrentUsage.IsOverLimit)
            {
                OnUsageLimitReached?.Invoke(CurrentUsage);
            }
            else if (CurrentUsage.ShouldWarn)
            {
                OnUsageWarning?.Invoke(CurrentUsage);
            }
        }

        /// <summary>
        /// Handle server warning message.
        /// </summary>
        public static void HandleWarning(string warning)
        {
            Debug.LogWarning($"[AlakazamAuth] Server warning: {warning}");
            OnUsageWarning?.Invoke(CurrentUsage);
        }

        /// <summary>
        /// Handle auth failure.
        /// </summary>
        public static void HandleAuthFailed(string message)
        {
            Debug.LogError($"[AlakazamAuth] Authentication failed: {message}");
            OnAuthFailed?.Invoke(message);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Open the Alakazam account page in browser.
        /// </summary>
        public static void OpenAccountPage()
        {
            Application.OpenURL("https://alakazam.ai/account");
        }

        /// <summary>
        /// Open the Alakazam signup page in browser.
        /// </summary>
        public static void OpenSignupPage()
        {
            Application.OpenURL("https://alakazam.ai/signup");
        }

        /// <summary>
        /// Format seconds as human-readable time.
        /// </summary>
        public static string FormatTime(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}s";
            if (seconds < 3600)
                return $"{seconds / 60}m {seconds % 60}s";
            return $"{seconds / 3600}h {(seconds % 3600) / 60}m";
        }

        #endregion
    }

    /// <summary>
    /// Usage information from server.
    /// </summary>
    [Serializable]
    public class UsageInfo
    {
        public int SecondsUsed;
        public int SecondsLimit;
        public int SecondsRemaining;

        public float UsagePercent => SecondsLimit > 0 ? (SecondsUsed / (float)SecondsLimit) * 100f : 100f;
        public bool ShouldWarn => UsagePercent >= 80f;
        public bool IsOverLimit => SecondsUsed >= SecondsLimit;

        public string FormattedUsed => AlakazamAuth.FormatTime(SecondsUsed);
        public string FormattedLimit => AlakazamAuth.FormatTime(SecondsLimit);
        public string FormattedRemaining => AlakazamAuth.FormatTime(SecondsRemaining);
    }
}
