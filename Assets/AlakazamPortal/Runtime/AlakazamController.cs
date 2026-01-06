using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AlakazamPortal
{
    /// <summary>
    /// Main controller for Alakazam proxy server connection.
    ///
    /// Connects to Alakazam proxy server via WebSocket.
    /// Captures rendered frames from your camera and returns stylized output in real-time.
    ///
    /// Attach to a GameObject with a Camera to capture frames from.
    /// The transformed output is displayed on the specified RawImage.
    /// </summary>
    public class AlakazamController : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "ws://localhost:9001";

        [Header("Prompt")]
        [SerializeField] private string prompt = "anime style, vibrant colors";
        [SerializeField] private bool enhancePrompt = true;

        [Header("Input")]
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private int captureWidth = 1280;
        [SerializeField] private int captureHeight = 720;
        [SerializeField] private int jpegQuality = 85;
        [SerializeField] private float targetFps = 30f;

        [Header("Output")]
        [Tooltip("UI RawImage to display output. If null and autoCreateDisplay is true, one will be created.")]
        [SerializeField] private RawImage outputDisplay;
        [Tooltip("Automatically create a fullscreen canvas if no outputDisplay is assigned")]
        [SerializeField] private bool autoCreateDisplay = true;

        [Header("Status")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private bool isStreaming = false;
        [SerializeField] private float currentFps = 0f;
        [SerializeField] private int framesSent = 0;
        [SerializeField] private int framesReceived = 0;

        // WebSocket
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly Queue<byte[]> _receivedFrames = new Queue<byte[]>();
        private readonly object _frameLock = new object();

        // Capture
        private RenderTexture _captureRT;
        private Texture2D _captureTexture;
        private Texture2D _outputTexture;

        // State
        private float _fpsTimer = 0f;
        private int _fpsFrameCount = 0;
        private string _lastPrompt;
        private bool _isUsingImageStyle = false;
        private bool _isExtractingStyle = false;
        private bool _isReady = false; // Server is ready to accept commands (separate from streaming)
        private bool _isCaptureLoopRunning = false; // Actual frame capture is running

        public bool IsConnected => isConnected;
        public bool IsStreaming => isStreaming;

        /// <summary>
        /// True if connected and ready to accept commands (style extraction, etc.)
        /// </summary>
        public bool IsReady => _isReady;
        public string Prompt => prompt;
        public RawImage OutputDisplay => outputDisplay;

        /// <summary>
        /// True if currently using a style extracted from an image (not text prompt).
        /// </summary>
        public bool IsUsingImageStyle => _isUsingImageStyle;

        /// <summary>
        /// True if currently extracting style from an image.
        /// </summary>
        public bool IsExtractingStyle => _isExtractingStyle;

        /// <summary>
        /// Clear image style mode and return to text prompt mode.
        /// </summary>
        public void ClearImageStyle()
        {
            _isUsingImageStyle = false;
            _isExtractingStyle = false;
        }

        /// <summary>
        /// Extract style from image immediately. Will auto-connect if needed.
        /// Does NOT start streaming - only extracts the style.
        /// </summary>
        public void ExtractStyleFromImage(Texture2D referenceImage)
        {
            if (referenceImage == null)
            {
                Debug.LogError("[AlakazamController] Reference image is null");
                return;
            }

            _pendingStyleImage = referenceImage;
            _isExtractingStyle = true;
            _extractionOnlyMode = true; // Always set this to prevent streaming

            if (!isConnected)
            {
                // Auto-connect for extraction only (no streaming)
                Debug.Log("[AlakazamController] Connecting for style extraction...");
                StartCoroutine(ConnectForExtractionOnly());
            }
            else if (_isReady)
            {
                // Already connected and ready - send extraction request immediately
                SendImageForExtraction(referenceImage);
            }
            else
            {
                // Connected but not ready yet - wait for ready then extract
                StartCoroutine(WaitForReadyAndExtract());
            }
        }

        private IEnumerator WaitForReadyAndExtract()
        {
            float timeout = 10f;
            while (!_isReady && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (_isReady && _pendingStyleImage != null)
            {
                SendImageForExtraction(_pendingStyleImage);
            }
            else
            {
                Debug.LogError("[AlakazamController] Timeout waiting for ready state");
                _isExtractingStyle = false;
            }
        }

        private Texture2D _pendingStyleImage;
        private bool _extractionOnlyMode = false;

        private IEnumerator ConnectForExtractionOnly()
        {
            _extractionOnlyMode = true;

            // DON'T setup capture resources for extraction-only - we only need WebSocket
            // SetupCapture will be called later if/when user starts streaming

            // Connect WebSocket
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            Debug.Log($"[AlakazamController] Connecting to {serverUrl} for extraction...");

            var connectTask = _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);

            // Wait for connection
            float timeout = 10f;
            while (!connectTask.IsCompleted && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (connectTask.IsFaulted)
            {
                Debug.LogError($"[AlakazamController] Connection failed: {connectTask.Exception?.InnerException?.Message}");
                _isExtractingStyle = false;
                _extractionOnlyMode = false;
                yield break;
            }

            if (!connectTask.IsCompleted || _ws.State != WebSocketState.Open)
            {
                Debug.LogError("[AlakazamController] Connection timeout");
                _isExtractingStyle = false;
                _extractionOnlyMode = false;
                _cts?.Cancel();
                yield break;
            }

            Debug.Log("[AlakazamController] Connected for extraction");
            isConnected = true;

            // Start receive loop
            _ = ReceiveLoopAsync();

            // Send auth (but don't start streaming)
            yield return SendAuthForExtractionCoroutine();
        }

        private IEnumerator SendAuthForExtractionCoroutine()
        {
            // Check for API key
            string apiKey = AlakazamAuth.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[AlakazamController] No API key configured");
                AlakazamAuth.HandleAuthFailed("No API key configured");
                _isExtractingStyle = false;
                _extractionOnlyMode = false;
                yield break;
            }

            var authMsg = new AuthMessage
            {
                type = "auth",
                prompt = prompt,
                api_key = apiKey
            };

            var task = SendJsonAsync(authMsg);
            while (!task.IsCompleted) yield return null;

            _lastPrompt = prompt;
            Debug.Log("[AlakazamController] Auth sent, waiting for ready...");

            // Wait for ready state (not streaming, just ready to accept commands)
            float timeout = 10f;
            while (!_isReady && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            // Now send the image for extraction (but don't start capture loop)
            if (_pendingStyleImage != null)
            {
                SendImageForExtraction(_pendingStyleImage);
            }
            else
            {
                _isExtractingStyle = false;
                _extractionOnlyMode = false;
            }
        }

        private void SendImageForExtraction(Texture2D image)
        {
            if (image == null) return;

            // Encode image to JPEG and then base64
            byte[] jpegData = image.EncodeToJPG(90);
            string base64Data = Convert.ToBase64String(jpegData);

            _ = SendImagePromptAsync(base64Data, enhancePrompt);
            Debug.Log($"[AlakazamController] Sent image for style extraction ({jpegData.Length} bytes)");
        }

        #region Unity Lifecycle

        private void Awake()
        {
            // Reset all runtime state - SerializedFields can persist from previous Play Mode sessions
            // This ensures a clean state when entering Play Mode
            isConnected = false;
            isStreaming = false;
            _isReady = false;
            _isCaptureLoopRunning = false;
            _extractionOnlyMode = false;
            _isExtractingStyle = false;
            _isUsingImageStyle = false;
            _pendingStyleImage = null;
            _lastPrompt = null;

            if (sourceCamera == null)
                sourceCamera = Camera.main;
        }

        private void Start()
        {
            if (outputDisplay == null && autoCreateDisplay)
            {
                CreateFullscreenDisplay();
            }
        }

        private void Update()
        {
            // Process received frames on main thread
            ProcessReceivedFrames();

            // Detect prompt changes from Inspector
            if (isStreaming && prompt != _lastPrompt)
            {
                _lastPrompt = prompt;
                _ = SendPromptAsync(prompt, enhancePrompt);
                Debug.Log($"[AlakazamController] Prompt changed: {prompt}");
            }

            // FPS counter
            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 1f)
            {
                currentFps = _fpsFrameCount / _fpsTimer;
                _fpsFrameCount = 0;
                _fpsTimer = 0f;
            }
        }

        private void OnDestroy()
        {
            Stop();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Start the Alakazam session and begin streaming.
        /// </summary>
        public void StartAlakazam()
        {
            _extractionOnlyMode = false; // Ensure we're in full streaming mode

            // If already connected and ready (e.g., from extraction), just start streaming
            if (isConnected && _isReady)
            {
                Debug.Log("[AlakazamController] Already connected, starting capture loop");

                // Setup capture resources if not already done (e.g., if we connected for extraction only)
                if (_captureRT == null)
                {
                    SetupCapture();
                }

                isStreaming = true;
                if (!_isCaptureLoopRunning)
                {
                    StartCoroutine(CaptureLoop());
                }
            }
            else
            {
                // Need to connect first
                StartCoroutine(ConnectAndStream());
            }
        }

        /// <summary>
        /// Stop streaming and disconnect.
        /// </summary>
        public void Stop()
        {
            isConnected = false;
            isStreaming = false;
            _isReady = false;
            _isCaptureLoopRunning = false;
            _extractionOnlyMode = false;
            _isExtractingStyle = false;
            _pendingStyleImage = null;
            StopAllCoroutines();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_ws != null)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                    {
                        _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None).Wait(1000);
                    }
                }
                catch { }
                _ws.Dispose();
                _ws = null;
            }

            if (_captureRT != null)
            {
                _captureRT.Release();
                Destroy(_captureRT);
                _captureRT = null;
            }
            if (_captureTexture != null)
            {
                Destroy(_captureTexture);
                _captureTexture = null;
            }
            if (_outputTexture != null)
            {
                Destroy(_outputTexture);
                _outputTexture = null;
            }

            framesSent = 0;
            framesReceived = 0;

            Debug.Log("[AlakazamController] Stopped");
        }

        /// <summary>
        /// Change the style prompt.
        /// </summary>
        public void SetPrompt(string newPrompt)
        {
            SetPrompt(newPrompt, enhancePrompt);
        }

        /// <summary>
        /// Change the style prompt with explicit enhance option.
        /// </summary>
        public void SetPrompt(string newPrompt, bool enhance)
        {
            prompt = newPrompt;
            enhancePrompt = enhance;
            if (isStreaming)
            {
                _ = SendPromptAsync(prompt, enhancePrompt);
            }
        }

        /// <summary>
        /// Toggle the effect (show original vs transformed).
        /// </summary>
        public void ToggleEffect()
        {
            if (outputDisplay != null)
            {
                outputDisplay.enabled = !outputDisplay.enabled;
            }
        }

        /// <summary>
        /// Set style from a reference image. The server will analyze the image
        /// and extract a style prompt using AI vision.
        /// </summary>
        /// <param name="referenceImage">Texture2D containing the reference style image</param>
        public void SetStyleFromImage(Texture2D referenceImage)
        {
            SetStyleFromImage(referenceImage, enhancePrompt);
        }

        /// <summary>
        /// Set style from a reference image with explicit enhance option.
        /// </summary>
        /// <param name="referenceImage">Texture2D containing the reference style image</param>
        /// <param name="enhance">Whether to enhance the extracted prompt</param>
        public void SetStyleFromImage(Texture2D referenceImage, bool enhance)
        {
            if (referenceImage == null)
            {
                Debug.LogError("[AlakazamController] Reference image is null");
                return;
            }

            if (!isStreaming)
            {
                Debug.LogWarning("[AlakazamController] Not streaming, cannot set style from image");
                return;
            }

            // Set extracting flag
            _isExtractingStyle = true;

            // Encode image to JPEG and then base64
            byte[] jpegData = referenceImage.EncodeToJPG(90);
            string base64Data = Convert.ToBase64String(jpegData);

            _ = SendImagePromptAsync(base64Data, enhance);
            Debug.Log($"[AlakazamController] Sending reference image for style extraction ({jpegData.Length} bytes)");
        }

        /// <summary>
        /// Set style from a base64-encoded image.
        /// </summary>
        /// <param name="base64ImageData">Base64-encoded JPEG image data</param>
        public void SetStyleFromBase64(string base64ImageData)
        {
            SetStyleFromBase64(base64ImageData, enhancePrompt);
        }

        /// <summary>
        /// Set style from a base64-encoded image with explicit enhance option.
        /// </summary>
        /// <param name="base64ImageData">Base64-encoded JPEG image data</param>
        /// <param name="enhance">Whether to enhance the extracted prompt</param>
        public void SetStyleFromBase64(string base64ImageData, bool enhance)
        {
            if (string.IsNullOrEmpty(base64ImageData))
            {
                Debug.LogError("[AlakazamController] Image data is null or empty");
                return;
            }

            if (!isStreaming)
            {
                Debug.LogWarning("[AlakazamController] Not streaming, cannot set style from image");
                return;
            }

            _ = SendImagePromptAsync(base64ImageData, enhance);
            Debug.Log("[AlakazamController] Sending base64 image for style extraction");
        }

        /// <summary>
        /// Event invoked when style is extracted from an image.
        /// </summary>
        public event Action<string> OnStyleExtracted;

        #endregion

        #region Connection

        private IEnumerator ConnectAndStream()
        {
            Debug.Log($"[AlakazamController] Connecting to {serverUrl}...");

            // Setup capture
            SetupCapture();

            // Connect WebSocket
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            var connectTask = _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);

            // Wait for connection
            float timeout = 10f;
            while (!connectTask.IsCompleted && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (connectTask.IsFaulted)
            {
                Debug.LogError($"[AlakazamController] Connection failed: {connectTask.Exception?.InnerException?.Message}");
                yield break;
            }

            if (!connectTask.IsCompleted)
            {
                Debug.LogError("[AlakazamController] Connection timeout");
                _cts.Cancel();
                yield break;
            }

            if (_ws.State != WebSocketState.Open)
            {
                Debug.LogError($"[AlakazamController] WebSocket not open: {_ws.State}");
                yield break;
            }

            Debug.Log("[AlakazamController] WebSocket connected");
            isConnected = true;

            // Start receive loop
            _ = ReceiveLoopAsync();

            // Send auth
            yield return SendAuthCoroutine();
        }

        private IEnumerator SendAuthCoroutine()
        {
            // Check for API key
            string apiKey = AlakazamAuth.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[AlakazamController] No API key configured. Open Alakazam > Setup Wizard to configure.");
                AlakazamAuth.HandleAuthFailed("No API key configured");
                yield break;
            }

            var authMsg = new AuthMessage
            {
                type = "auth",
                prompt = prompt,
                api_key = apiKey
            };

            var task = SendJsonAsync(authMsg);
            while (!task.IsCompleted) yield return null;

            _lastPrompt = prompt;
            Debug.Log("[AlakazamController] Auth sent");
        }

        private void SetupCapture()
        {
            _captureRT = new RenderTexture(captureWidth, captureHeight, 24);
            _captureRT.Create();

            _captureTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            _outputTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);

            if (outputDisplay != null)
            {
                outputDisplay.texture = _outputTexture;
            }

            Debug.Log($"[AlakazamController] Capture setup: {captureWidth}x{captureHeight}");
        }

        private void CreateFullscreenDisplay()
        {
            var canvasGO = new GameObject("AlakazamDisplay");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var imageGO = new GameObject("OutputImage");
            imageGO.transform.SetParent(canvasGO.transform);
            outputDisplay = imageGO.AddComponent<RawImage>();
            outputDisplay.color = Color.white;

            var rect = outputDisplay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Debug.Log("[AlakazamController] Created fullscreen display");
        }

        #endregion

        #region WebSocket Communication

        private async Task SendJsonAsync<T>(T message)
        {
            if (_ws?.State != WebSocketState.Open) return;

            try
            {
                string json = JsonUtility.ToJson(message);
                byte[] data = Encoding.UTF8.GetBytes(json);
                await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AlakazamController] Send error: {e.Message}");
            }
        }

        private async Task SendBinaryAsync(byte[] data)
        {
            if (_ws?.State != WebSocketState.Open) return;

            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, _cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AlakazamController] Send error: {e.Message}");
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[1024 * 1024]; // 1MB buffer for frames
            var messageBuffer = new List<byte>();

            try
            {
                while (_ws?.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[AlakazamController] Server closed connection");
                        isConnected = false;
                        isStreaming = false;
                        break;
                    }

                    messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        byte[] messageData = messageBuffer.ToArray();
                        messageBuffer.Clear();

                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            // Binary = stylized frame
                            lock (_frameLock)
                            {
                                _receivedFrames.Enqueue(messageData);
                            }
                        }
                        else if (result.MessageType == WebSocketMessageType.Text)
                        {
                            // Text = JSON message
                            string json = Encoding.UTF8.GetString(messageData);
                            HandleMessage(json);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on disconnect
            }
            catch (Exception e)
            {
                Debug.LogError($"[AlakazamController] Receive error: {e.Message}");
                isConnected = false;
                isStreaming = false;
            }
        }

        private async Task SendPromptAsync(string newPrompt, bool enhance)
        {
            var msg = new PromptMessage
            {
                type = "prompt",
                prompt = newPrompt,
                enhance = enhance
            };
            await SendJsonAsync(msg);
            Debug.Log($"[AlakazamController] Prompt sent: {newPrompt}");
        }

        private async Task SendImagePromptAsync(string base64ImageData, bool enhance)
        {
            var msg = new ImagePromptMessage
            {
                type = "image_prompt",
                image_data = base64ImageData,
                enhance = enhance
            };
            await SendJsonAsync(msg);
        }

        private void HandleMessage(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<ServerMessage>(json);

                switch (msg.type)
                {
                    case "ready":
                        Debug.Log($"[AlakazamController] Ready! Session: {msg.session_id}, {msg.width}x{msg.height}");

                        // Update usage info
                        if (msg.usage != null)
                        {
                            AlakazamAuth.UpdateUsage(
                                msg.usage.seconds_used,
                                msg.usage.seconds_limit,
                                msg.usage.seconds_remaining
                            );
                        }

                        // Handle server warning (80%+ usage)
                        if (!string.IsNullOrEmpty(msg.warning))
                        {
                            AlakazamAuth.HandleWarning(msg.warning);
                        }

                        _isReady = true;

                        // Only start capture loop if NOT in extraction-only mode
                        if (_extractionOnlyMode)
                        {
                            Debug.Log("[AlakazamController] Extraction-only mode - NOT starting capture loop");
                        }
                        else
                        {
                            Debug.Log("[AlakazamController] Starting capture loop");
                            isStreaming = true;
                            if (!_isCaptureLoopRunning)
                            {
                                StartCoroutine(CaptureLoop());
                            }
                        }
                        break;

                    case "error":
                        Debug.LogError($"[AlakazamController] Server error: {msg.message}");
                        AlakazamAuth.HandleAuthFailed(msg.message);
                        break;

                    case "style_extracted":
                        string extractedPrompt = msg.prompt;
                        Debug.Log($"[AlakazamController] Style extracted: {extractedPrompt}");
                        prompt = extractedPrompt;
                        _lastPrompt = extractedPrompt;
                        _isExtractingStyle = false;
                        _isUsingImageStyle = true;
                        // Keep _extractionOnlyMode = true until user explicitly starts streaming
                        _pendingStyleImage = null;
                        OnStyleExtracted?.Invoke(extractedPrompt);
                        break;

                    default:
                        Debug.Log($"[AlakazamController] Message: {json}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AlakazamController] Failed to parse message: {e.Message}");
            }
        }

        #endregion

        #region Frame Handling

        private IEnumerator CaptureLoop()
        {
            // Safety check - never run capture loop in extraction-only mode
            if (_extractionOnlyMode)
            {
                Debug.LogWarning("[AlakazamController] CaptureLoop blocked - extraction only mode");
                yield break;
            }

            _isCaptureLoopRunning = true;
            float frameInterval = 1f / targetFps;
            Debug.Log($"[AlakazamController] Starting capture at {targetFps} FPS");

            while (isStreaming && !_extractionOnlyMode)
            {
                CaptureAndSendFrame();
                yield return new WaitForSeconds(frameInterval);
            }

            _isCaptureLoopRunning = false;
            Debug.Log("[AlakazamController] Capture loop stopped");
        }

        private void CaptureAndSendFrame()
        {
            if (sourceCamera == null || _captureRT == null || _ws == null || !isStreaming)
                return;

            // Render camera to capture texture
            var originalRT = sourceCamera.targetTexture;
            sourceCamera.targetTexture = _captureRT;
            sourceCamera.Render();
            sourceCamera.targetTexture = originalRT;

            // Read pixels
            RenderTexture.active = _captureRT;
            _captureTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            _captureTexture.Apply();
            RenderTexture.active = null;

            // Encode frame
            byte[] jpegData = _captureTexture.EncodeToJPG(jpegQuality);

            // Send over WebSocket
            _ = SendBinaryAsync(jpegData);
            framesSent++;

            if (framesSent <= 5 || framesSent % 100 == 0)
            {
                Debug.Log($"[AlakazamController] Sent frame {framesSent}: {jpegData.Length} bytes");
            }
        }

        private void ProcessReceivedFrames()
        {
            byte[] frameData = null;

            lock (_frameLock)
            {
                if (_receivedFrames.Count > 0)
                {
                    // Take latest frame, discard older ones
                    while (_receivedFrames.Count > 1)
                        _receivedFrames.Dequeue();
                    frameData = _receivedFrames.Dequeue();
                }
            }

            if (frameData != null)
            {
                // Decode stylized frame
                _outputTexture.LoadImage(frameData);

                if (outputDisplay != null)
                {
                    outputDisplay.texture = _outputTexture;
                }

                framesReceived++;
                _fpsFrameCount++;

                if (framesReceived <= 5 || framesReceived % 100 == 0)
                {
                    Debug.Log($"[AlakazamController] Received frame {framesReceived}: {frameData.Length} bytes");
                }
            }
        }

        #endregion

        #region Message Types

        [Serializable]
        private class AuthMessage
        {
            public string type;
            public string prompt;
            public string api_key;
        }

        [Serializable]
        private class PromptMessage
        {
            public string type;
            public string prompt;
            public bool enhance;
        }

        [Serializable]
        private class ImagePromptMessage
        {
            public string type;
            public string image_data;
            public bool enhance;
        }

        [Serializable]
        private class ServerMessage
        {
            public string type;
            public string session_id;
            public int width;
            public int height;
            public string message;
            public string warning;
            public string prompt;  // For style_extracted response
            public UsageData usage;
        }

        [Serializable]
        private class UsageData
        {
            public int seconds_used;
            public int seconds_limit;
            public int seconds_remaining;
        }

        #endregion
    }
}
