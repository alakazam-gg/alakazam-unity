using UnityEngine;
using UnityEngine.UI;

namespace NeuralAkazam.Demo
{
    /// <summary>
    /// Demo controller with UI for testing MirageLSD.
    /// Shows status, allows prompt changes, and provides preset buttons.
    /// </summary>
    public class MirageDemo : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MirageController mirageController;

        [Header("UI - Auto Created")]
        private Canvas _uiCanvas;
        private Text _statusText;
        private InputField _promptInput;
        private Button _startButton;
        private Button _stopButton;

        [Header("Preset Prompts")]
        private readonly string[] _presets = new[]
        {
            "anime style, vibrant colors, cel shading",
            "cyberpunk city, neon lights, rain, night",
            "watercolor painting, soft edges, pastel colors",
            "pixel art, 16-bit retro game",
            "dark fantasy, gothic, dramatic lighting",
            "studio ghibli style, whimsical",
            "van gogh starry night style, swirling",
            "minecraft voxel style, blocky"
        };

        private int _currentPreset = 0;

        private void Start()
        {
            if (mirageController == null)
            {
                mirageController = FindObjectOfType<MirageController>();
            }

            CreateUI();
        }

        private void Update()
        {
            UpdateStatus();

            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnStartClicked();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnStopClicked();
            }

            // Bracket keys to cycle presets (like Oasis 2.0)
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                NextPreset();
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                PreviousPreset();
            }
        }

        private void CreateUI()
        {
            // Create UI Canvas (separate from the output canvas)
            var canvasGO = new GameObject("MirageDemoUI");
            _uiCanvas = canvasGO.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 200; // Above the output

            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Status text (top left)
            _statusText = CreateText("Status", new Vector2(10, -10), new Vector2(400, 100), TextAnchor.UpperLeft);
            _statusText.text = "Press ENTER to start MirageLSD";

            // Prompt input (bottom)
            var inputGO = new GameObject("PromptInput");
            inputGO.transform.SetParent(_uiCanvas.transform);
            var inputRect = inputGO.AddComponent<RectTransform>();
            _promptInput = inputGO.AddComponent<InputField>();
            inputRect.anchorMin = new Vector2(0, 0);
            inputRect.anchorMax = new Vector2(1, 0);
            inputRect.pivot = new Vector2(0.5f, 0);
            inputRect.anchoredPosition = new Vector2(0, 60);
            inputRect.sizeDelta = new Vector2(-200, 40);

            // Input field visuals
            var inputBG = inputGO.AddComponent<Image>();
            inputBG.color = new Color(0, 0, 0, 0.7f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(inputGO.transform);
            var textRect = textGO.AddComponent<RectTransform>();
            var inputText = textGO.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            _promptInput.textComponent = inputText;
            // Initialize with controller's prompt (from Inspector), not hardcoded preset
            _promptInput.text = mirageController != null ? mirageController.Prompt : _presets[0];
            _promptInput.onEndEdit.AddListener(OnPromptChanged);

            // Placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(inputGO.transform);
            var phRect = placeholderGO.AddComponent<RectTransform>();
            var placeholder = placeholderGO.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholder.color = new Color(1, 1, 1, 0.5f);
            placeholder.text = "Enter style prompt...";
            placeholder.alignment = TextAnchor.MiddleLeft;
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(10, 0);
            phRect.offsetMax = new Vector2(-10, 0);
            _promptInput.placeholder = placeholder;

            // Start button
            _startButton = CreateButton("START", new Vector2(-80, 10), new Vector2(150, 40), OnStartClicked);
            _startButton.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
            _startButton.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);

            // Stop button
            _stopButton = CreateButton("STOP", new Vector2(-80, 60), new Vector2(150, 40), OnStopClicked);
            _stopButton.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
            _stopButton.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
            _stopButton.interactable = false;

            // Preset label
            var presetText = CreateText("[ ] / [ ] to cycle presets", new Vector2(10, 10), new Vector2(300, 30), TextAnchor.LowerLeft);
            presetText.fontSize = 14;
            presetText.color = new Color(1, 1, 1, 0.6f);
        }

        private Text CreateText(string name, Vector2 position, Vector2 size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_uiCanvas.transform);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.fontSize = 18;
            text.alignment = anchor;

            // Add shadow for readability
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1, -1);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            return text;
        }

        private Button CreateButton(string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(_uiCanvas.transform);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 0.9f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.color = Color.white;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            return button;
        }

        private void UpdateStatus()
        {
            if (mirageController == null)
            {
                _statusText.text = "MirageController not found!";
                return;
            }

            string status = mirageController.IsStreaming ? "<color=green>STREAMING</color>" :
                           mirageController.IsConnected ? "<color=yellow>CONNECTED</color>" :
                           "<color=gray>DISCONNECTED</color>";

            // Show the actual prompt from the controller, not just input field
            string activePrompt = mirageController.Prompt;
            _statusText.text = $"Status: {status}\n" +
                              $"Prompt: {activePrompt}\n" +
                              $"Input: {_promptInput.text}";
        }

        private void OnStartClicked()
        {
            if (mirageController != null && !mirageController.IsStreaming)
            {
                // Sync input field to controller before starting (in case user edited input)
                if (_promptInput.text != mirageController.Prompt)
                {
                    mirageController.SetPrompt(_promptInput.text);
                }
                mirageController.StartMirage();
                _startButton.interactable = false;
                _stopButton.interactable = true;
            }
        }

        private void OnStopClicked()
        {
            if (mirageController != null)
            {
                mirageController.Stop();
                _startButton.interactable = true;
                _stopButton.interactable = false;
            }
        }

        private void OnPromptChanged(string newPrompt)
        {
            if (mirageController != null && mirageController.IsStreaming)
            {
                mirageController.SetPrompt(newPrompt);
            }
        }

        private void NextPreset()
        {
            _currentPreset = (_currentPreset + 1) % _presets.Length;
            _promptInput.text = _presets[_currentPreset];
            OnPromptChanged(_presets[_currentPreset]);
        }

        private void PreviousPreset()
        {
            _currentPreset = (_currentPreset - 1 + _presets.Length) % _presets.Length;
            _promptInput.text = _presets[_currentPreset];
            OnPromptChanged(_presets[_currentPreset]);
        }
    }
}
