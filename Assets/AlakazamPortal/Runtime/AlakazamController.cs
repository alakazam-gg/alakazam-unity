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

        public bool IsConnected => isConnected;
        public bool IsStreaming => isStreaming;
        public string Prompt => prompt;
        public RawImage OutputDisplay => outputDisplay;

        #region Unity Lifecycle

        private void Awake()
        {
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
            StartCoroutine(ConnectAndStream());
        }

        /// <summary>
        /// Stop streaming and disconnect.
        /// </summary>
        public void Stop()
        {
            isConnected = false;
            isStreaming = false;
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

                        isStreaming = true;
                        StartCoroutine(CaptureLoop());
                        break;

                    case "error":
                        Debug.LogError($"[AlakazamController] Server error: {msg.message}");
                        AlakazamAuth.HandleAuthFailed(msg.message);
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
            float frameInterval = 1f / targetFps;
            Debug.Log($"[AlakazamController] Starting capture at {targetFps} FPS");

            while (isStreaming)
            {
                CaptureAndSendFrame();
                yield return new WaitForSeconds(frameInterval);
            }
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
        private class ServerMessage
        {
            public string type;
            public string session_id;
            public int width;
            public int height;
            public string message;
            public string warning;
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
