using UnityEngine;
using UnityEditor;

namespace AlakazamPortal.Editor
{
    /// <summary>
    /// First Time User Experience (FTUE) wizard for Alakazam Portal setup.
    /// </summary>
    public class SetupWizard : EditorWindow
    {
        private enum WizardStep
        {
            Welcome,
            ApiKey,
            DataConsent,
            Complete
        }

        private WizardStep _currentStep = WizardStep.Welcome;
        private string _apiKey = "";
        private string _validationMessage = "";
        private bool _validationSuccess = false;

        private bool _consentTerms = false;
        private bool _consentAnalytics = false;
        private bool _consentTraining = false;
        private bool _consentCloud = false;

        private GUIStyle _headerStyle;
        private GUIStyle _descriptionStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _scrollPosition;

        [MenuItem("Alakazam/Setup Wizard", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<SetupWizard>("Alakazam Setup");
            window.minSize = new Vector2(450, 400);
            window.maxSize = new Vector2(550, 500);
            window.Show();
        }

        [MenuItem("Alakazam/Account Settings", priority = 1)]
        public static void ShowAccountSettings()
        {
            Application.OpenURL("https://alakazam.ai/account");
        }

        [InitializeOnLoadMethod]
        private static void CheckFirstRun()
        {
            // Show wizard on first run (after domain reload)
            EditorApplication.delayCall += () =>
            {
                if (!AlakazamAuth.IsSetupComplete && !AlakazamAuth.HasApiKey)
                {
                    // Only show once per session
                    if (!SessionState.GetBool("AlakazamWizardShown", false))
                    {
                        SessionState.SetBool("AlakazamWizardShown", true);
                        ShowWindow();
                    }
                }
            };
        }

        private void OnEnable()
        {
            _apiKey = AlakazamAuth.GetApiKey();
            _consentAnalytics = AlakazamAuth.ShareUsageAnalytics;
            _consentTraining = AlakazamAuth.ShareDataForTraining;
            _consentCloud = AlakazamAuth.StoreCaputresOnline;

            // Skip to complete if already set up
            if (AlakazamAuth.IsSetupComplete && AlakazamAuth.HasApiKey)
            {
                _currentStep = WizardStep.Complete;
            }
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(0, 0, 10, 10)
                };
            }

