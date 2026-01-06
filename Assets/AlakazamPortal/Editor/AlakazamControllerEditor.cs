using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace AlakazamPortal.Editor
{
    /// <summary>
    /// Custom Inspector for AlakazamController with image picker functionality.
    /// </summary>
    [CustomEditor(typeof(AlakazamController))]
    public class AlakazamControllerEditor : UnityEditor.Editor
    {
        private Texture2D _previewTexture;
        private string _loadedImagePath;
        private string _extractedStyle;
        private bool _isExtracting;
        private bool _wasStreaming;

        // Session state keys for persisting across Play Mode
        private const string SESSION_KEY_IMAGE_PATH = "AlakazamImagePicker_ImagePath";
        private const string SESSION_KEY_EXTRACTED_STYLE = "AlakazamImagePicker_ExtractedStyle";

        private SerializedProperty _serverUrl;
        private SerializedProperty _prompt;
        private SerializedProperty _enhancePrompt;
        private SerializedProperty _sourceCamera;
        private SerializedProperty _captureWidth;
        private SerializedProperty _captureHeight;
        private SerializedProperty _jpegQuality;
        private SerializedProperty _targetFps;
        private SerializedProperty _outputDisplay;
        private SerializedProperty _autoCreateDisplay;

        private bool _showStyleFromImage = true;

        private void OnEnable()
        {
            _serverUrl = serializedObject.FindProperty("serverUrl");
            _prompt = serializedObject.FindProperty("prompt");
            _enhancePrompt = serializedObject.FindProperty("enhancePrompt");
            _sourceCamera = serializedObject.FindProperty("sourceCamera");
            _captureWidth = serializedObject.FindProperty("captureWidth");
            _captureHeight = serializedObject.FindProperty("captureHeight");
            _jpegQuality = serializedObject.FindProperty("jpegQuality");
            _targetFps = serializedObject.FindProperty("targetFps");
            _outputDisplay = serializedObject.FindProperty("outputDisplay");
            _autoCreateDisplay = serializedObject.FindProperty("autoCreateDisplay");

            // Subscribe to style extraction event
            var controller = target as AlakazamController;
            if (controller != null)
            {
                controller.OnStyleExtracted += OnStyleExtracted;
            }

            // Restore image from session state (persists across Play Mode)
            RestoreImageFromSession();
        }

        private void RestoreImageFromSession()
        {
            string savedPath = SessionState.GetString(SESSION_KEY_IMAGE_PATH, "");
            string savedStyle = SessionState.GetString(SESSION_KEY_EXTRACTED_STYLE, "");

            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                // Reload the image
                try
                {
                    byte[] imageData = File.ReadAllBytes(savedPath);
                    _previewTexture = new Texture2D(2, 2);
                    if (_previewTexture.LoadImage(imageData))
                    {
                        _loadedImagePath = savedPath;
                        _extractedStyle = savedStyle;
                        Debug.Log($"[AlakazamEditor] Restored image from session: {savedPath}");

                        var controller = target as AlakazamController;
                        if (controller != null && Application.isPlaying)
                        {
                            if (!string.IsNullOrEmpty(_extractedStyle))
                            {
                                // We have an extracted style - just apply it
                                controller.SetPrompt(_extractedStyle);
                            }
                            else
                            {
                                // Image but no extracted style yet - extract now
                                controller.ExtractStyleFromImage(_previewTexture);
                            }
                        }
                    }
                    else
                    {
                        DestroyImmediate(_previewTexture);
                        _previewTexture = null;
                        ClearSessionState();
                    }
                }
                catch
                {
                    ClearSessionState();
                }
            }
        }

        private void SaveImageToSession()
        {
            SessionState.SetString(SESSION_KEY_IMAGE_PATH, _loadedImagePath ?? "");
            SessionState.SetString(SESSION_KEY_EXTRACTED_STYLE, _extractedStyle ?? "");
        }

        private void ClearSessionState()
        {
            SessionState.EraseString(SESSION_KEY_IMAGE_PATH);
            SessionState.EraseString(SESSION_KEY_EXTRACTED_STYLE);
        }

        private void OnDisable()
        {
            var controller = target as AlakazamController;
            if (controller != null)
            {
                controller.OnStyleExtracted -= OnStyleExtracted;
            }

            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        private void OnStyleExtracted(string prompt)
        {
            _extractedStyle = prompt;
            _isExtracting = false;
            SaveImageToSession(); // Persist extracted style
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var controller = target as AlakazamController;

            // Track streaming state for UI updates
            if (controller != null && Application.isPlaying)
            {
                _wasStreaming = controller.IsStreaming;
            }
            else
            {
                _wasStreaming = false;
            }

            // Server Configuration
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serverUrl);
            EditorGUILayout.Space();

            // Prompt
            EditorGUILayout.LabelField("Style Prompt", EditorStyles.boldLabel);

            // Show indicator if using image-based style
            bool controllerExtracting = controller != null && controller.IsExtractingStyle;
            bool controllerUsingImage = controller != null && controller.IsUsingImageStyle;

            if (controllerUsingImage || (!string.IsNullOrEmpty(_extractedStyle) && _previewTexture != null))
            {
                EditorGUILayout.HelpBox("Using style extracted from image", MessageType.Info);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(_prompt);
                EditorGUI.EndDisabledGroup();
            }
            else if (controllerExtracting || (_isExtracting && _previewTexture != null))
            {
                EditorGUILayout.HelpBox("Extracting style from image...", MessageType.Info);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(_prompt);
                EditorGUI.EndDisabledGroup();
            }
            else if (_previewTexture != null && !Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to extract style from image", MessageType.Info);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(_prompt);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.PropertyField(_prompt);
            }

            EditorGUILayout.PropertyField(_enhancePrompt);
            EditorGUILayout.Space();

            // Style from Image section
            _showStyleFromImage = EditorGUILayout.Foldout(_showStyleFromImage, "Style from Image", true, EditorStyles.foldoutHeader);
            if (_showStyleFromImage)
            {
                EditorGUI.indentLevel++;
                DrawStyleFromImageSection(controller);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            // Input
            EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sourceCamera);
            EditorGUILayout.PropertyField(_captureWidth);
            EditorGUILayout.PropertyField(_captureHeight);
            EditorGUILayout.PropertyField(_jpegQuality);
            EditorGUILayout.PropertyField(_targetFps);
            EditorGUILayout.Space();

            // Output
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_outputDisplay);
            EditorGUILayout.PropertyField(_autoCreateDisplay);
            EditorGUILayout.Space();

            // Status (runtime only)
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Toggle("Connected", controller.IsConnected);
                EditorGUILayout.Toggle("Streaming", controller.IsStreaming);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                // Control buttons
                EditorGUILayout.BeginHorizontal();
                if (!controller.IsConnected)
                {
                    if (GUILayout.Button("Start", GUILayout.Height(30)))
                    {
                        controller.StartAlakazam();
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop", GUILayout.Height(30)))
                    {
                        controller.Stop();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStyleFromImageSection(AlakazamController controller)
        {
            // Image selection - Drag & drop area
            EditorGUILayout.BeginHorizontal();

            var dropArea = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));

            // Draw background based on state
            Color bgColor;
            string dropText;
            if (_isExtracting)
            {
                bgColor = new Color(0.3f, 0.4f, 0.5f);
                dropText = "Extracting style...";
            }
            else if (_previewTexture != null)
            {
                bgColor = new Color(0.2f, 0.35f, 0.2f);
                dropText = "";
            }
            else
            {
                bgColor = new Color(0.25f, 0.25f, 0.25f);
                dropText = "Drop reference image here\nor click Browse";
            }

            EditorGUI.DrawRect(dropArea, bgColor);

            if (!string.IsNullOrEmpty(dropText))
            {
                var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                style.normal.textColor = Color.white;
                style.fontSize = 11;
                GUI.Label(dropArea, dropText, style);
            }

            // Draw preview if loaded
            if (_previewTexture != null)
            {
                float aspectRatio = (float)_previewTexture.width / _previewTexture.height;
                float previewHeight = dropArea.height - 8;
                float previewWidth = previewHeight * aspectRatio;
                var previewRect = new Rect(
                    dropArea.x + (dropArea.width - previewWidth) / 2,
                    dropArea.y + 4,
                    previewWidth,
                    previewHeight
                );
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);
            }

            // Handle drag & drop
            HandleDragAndDrop(dropArea);

            EditorGUILayout.EndHorizontal();

            // File path
            if (!string.IsNullOrEmpty(_loadedImagePath))
            {
                EditorGUILayout.LabelField(Path.GetFileName(_loadedImagePath), EditorStyles.miniLabel);
            }

            // Buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                BrowseForImage();
            }

            EditorGUI.BeginDisabledGroup(_previewTexture == null);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                ClearImage();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            // Manual re-extract button (in case user wants to refresh)
            if (_previewTexture != null && Application.isPlaying && controller.IsStreaming && !_isExtracting)
            {
                if (GUILayout.Button("Re-extract", GUILayout.Width(80)))
                {
                    ApplyStyleFromImage(controller);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Show extracted style
            if (!string.IsNullOrEmpty(_extractedStyle))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Extracted Style:", EditorStyles.boldLabel);

                var textAreaStyle = new GUIStyle(EditorStyles.textArea);
                textAreaStyle.wordWrap = true;
                EditorGUILayout.TextArea(_extractedStyle, textAreaStyle, GUILayout.MinHeight(40));
            }
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    // Check if any dragged items are images
                    bool hasImage = false;
                    foreach (var path in DragAndDrop.paths)
                    {
                        if (IsImageFile(path))
                        {
                            hasImage = true;
                            break;
                        }
                    }

                    if (hasImage)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (evt.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();

                            foreach (var path in DragAndDrop.paths)
                            {
                                if (IsImageFile(path))
                                {
                                    LoadImage(path);
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    }

                    evt.Use();
                    break;
            }
        }

        private bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif";
        }

        private void BrowseForImage()
        {
            string path = EditorUtility.OpenFilePanel(
                "Select Style Reference Image",
                "",
                "png,jpg,jpeg,bmp"
            );

            if (!string.IsNullOrEmpty(path))
            {
                LoadImage(path);
            }
        }

        private void LoadImage(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"Image file not found: {path}");
                return;
            }

            try
            {
                byte[] imageData = File.ReadAllBytes(path);

                if (_previewTexture != null)
                {
                    DestroyImmediate(_previewTexture);
                }

                _previewTexture = new Texture2D(2, 2);
                if (_previewTexture.LoadImage(imageData))
                {
                    _loadedImagePath = path;
                    _extractedStyle = null;
                    SaveImageToSession(); // Persist across Play Mode
                    Debug.Log($"Loaded image: {path} ({_previewTexture.width}x{_previewTexture.height})");

                    // Auto-extract style immediately (will auto-connect if needed)
                    var controller = target as AlakazamController;
                    if (controller != null && Application.isPlaying)
                    {
                        controller.ExtractStyleFromImage(_previewTexture);
                    }

                    Repaint();
                }
                else
                {
                    Debug.LogError($"Failed to decode image: {path}");
                    DestroyImmediate(_previewTexture);
                    _previewTexture = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load image: {e.Message}");
            }
        }

        private void ClearImage()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
            _loadedImagePath = null;
            _extractedStyle = null;
            ClearSessionState(); // Clear persisted state

            // Clear image style mode on the controller
            var controller = target as AlakazamController;
            if (controller != null)
            {
                controller.ClearImageStyle();
            }

            Repaint();
        }

        private void ApplyStyleFromImage(AlakazamController controller)
        {
            if (_previewTexture == null || controller == null)
                return;

            _isExtracting = true;
            _extractedStyle = null;
            controller.SetStyleFromImage(_previewTexture);
            Repaint();
        }
    }
}
