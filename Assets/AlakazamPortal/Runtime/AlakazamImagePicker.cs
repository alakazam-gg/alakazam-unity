using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AlakazamPortal
{
    /// <summary>
    /// Image picker component for Alakazam style extraction.
    /// Supports file browser and drag & drop.
    /// </summary>
    public class AlakazamImagePicker : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] private AlakazamController alakazamController;

        [Header("UI")]
        [SerializeField] private Image dropZoneImage;
        [SerializeField] private Text dropZoneText;
        [SerializeField] private RawImage previewImage;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
        [SerializeField] private Color activeColor = new Color(0.2f, 0.6f, 0.3f, 0.9f);

        private Texture2D _loadedTexture;
        private bool _isVisible = false;

        public event Action<Texture2D> OnImageSelected;
        public event Action<string> OnStyleExtracted;

        private void Awake()
        {
            if (alakazamController == null)
                alakazamController = FindObjectOfType<AlakazamController>();
        }

        private void Start()
        {
            if (alakazamController != null)
            {
                alakazamController.OnStyleExtracted += HandleStyleExtracted;
            }

            // Start hidden
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (alakazamController != null)
            {
                alakazamController.OnStyleExtracted -= HandleStyleExtracted;
            }

            if (_loadedTexture != null)
            {
                Destroy(_loadedTexture);
            }
        }

        /// <summary>
        /// Show/hide the image picker panel.
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
            gameObject.SetActive(_isVisible);
        }

        /// <summary>
        /// Show the image picker panel.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the image picker panel.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Open file browser to select an image.
        /// </summary>
        public void OpenFileBrowser()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel(
                "Select Style Reference Image",
                "",
                "png,jpg,jpeg"
            );

            if (!string.IsNullOrEmpty(path))
            {
                LoadImageFromPath(path);
            }
#else
            // For runtime builds, we need a file browser plugin
            // For now, log a message directing users to drag & drop
            Debug.Log("[AlakazamImagePicker] File browser not available in builds. Please drag & drop an image onto the window.");
