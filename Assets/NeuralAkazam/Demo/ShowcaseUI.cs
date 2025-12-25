using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NeuralAkazam.Demo
{
    /// <summary>
    /// Clean showcase UI for demos.
    /// - Style presets as quick-select buttons (populate the text field)
    /// - Editable prompt field for custom styles
    /// - Minimal status display
    /// </summary>
    public class ShowcaseUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MirageController mirageController;

        [Header("Style Presets")]
        [SerializeField] private StylePreset[] presets = new[]
        {
            new StylePreset { name = "Cyberpunk", prompt = "cyberpunk neon city, rain, night, blade runner aesthetic" },
            new StylePreset { name = "Anime", prompt = "anime style, studio ghibli, vibrant colors, cel shading" },
            new StylePreset { name = "Watercolor", prompt = "watercolor painting, soft edges, pastel colors, artistic" },
            new StylePreset { name = "Pixel Art", prompt = "pixel art, 16-bit retro game, nostalgic" },
            new StylePreset { name = "Gothic", prompt = "dark fantasy, gothic architecture, dramatic lighting, moody" },
            new StylePreset { name = "Van Gogh", prompt = "van gogh starry night style, swirling brushstrokes, impressionist" },
            new StylePreset { name = "Minecraft", prompt = "minecraft voxel style, blocky, low poly" },
            new StylePreset { name = "Noir", prompt = "film noir, black and white, high contrast, 1940s detective" },
        };

        [Header("Settings")]
        [SerializeField] private bool autoStart = false;  // Let user explore first
        [SerializeField] private bool enhancePrompt = true;

        // UI Elements
        private Canvas _canvas;
        private InputField _promptField;
        private Text _statusIndicator;
        private GameObject _presetsContainer;
        private Button _startStopButton;
        private Text _startStopText;
        private int _selectedPreset = -1;

        // Split screen
        private bool _splitScreenActive = false;
        private RawImage _originalView;
        private RectTransform _outputRect;
        private Vector2 _outputOriginalAnchorMin;
        private Vector2 _outputOriginalAnchorMax;

        private void Start()
        {
            if (mirageController == null)
                mirageController = FindObjectOfType<MirageController>();

            CreateUI();
            SetupSplitScreen();

            if (autoStart && mirageController != null)
            {
                mirageController.StartMirage();
            }
        }

        private void SetupSplitScreen()
        {
            if (mirageController == null) return;

            // Get the output display (AI-transformed view)
            var output = mirageController.OutputDisplay;
            if (output != null)
            {
                _outputRect = output.GetComponent<RectTransform>();
                _outputOriginalAnchorMin = _outputRect.anchorMin;
                _outputOriginalAnchorMax = _outputRect.anchorMax;
            }

            // Create original camera view (hidden by default)
            var originalGO = new GameObject("OriginalView");
            var originalCanvas = originalGO.AddComponent<Canvas>();
            originalCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            originalCanvas.sortingOrder = 50; // Below the output

            var viewGO = new GameObject("CameraView");
            viewGO.transform.SetParent(originalGO.transform);
            _originalView = viewGO.AddComponent<RawImage>();

            var viewRect = _originalView.GetComponent<RectTransform>();
            viewRect.anchorMin = new Vector2(0, 0);
            viewRect.anchorMax = new Vector2(0.5f, 1);
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;

            // Render texture for the camera
            var cam = Camera.main;
            if (cam != null)
            {
                var rt = new RenderTexture(Screen.width / 2, Screen.height, 24);
                _originalView.texture = rt;

                // We'll update this each frame from the camera
            }

            // Add label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(viewGO.transform);
            var label = labelGO.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "ORIGINAL";
            label.fontSize = 24;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperCenter;

            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.pivot = new Vector2(0.5f, 1);
            labelRect.anchoredPosition = new Vector2(0, -20);
            labelRect.sizeDelta = new Vector2(0, 40);

            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor = Color.black;

            // Also add label to the AI view side
            if (_outputRect != null)
            {
                var aiLabelGO = new GameObject("AILabel");
                aiLabelGO.transform.SetParent(_outputRect.transform);
                var aiLabel = aiLabelGO.AddComponent<Text>();
                aiLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                aiLabel.text = "AI TRANSFORMED";
                aiLabel.fontSize = 24;
                aiLabel.color = Color.white;
                aiLabel.alignment = TextAnchor.UpperCenter;

                var aiLabelRect = aiLabelGO.GetComponent<RectTransform>();
                aiLabelRect.anchorMin = new Vector2(0, 1);
                aiLabelRect.anchorMax = new Vector2(1, 1);
                aiLabelRect.pivot = new Vector2(0.5f, 1);
                aiLabelRect.anchoredPosition = new Vector2(0, -20);
                aiLabelRect.sizeDelta = new Vector2(0, 40);

                var aiShadow = aiLabelGO.AddComponent<Shadow>();
                aiShadow.effectColor = Color.black;

                aiLabelGO.SetActive(false); // Only show in split mode
            }

            originalGO.SetActive(false); // Start hidden
        }

        private void Update()
        {
            UpdateStatus();

            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ApplyPrompt();
            }

            // Number keys 1-8 for presets
            for (int i = 0; i < Mathf.Min(8, presets.Length); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectPreset(i);
                }
            }

            // Tab to toggle UI visibility
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleUI();
            }

            // Space to start/stop
            if (Input.GetKeyDown(KeyCode.Space) && !_promptField.isFocused)
            {
                ToggleStartStop();
            }

            // B for before/after split screen
            if (Input.GetKeyDown(KeyCode.B))
            {
                ToggleSplitScreen();
            }

            // R for screenshot
            if (Input.GetKeyDown(KeyCode.R))
            {
                CaptureScreenshot();
            }

            // Update original view texture in split mode
            if (_splitScreenActive)
            {
                UpdateOriginalView();
            }
        }

        private void CaptureScreenshot()
        {
            StartCoroutine(CaptureScreenshotCoroutine());
        }

        private System.Collections.IEnumerator CaptureScreenshotCoroutine()
        {
            // Wait for end of frame to capture everything
            yield return new WaitForEndOfFrame();

            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"NeuralAkazam_{timestamp}.png";
            string path = System.IO.Path.Combine(Application.persistentDataPath, filename);

            // Capture screen to texture
            Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenshot.Apply();

            // Encode and save
            byte[] bytes = screenshot.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);

            // Cleanup
            Destroy(screenshot);

            Debug.Log($"[ShowcaseUI] Screenshot saved: {path}");
            StartCoroutine(ShowNotification("Screenshot saved!"));
        }

        private System.Collections.IEnumerator ShowNotification(string message)
        {
            // Create temporary notification
            var notifGO = new GameObject("Notification");
            notifGO.transform.SetParent(_canvas.transform);

            var text = notifGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = message;
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var shadow = notifGO.AddComponent<Shadow>();
            shadow.effectColor = Color.black;

            var rect = notifGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 100);
            rect.sizeDelta = new Vector2(400, 50);

            // Fade out over 2 seconds
            float elapsed = 0f;
            while (elapsed < 2f)
            {
                elapsed += Time.deltaTime;
                text.color = new Color(1, 1, 1, 1 - (elapsed / 2f));
                yield return null;
            }

            Destroy(notifGO);
        }

        private void ToggleStartStop()
        {
            if (mirageController == null) return;

            if (mirageController.IsStreaming || mirageController.IsConnected)
            {
                mirageController.Stop();
            }
            else
            {
                // Apply current prompt before starting
                if (!string.IsNullOrEmpty(_promptField.text))
                {
                    mirageController.SetPrompt(_promptField.text, enhancePrompt);
                }
                mirageController.StartMirage();
            }

            UpdateStartStopButton();
        }

        private void UpdateStartStopButton()
        {
            if (_startStopButton == null || _startStopText == null) return;

            bool isRunning = mirageController != null && (mirageController.IsStreaming || mirageController.IsConnected);
            _startStopText.text = isRunning ? "STOP" : "START";
            _startStopButton.GetComponent<Image>().color = isRunning
                ? new Color(0.8f, 0.3f, 0.3f, 1f)  // Red for stop
                : new Color(0.3f, 0.7f, 0.3f, 1f); // Green for start
        }

        private void ToggleSplitScreen()
        {
            _splitScreenActive = !_splitScreenActive;

            if (_originalView != null)
            {
                _originalView.transform.parent.gameObject.SetActive(_splitScreenActive);
            }

            if (_outputRect != null)
            {
                if (_splitScreenActive)
                {
                    // Move AI view to right half
                    _outputRect.anchorMin = new Vector2(0.5f, 0);
                    _outputRect.anchorMax = new Vector2(1, 1);

                    // Show AI label
                    var aiLabel = _outputRect.Find("AILabel");
                    if (aiLabel != null) aiLabel.gameObject.SetActive(true);
                }
                else
                {
                    // Restore full screen
                    _outputRect.anchorMin = _outputOriginalAnchorMin;
                    _outputRect.anchorMax = _outputOriginalAnchorMax;

                    // Hide AI label
                    var aiLabel = _outputRect.Find("AILabel");
                    if (aiLabel != null) aiLabel.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateOriginalView()
        {
            // Blit the current camera view to the original view texture
            var cam = Camera.main;
            if (cam != null && _originalView != null && _originalView.texture is RenderTexture rt)
            {
                var currentRT = RenderTexture.active;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = currentRT;
            }
        }

        private void CreateUI()
        {
            // Main canvas
            var canvasGO = new GameObject("ShowcaseUI");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200; // Above MirageController's output canvas (100)

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists for UI interaction
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            // Status indicator (top-left, minimal)
            _statusIndicator = CreateText("Status", _canvas.transform);
            var statusRect = _statusIndicator.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(0, 1);
            statusRect.pivot = new Vector2(0, 1);
            statusRect.anchoredPosition = new Vector2(20, -20);
            statusRect.sizeDelta = new Vector2(300, 30);
            _statusIndicator.fontSize = 16;
            _statusIndicator.alignment = TextAnchor.UpperLeft;

            // Controls hint (top-right)
            var controlsHint = CreateText("Controls", _canvas.transform);
            var controlsRect = controlsHint.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(1, 1);
            controlsRect.anchorMax = new Vector2(1, 1);
            controlsRect.pivot = new Vector2(1, 1);
            controlsRect.anchoredPosition = new Vector2(-20, -20);
            controlsRect.sizeDelta = new Vector2(350, 60);
            controlsHint.fontSize = 14;
            controlsHint.alignment = TextAnchor.UpperRight;
            controlsHint.color = new Color(1, 1, 1, 0.6f);
            controlsHint.text = "WASD: Move | Mouse: Look | Shift: Fast\nSpace: Start/Stop | B: Split | R: Screenshot";

            // Bottom panel container
            var bottomPanel = new GameObject("BottomPanel");
            bottomPanel.transform.SetParent(_canvas.transform);
            var panelRect = bottomPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0, 120);

            var panelBG = bottomPanel.AddComponent<Image>();
            panelBG.color = new Color(0, 0, 0, 0.7f);

            // Prompt input field
            CreatePromptField(bottomPanel.transform);

            // Preset buttons
            CreatePresetButtons(bottomPanel.transform);

            // Start/Stop button (right side of bottom panel)
            CreateStartStopButton(bottomPanel.transform);

            // Branding (bottom-right corner)
            var branding = CreateText("Branding", _canvas.transform);
            var brandRect = branding.GetComponent<RectTransform>();
            brandRect.anchorMin = new Vector2(1, 0);
            brandRect.anchorMax = new Vector2(1, 0);
            brandRect.pivot = new Vector2(1, 0);
            brandRect.anchoredPosition = new Vector2(-20, 130);
            brandRect.sizeDelta = new Vector2(200, 25);
            branding.fontSize = 14;
            branding.alignment = TextAnchor.LowerRight;
            branding.color = new Color(1, 1, 1, 0.5f);
            branding.text = "NeuralAkazam + Decart MirageLSD";
        }

        private void CreatePromptField(Transform parent)
        {
            var fieldGO = new GameObject("PromptField");
            fieldGO.transform.SetParent(parent);

            var fieldRect = fieldGO.AddComponent<RectTransform>();
            fieldRect.anchorMin = new Vector2(0, 1);
            fieldRect.anchorMax = new Vector2(1, 1);
            fieldRect.pivot = new Vector2(0.5f, 1);
            fieldRect.anchoredPosition = new Vector2(0, -10);
            fieldRect.sizeDelta = new Vector2(-40, 40);

            var fieldBG = fieldGO.AddComponent<Image>();
            fieldBG.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            _promptField = fieldGO.AddComponent<InputField>();

            // Text component
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(fieldGO.transform);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15, 5);
            textRect.offsetMax = new Vector2(-15, -5);

            _promptField.textComponent = text;

            // Placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(fieldGO.transform);
            var placeholder = placeholderGO.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholder.color = new Color(1, 1, 1, 0.4f);
            placeholder.fontSize = 18;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.text = "Type a style or select a preset below...";

            var phRect = placeholderGO.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(15, 5);
            phRect.offsetMax = new Vector2(-15, -5);

            _promptField.placeholder = placeholder;

            // Set initial prompt from controller
            if (mirageController != null)
                _promptField.text = mirageController.Prompt;

            _promptField.onEndEdit.AddListener(OnPromptSubmit);
        }

        private void CreatePresetButtons(Transform parent)
        {
            _presetsContainer = new GameObject("Presets");
            _presetsContainer.transform.SetParent(parent);

            var containerRect = _presetsContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.pivot = new Vector2(0.5f, 0);
            containerRect.anchoredPosition = new Vector2(0, 15);
            containerRect.sizeDelta = new Vector2(-40, 45);

            var layout = _presetsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            for (int i = 0; i < presets.Length; i++)
            {
                CreatePresetButton(i, presets[i]);
            }
        }

        private void CreatePresetButton(int index, StylePreset preset)
        {
            var btnGO = new GameObject($"Preset_{preset.name}");
            btnGO.transform.SetParent(_presetsContainer.transform);

            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(120, 35);

            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.pressedColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            colors.selectedColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            btn.colors = colors;

            int capturedIndex = index;
            btn.onClick.AddListener(() => SelectPreset(capturedIndex));

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform);
            var label = labelGO.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = $"{index + 1}. {preset.name}";
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;

            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private void CreateStartStopButton(Transform parent)
        {
            var btnGO = new GameObject("StartStopButton");
            btnGO.transform.SetParent(parent);

            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1, 0);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.pivot = new Vector2(1, 0.5f);
            btnRect.anchoredPosition = new Vector2(-15, 0);
            btnRect.sizeDelta = new Vector2(100, -30);

            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.7f, 0.3f, 1f); // Green by default

            _startStopButton = btnGO.AddComponent<Button>();
            _startStopButton.onClick.AddListener(ToggleStartStop);

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform);
            _startStopText = labelGO.AddComponent<Text>();
            _startStopText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _startStopText.text = "START";
            _startStopText.fontSize = 18;
            _startStopText.fontStyle = FontStyle.Bold;
            _startStopText.color = Color.white;
            _startStopText.alignment = TextAnchor.MiddleCenter;

            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private Text CreateText(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);

            return text;
        }

        private void SelectPreset(int index)
        {
            if (index < 0 || index >= presets.Length) return;

            _selectedPreset = index;
            _promptField.text = presets[index].prompt;
            ApplyPrompt();

            // Visual feedback - highlight selected button
            UpdatePresetButtonColors();
        }

        private void UpdatePresetButtonColors()
        {
            for (int i = 0; i < _presetsContainer.transform.childCount; i++)
            {
                var btn = _presetsContainer.transform.GetChild(i).GetComponent<Image>();
                if (btn != null)
                {
                    btn.color = (i == _selectedPreset)
                        ? new Color(0.2f, 0.5f, 0.8f, 1f)  // Selected: blue
                        : new Color(0.3f, 0.3f, 0.3f, 1f); // Normal: gray
                }
            }
        }

        private void OnPromptSubmit(string text)
        {
            // Clear preset selection when user types custom prompt
            _selectedPreset = -1;
            UpdatePresetButtonColors();
            ApplyPrompt();
        }

        private void ApplyPrompt()
        {
            if (mirageController != null && !string.IsNullOrEmpty(_promptField.text))
            {
                mirageController.SetPrompt(_promptField.text, enhancePrompt);
            }
        }

        private void UpdateStatus()
        {
            if (mirageController == null)
            {
                _statusIndicator.text = "<color=#ff4444>●</color> No Controller";
                return;
            }

            if (mirageController.IsStreaming)
            {
                _statusIndicator.text = "<color=#44ff44>●</color> Live";
            }
            else if (mirageController.IsConnected)
            {
                _statusIndicator.text = "<color=#ffff44>●</color> Connecting...";
            }
            else
            {
                _statusIndicator.text = "<color=#888888>●</color> Ready (Space to start)";
            }

            UpdateStartStopButton();
        }

        private void ToggleUI()
        {
            var bottomPanel = _canvas.transform.Find("BottomPanel");
            if (bottomPanel != null)
            {
                bottomPanel.gameObject.SetActive(!bottomPanel.gameObject.activeSelf);
            }
        }

        [System.Serializable]
        public class StylePreset
        {
            public string name;
            public string prompt;
        }
    }
}
