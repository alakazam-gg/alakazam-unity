using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace NeuralAkazam
{
    /// <summary>
    /// Simple WebSocket client for Alakazam proxy server.
    /// No WebRTC needed - just sends/receives JPEG frames over WebSocket.
    /// </summary>
    public class AlakazamClient : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "ws://localhost:9001";
        [SerializeField] private string apiKey = "";

        [Header("Prompt")]
        [SerializeField] private string prompt = "minecraft village";
        [SerializeField] private bool enhancePrompt = true;

        [Header("Input")]
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private int captureWidth = 1280;
        [SerializeField] private int captureHeight = 720;
        [SerializeField] private int jpegQuality = 85;
        [SerializeField] private float targetFps = 30f;

        [Header("Output")]
        [SerializeField] private RawImage outputDisplay;
        [SerializeField] private bool autoCreateDisplay = true;

        [Header("Status")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private bool isReady = false;
        [SerializeField] private int framesSent = 0;
        [SerializeField] private int framesReceived = 0;
        [SerializeField] private float currentFps = 0f;

        // WebSocket
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly Queue<byte[]> _receivedFrames = new Queue<byte[]>();
        private readonly object _frameLock = new object();

        // Capture
        private RenderTexture _captureRT;
        private Texture2D _captureTexture;
        private Texture2D _outputTexture;

        // FPS tracking
        private float _fpsTimer = 0f;
        private int _fpsFrameCount = 0;
        private string _lastPrompt;

        public bool IsConnected => isConnected;
        public bool IsReady => isReady;

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

            // Detect prompt changes
            if (isReady && prompt != _lastPrompt)
            {
                _lastPrompt = prompt;
                _ = SendPromptAsync(prompt, enhancePrompt);
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
            Disconnect();
        }

        #endregion

        #region Public API

        public void Connect()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[AlakazamClient] API key is required");
                return;
            }

            StartCoroutine(ConnectCoroutine());
        }

        public void Disconnect()
        {
            isConnected = false;
            isReady = false;
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
            }
            if (_captureTexture != null)
                Destroy(_captureTexture);
            if (_outputTexture != null)
                Destroy(_outputTexture);

            Debug.Log("[AlakazamClient] Disconnected");
        }

        public void SetPrompt(string newPrompt, bool enhance = true)
        {
            prompt = newPrompt;
            enhancePrompt = enhance;
            if (isReady)
            {
                _ = SendPromptAsync(prompt, enhancePrompt);
            }
        }

        #endregion

        #region Connection

        private IEnumerator ConnectCoroutine()
        {
            Debug.Log($"[AlakazamClient] Connecting to {serverUrl}...");

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
                Debug.LogError($"[AlakazamClient] Connection failed: {connectTask.Exception?.InnerException?.Message}");
                yield break;
            }

            if (!connectTask.IsCompleted)
            {
                Debug.LogError("[AlakazamClient] Connection timeout");
                _cts.Cancel();
                yield break;
            }

            if (_ws.State != WebSocketState.Open)
            {
                Debug.LogError($"[AlakazamClient] WebSocket not open: {_ws.State}");
                yield break;
            }

            Debug.Log("[AlakazamClient] WebSocket connected");
            isConnected = true;

            // Start receive loop
            _ = ReceiveLoopAsync();

            // Send auth
            yield return SendAuthCoroutine();
        }

        private IEnumerator SendAuthCoroutine()
        {
            var auth = new AuthMessage
            {
                type = "auth",
                api_key = apiKey,
                prompt = prompt
            };

            var task = SendJsonAsync(auth);
            while (!task.IsCompleted) yield return null;

            Debug.Log("[AlakazamClient] Auth sent");
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

            Debug.Log($"[AlakazamClient] Capture setup: {captureWidth}x{captureHeight}");
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

            Debug.Log("[AlakazamClient] Created fullscreen display");
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
                Debug.LogError($"[AlakazamClient] Send error: {e.Message}");
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
                Debug.LogError($"[AlakazamClient] Send error: {e.Message}");
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
                        Debug.Log("[AlakazamClient] Server closed connection");
                        isConnected = false;
                        isReady = false;
                        break;
                    }

                    messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        byte[] messageData = messageBuffer.ToArray();
                        messageBuffer.Clear();

                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            // Binary = JPEG frame
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
                Debug.LogError($"[AlakazamClient] Receive error: {e.Message}");
                isConnected = false;
                isReady = false;
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
            Debug.Log($"[AlakazamClient] Prompt sent: {newPrompt}");
        }

        private void HandleMessage(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<ServerMessage>(json);

                switch (msg.type)
                {
                    case "ready":
                        Debug.Log($"[AlakazamClient] Ready! Session: {msg.session_id}, {msg.width}x{msg.height}");
                        isReady = true;
                        _lastPrompt = prompt;
                        StartCoroutine(CaptureLoop());
                        break;

                    case "error":
                        Debug.LogError($"[AlakazamClient] Server error: {msg.message}");
                        break;

                    default:
                        Debug.Log($"[AlakazamClient] Message: {json}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AlakazamClient] Failed to parse message: {e.Message}");
            }
        }

        #endregion

        #region Frame Handling

        private IEnumerator CaptureLoop()
        {
            float frameInterval = 1f / targetFps;
            Debug.Log($"[AlakazamClient] Starting capture at {targetFps} FPS");

            while (isReady)
            {
                CaptureAndSendFrame();
                yield return new WaitForSeconds(frameInterval);
            }
        }

        private void CaptureAndSendFrame()
        {
            if (sourceCamera == null || _captureRT == null || _ws == null || !isReady)
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

            // Encode to JPEG
            byte[] jpegData = _captureTexture.EncodeToJPG(jpegQuality);

            // Send over WebSocket
            _ = SendBinaryAsync(jpegData);
            framesSent++;

            if (framesSent <= 5 || framesSent % 100 == 0)
            {
                Debug.Log($"[AlakazamClient] Sent frame {framesSent}: {jpegData.Length} bytes");
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
                // Decode JPEG
                _outputTexture.LoadImage(frameData);

                if (outputDisplay != null)
                {
                    outputDisplay.texture = _outputTexture;
                }

                framesReceived++;
                _fpsFrameCount++;

                if (framesReceived <= 5 || framesReceived % 100 == 0)
                {
                    Debug.Log($"[AlakazamClient] Received frame {framesReceived}: {frameData.Length} bytes");
                }
            }
        }

        #endregion

        #region Message Types

        [Serializable]
        private class AuthMessage
        {
            public string type;
            public string api_key;
            public string prompt;
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
        }

        #endregion
    }
}