#endif
        }

        /// <summary>
        /// Load an image from a file path.
        /// </summary>
        public void LoadImageFromPath(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[AlakazamImagePicker] File not found: {path}");
                return;
            }

            try
            {
                byte[] imageData = File.ReadAllBytes(path);
                LoadImageFromBytes(imageData, Path.GetFileName(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[AlakazamImagePicker] Failed to load image: {e.Message}");
            }
        }

        /// <summary>
        /// Load an image from raw bytes.
        /// </summary>
        public void LoadImageFromBytes(byte[] imageData, string filename = "image")
        {
            // Clean up old texture
            if (_loadedTexture != null)
            {
                Destroy(_loadedTexture);
            }

            // Create new texture
            _loadedTexture = new Texture2D(2, 2);
            if (_loadedTexture.LoadImage(imageData))
            {
                Debug.Log($"[AlakazamImagePicker] Loaded image: {filename} ({_loadedTexture.width}x{_loadedTexture.height})");

                // Update preview
                if (previewImage != null)
                {
                    previewImage.texture = _loadedTexture;
                    previewImage.gameObject.SetActive(true);
                }

                // Update drop zone text
                if (dropZoneText != null)
                {
                    dropZoneText.text = $"Loaded: {filename}\nClick 'Apply' to extract style";
                }

                if (dropZoneImage != null)
                {
                    dropZoneImage.color = activeColor;
                }

                OnImageSelected?.Invoke(_loadedTexture);
            }
            else
            {
                Debug.LogError("[AlakazamImagePicker] Failed to decode image data");
                Destroy(_loadedTexture);
                _loadedTexture = null;
            }
        }

        /// <summary>
        /// Apply the loaded image to extract style.
        /// </summary>
        public void ApplyStyle()
        {
            if (_loadedTexture == null)
            {
                Debug.LogWarning("[AlakazamImagePicker] No image loaded");
                return;
            }

            if (alakazamController == null)
            {
                Debug.LogError("[AlakazamImagePicker] No AlakazamController reference");
                return;
            }

            if (!alakazamController.IsStreaming)
            {
                Debug.LogWarning("[AlakazamImagePicker] Alakazam not streaming. Start streaming first.");
                return;
            }

            alakazamController.SetStyleFromImage(_loadedTexture);

            if (dropZoneText != null)
            {
                dropZoneText.text = "Extracting style...";
            }
        }

        private void HandleStyleExtracted(string prompt)
        {
            if (dropZoneText != null)
            {
                dropZoneText.text = $"Style applied!\n\"{prompt.Substring(0, Math.Min(50, prompt.Length))}...\"";
            }

            OnStyleExtracted?.Invoke(prompt);
        }

        /// <summary>
        /// Clear the loaded image.
        /// </summary>
        public void ClearImage()
        {
            if (_loadedTexture != null)
            {
                Destroy(_loadedTexture);
                _loadedTexture = null;
            }

            if (previewImage != null)
            {
                previewImage.texture = null;
                previewImage.gameObject.SetActive(false);
            }

            if (dropZoneText != null)
            {
                dropZoneText.text = "Drop image here\nor click 'Browse'";
            }

            if (dropZoneImage != null)
            {
                dropZoneImage.color = normalColor;
            }
        }

        #region Drag & Drop Handlers

        public void OnDrop(PointerEventData eventData)
        {
            // Note: Unity's built-in drag & drop from OS doesn't use PointerEventData
            // This handles UI-based drag & drop within Unity
            Debug.Log("[AlakazamImagePicker] OnDrop called (UI drag)");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (dropZoneImage != null && _loadedTexture == null)
            {
                dropZoneImage.color = hoverColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (dropZoneImage != null && _loadedTexture == null)
            {
                dropZoneImage.color = normalColor;
            }
        }

        #endregion

        #region Static UI Factory

        /// <summary>
        /// Create the image picker UI panel.
        /// </summary>
        public static AlakazamImagePicker CreateUI(Canvas parentCanvas, AlakazamController controller)
        {
            // Main panel
            var panelGO = new GameObject("ImagePickerPanel");
            panelGO.transform.SetParent(parentCanvas.transform);

            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(400, 350);

            var panelBG = panelGO.AddComponent<Image>();
            panelBG.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            var picker = panelGO.AddComponent<AlakazamImagePicker>();
            picker.alakazamController = controller;

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panelGO.transform);
            var title = titleGO.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.text = "Style from Image";
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleCenter;

            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -10);
            titleRect.sizeDelta = new Vector2(0, 30);

            // Close button
            var closeGO = new GameObject("CloseButton");
            closeGO.transform.SetParent(panelGO.transform);

            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-5, -5);
            closeRect.sizeDelta = new Vector2(30, 30);

            var closeBG = closeGO.AddComponent<Image>();
            closeBG.color = new Color(0.5f, 0.2f, 0.2f, 1f);

            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => picker.Hide());

            var closeText = new GameObject("X").AddComponent<Text>();
            closeText.transform.SetParent(closeGO.transform);
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeText.text = "X";
            closeText.fontSize = 16;
            closeText.color = Color.white;
            closeText.alignment = TextAnchor.MiddleCenter;
            var closeTextRect = closeText.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            // Drop zone
            var dropZoneGO = new GameObject("DropZone");
            dropZoneGO.transform.SetParent(panelGO.transform);

            var dropRect = dropZoneGO.AddComponent<RectTransform>();
            dropRect.anchorMin = new Vector2(0.05f, 0.3f);
            dropRect.anchorMax = new Vector2(0.95f, 0.85f);
            dropRect.offsetMin = Vector2.zero;
            dropRect.offsetMax = Vector2.zero;

            picker.dropZoneImage = dropZoneGO.AddComponent<Image>();
            picker.dropZoneImage.color = picker.normalColor;

            // Preview image (inside drop zone)
            var previewGO = new GameObject("Preview");
            previewGO.transform.SetParent(dropZoneGO.transform);

            var previewRect = previewGO.AddComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.05f, 0.15f);
            previewRect.anchorMax = new Vector2(0.95f, 0.95f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;

            picker.previewImage = previewGO.AddComponent<RawImage>();
            picker.previewImage.color = Color.white;
            previewGO.SetActive(false);

            // Drop zone text
            var dropTextGO = new GameObject("DropText");
            dropTextGO.transform.SetParent(dropZoneGO.transform);

            picker.dropZoneText = dropTextGO.AddComponent<Text>();
            picker.dropZoneText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            picker.dropZoneText.text = "Drop image here\nor click 'Browse'";
            picker.dropZoneText.fontSize = 16;
            picker.dropZoneText.color = new Color(1, 1, 1, 0.7f);
            picker.dropZoneText.alignment = TextAnchor.MiddleCenter;

            var dropTextRect = dropTextGO.GetComponent<RectTransform>();
            dropTextRect.anchorMin = Vector2.zero;
            dropTextRect.anchorMax = Vector2.one;
            dropTextRect.offsetMin = Vector2.zero;
            dropTextRect.offsetMax = Vector2.zero;

            // Button container
            var buttonsGO = new GameObject("Buttons");
            buttonsGO.transform.SetParent(panelGO.transform);

            var buttonsRect = buttonsGO.AddComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.05f, 0.05f);
            buttonsRect.anchorMax = new Vector2(0.95f, 0.25f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;

            var layout = buttonsGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Browse button
            CreateButton(buttonsGO.transform, "Browse", new Color(0.3f, 0.3f, 0.5f, 1f), () => picker.OpenFileBrowser());

            // Apply button
            CreateButton(buttonsGO.transform, "Apply Style", new Color(0.3f, 0.5f, 0.3f, 1f), () => picker.ApplyStyle());

            // Clear button
            CreateButton(buttonsGO.transform, "Clear", new Color(0.5f, 0.3f, 0.3f, 1f), () => picker.ClearImage());

            return picker;
        }

        private static void CreateButton(Transform parent, string text, Color bgColor, Action onClick)
        {
            var btnGO = new GameObject(text + "Button");
            btnGO.transform.SetParent(parent);

            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(100, 40);

            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = bgColor;

            var btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform);
            var label = labelGO.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;

            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