            if (_descriptionStyle == null)
            {
                _descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 12,
                    margin = new RectOffset(10, 10, 5, 10)
                };
            }

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    padding = new RectOffset(20, 20, 10, 10)
                };
            }
        }

        private void OnGUI()
        {
            InitStyles();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);

            switch (_currentStep)
            {
                case WizardStep.Welcome:
                    DrawWelcome();
                    break;
                case WizardStep.ApiKey:
                    DrawApiKey();
                    break;
                case WizardStep.DataConsent:
                    DrawDataConsent();
                    break;
                case WizardStep.Complete:
                    DrawComplete();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawWelcome()
        {
            GUILayout.Label("Welcome to Alakazam Portal", _headerStyle);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Alakazam Portal lets you explore visual concepts on your greybox scenes in real-time. " +
                "Transform your 3D scenes with AI-powered stylization.\n\n" +
                "To get started, you'll need an API key from your Alakazam account.",
                _descriptionStyle
            );

            EditorGUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Get Started", _buttonStyle, GUILayout.Width(150)))
            {
                _currentStep = WizardStep.ApiKey;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // Footer
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Don't have an account? Sign up", EditorStyles.linkLabel))
            {
                AlakazamAuth.OpenSignupPage();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawApiKey()
        {
            GUILayout.Label("Enter Your API Key", _headerStyle);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Enter the API key from your Alakazam account. You can find this in your account dashboard.",
                _descriptionStyle
            );

            EditorGUILayout.Space(15);

            EditorGUILayout.LabelField("API Key:", EditorStyles.boldLabel);
            _apiKey = EditorGUILayout.TextField(_apiKey);

            // Validation message
            if (!string.IsNullOrEmpty(_validationMessage))
            {
                EditorGUILayout.Space(5);
                var style = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 11
                };
                EditorGUILayout.HelpBox(_validationMessage, _validationSuccess ? MessageType.Info : MessageType.Error);
            }

            EditorGUILayout.Space(20);

            // Buttons
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Back", GUILayout.Width(80)))
            {
                _currentStep = WizardStep.Welcome;
                _validationMessage = "";
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Account Dashboard", EditorStyles.linkLabel))
            {
                AlakazamAuth.OpenAccountPage();
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_apiKey));
            if (GUILayout.Button("Validate & Continue", _buttonStyle, GUILayout.Width(150)))
            {
                ValidateAndContinue();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();
        }

        private void ValidateAndContinue()
        {
            // Basic format validation
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _validationMessage = "Please enter an API key";
                _validationSuccess = false;
                return;
            }

            if (!_apiKey.StartsWith("ak_") && _apiKey.Length < 10)
            {
                _validationMessage = "Invalid API key format. Keys should start with 'ak_'";
                _validationSuccess = false;
                return;
            }

            // Save the key
            AlakazamAuth.SetApiKey(_apiKey.Trim());
            _validationMessage = "API key saved!";
            _validationSuccess = true;

            // Move to consent
            _currentStep = WizardStep.DataConsent;
            _validationMessage = "";
        }

        private void DrawDataConsent()
        {
            GUILayout.Label("Data & Privacy Settings", _headerStyle);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Choose how you'd like to share data with Alakazam. " +
                "We never store your proprietary game assets without your explicit consent.",
                _descriptionStyle
            );

            EditorGUILayout.Space(15);

            // Terms checkbox
            EditorGUILayout.BeginHorizontal();
            _consentTerms = EditorGUILayout.Toggle(_consentTerms, GUILayout.Width(20));
            EditorGUILayout.LabelField("I agree to the Terms of Service and Privacy Policy", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("View Terms of Service", EditorStyles.linkLabel, GUILayout.Width(150)))
            {
                Application.OpenURL("https://alakazam.ai/terms");
            }

            EditorGUILayout.Space(15);

            EditorGUILayout.LabelField("Optional Data Sharing:", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            // Analytics
            EditorGUILayout.BeginHorizontal();
            _consentAnalytics = EditorGUILayout.Toggle(_consentAnalytics, GUILayout.Width(20));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Share usage analytics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Help improve Alakazam by sharing anonymous usage data.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Training
            EditorGUILayout.BeginHorizontal();
            _consentTraining = EditorGUILayout.Toggle(_consentTraining, GUILayout.Width(20));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Allow model training with my stylized outputs", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("We may use your stylized outputs (not original assets) to improve AI models.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Cloud storage
            EditorGUILayout.BeginHorizontal();
            _consentCloud = EditorGUILayout.Toggle(_consentCloud, GUILayout.Width(20));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Store captures online", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Save your stylized captures to access from the web dashboard.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // Buttons
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Back", GUILayout.Width(80)))
            {
                _currentStep = WizardStep.ApiKey;
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!_consentTerms);
            if (GUILayout.Button("Complete Setup", _buttonStyle, GUILayout.Width(150)))
            {
                CompleteSetup();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();
        }

        private void CompleteSetup()
        {
            // Save preferences
            AlakazamAuth.ShareUsageAnalytics = _consentAnalytics;
            AlakazamAuth.ShareDataForTraining = _consentTraining;
            AlakazamAuth.StoreCaputresOnline = _consentCloud;
            AlakazamAuth.IsSetupComplete = true;

            _currentStep = WizardStep.Complete;
        }

        private void DrawComplete()
        {
            GUILayout.Label("Setup Complete!", _headerStyle);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "You're all set! Alakazam Portal is ready to use.\n\n" +
                "To get started:\n" +
                "1. Add an AlakazamController component to your scene\n" +
                "2. Configure your camera and output display\n" +
                "3. Enter Play mode and start stylizing!",
                _descriptionStyle
            );

            EditorGUILayout.Space(10);

            // Usage info
            var usage = AlakazamAuth.CurrentUsage;
            if (usage.SecondsLimit > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Usage This Month:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{usage.FormattedUsed} / {usage.FormattedLimit} ({usage.FormattedRemaining} remaining)");

                // Progress bar
                var rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, usage.UsagePercent / 100f, $"{usage.UsagePercent:F0}%");
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Documentation", GUILayout.Width(150)))
            {
                Application.OpenURL("https://alakazam.ai/docs");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Close", _buttonStyle, GUILayout.Width(100)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // Settings section
            EditorGUILayout.LabelField("Quick Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // API Key display (masked)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key:", GUILayout.Width(80));
            string maskedKey = MaskApiKey(AlakazamAuth.GetApiKey());
            EditorGUILayout.LabelField(maskedKey);
            if (GUILayout.Button("Change", GUILayout.Width(60)))
            {
                _currentStep = WizardStep.ApiKey;
            }
            EditorGUILayout.EndHorizontal();

            // Consent toggles
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Analytics:", GUILayout.Width(80));
            bool newAnalytics = EditorGUILayout.Toggle(AlakazamAuth.ShareUsageAnalytics);
            if (newAnalytics != AlakazamAuth.ShareUsageAnalytics)
                AlakazamAuth.ShareUsageAnalytics = newAnalytics;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Training:", GUILayout.Width(80));
            bool newTraining = EditorGUILayout.Toggle(AlakazamAuth.ShareDataForTraining);
            if (newTraining != AlakazamAuth.ShareDataForTraining)
                AlakazamAuth.ShareDataForTraining = newTraining;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Cloud:", GUILayout.Width(80));
            bool newCloud = EditorGUILayout.Toggle(AlakazamAuth.StoreCaputresOnline);
            if (newCloud != AlakazamAuth.StoreCaputresOnline)
                AlakazamAuth.StoreCaputresOnline = newCloud;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private string MaskApiKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 8)
                return "Not set";

            return key.Substring(0, 4) + "****" + key.Substring(key.Length - 4);
        }
    }
}
