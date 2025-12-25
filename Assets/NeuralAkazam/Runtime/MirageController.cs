using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;

namespace NeuralAkazam
{
    /// <summary>
    /// Main controller for MirageLSD real-time video transformation.
    ///
    /// Attach to a GameObject with a Camera to capture frames from.
    /// The transformed output is displayed on the specified RawImage.
    ///
    /// Requires:
    /// - Unity WebRTC package (com.unity.webrtc)
    /// - WebSocketSharp (websocket-sharp on NuGet)
    /// </summary>
    public class MirageController : MonoBehaviour
    {
        [Header("API Configuration")]
        [SerializeField] private string apiKey = "";

        [Header("Prompt")]
        [SerializeField] private string prompt = "anime style, vibrant colors";
        [SerializeField] private bool enhancePrompt = true;

        [Header("Input")]
        [SerializeField] private Camera sourceCamera;

        [Header("Output")]
        [Tooltip("UI RawImage to display output. If null and autoCreateDisplay is true, one will be created.")]
        [SerializeField] private RawImage outputDisplay;
        [Tooltip("Automatically create a fullscreen canvas if no outputDisplay is assigned")]
        [SerializeField] private bool autoCreateDisplay = true;
        [Tooltip("Also render to a RenderTexture (for use in materials/shaders)")]
        [SerializeField] private RenderTexture outputRenderTexture;

        [Header("Status")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private bool isStreaming = false;
        [SerializeField] private float currentFps = 0f;
        [SerializeField] private string activePrompt = "";  // Shows what prompt is actually being used

        // Components
        private MirageSession _session;
        private MirageSignaling _signaling;

        // WebRTC
        private RTCPeerConnection _peerConnection;
        private VideoStreamTrack _videoTrack;
        private MediaStream _sendStream;
        private RenderTexture _captureRT;
        private Texture2D _captureTexture;

        // Output
        private Texture2D _outputTexture;

        // Incoming video (must store reference to prevent GC)
        private VideoStreamTrack _incomingVideoTrack;
        private MediaStream _receiveStream;

        // State
        private bool _isInitialized = false;
        private int _frameCount = 0;
        private float _fpsTimer = 0f;
        private bool _offerSent = false;
        private List<(string candidate, string sdpMid, int sdpMLineIndex)> _pendingCandidates = new List<(string, string, int)>();
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
            // CRITICAL: WebRTC.Update() must be called for video frame copying
            StartCoroutine(WebRTC.Update());

            // Auto-create fullscreen display if needed
            if (outputDisplay == null && autoCreateDisplay)
            {
                CreateFullscreenDisplay();
            }
        }

        /// <summary>
        /// Creates a fullscreen Canvas with RawImage to display the MirageLSD output.
        /// </summary>
        private void CreateFullscreenDisplay()
        {
            // Create Canvas
            var canvasGO = new GameObject("MirageDisplay");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // On top of everything

            // Add CanvasScaler for proper scaling
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Create RawImage
            var imageGO = new GameObject("OutputImage");
            imageGO.transform.SetParent(canvasGO.transform);
            outputDisplay = imageGO.AddComponent<RawImage>();
            outputDisplay.color = Color.white;

            // Make it fullscreen
            var rect = outputDisplay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Debug.Log("[MirageController] Created fullscreen display");
        }

        private void Update()
        {
            // Process signaling messages on main thread
            _signaling?.ProcessMessages();

            // Detect prompt changes from Inspector
            if (isConnected && prompt != _lastPrompt)
            {
                _lastPrompt = prompt;
                activePrompt = prompt;
                _signaling?.SendPrompt(prompt, enhancePrompt);
                Debug.Log($"[MirageController] Prompt changed: {prompt}");
            }

            // FPS counter
            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 1f)
            {
                currentFps = _frameCount / _fpsTimer;
                _frameCount = 0;
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
        /// Start the MirageLSD session and begin streaming.
        /// </summary>
        public void StartMirage()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[MirageController] API key is required");
                return;
            }

            StartCoroutine(InitializeAndConnect());
        }

        /// <summary>
        /// Stop streaming and disconnect.
        /// </summary>
        public void Stop()
        {
            isStreaming = false;
            isConnected = false;
            _offerSent = false;
            _pendingCandidates.Clear();

            _incomingVideoTrack?.Dispose();
            _videoTrack?.Dispose();
            _sendStream?.Dispose();
            _receiveStream?.Dispose();
            _peerConnection?.Close();
            _peerConnection?.Dispose();
            _signaling?.Dispose();
            _session?.Dispose();

            if (_captureRT != null)
            {
                _captureRT.Release();
                Destroy(_captureRT);
            }
            if (_captureTexture != null)
                Destroy(_captureTexture);
            if (_outputTexture != null)
                Destroy(_outputTexture);

            _isInitialized = false;
            Debug.Log("[MirageController] Stopped");
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
            if (_signaling != null && _signaling.IsConnected)
            {
                _signaling.SendPrompt(prompt, enhancePrompt);
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

        #region Connection Flow

        private IEnumerator InitializeAndConnect()
        {
            Debug.Log("[MirageController] Starting...");

            // Step 1: Create session via HTTP (get WebSocket URL)
            _session = new MirageSession(apiKey);
            SessionConfig config = null;
            string sessionError = null;

            yield return _session.CreateSession(
                cfg => config = cfg,
                err => sessionError = err
            );

            if (sessionError != null)
            {
                Debug.LogError($"[MirageController] Failed to create session: {sessionError}");
                yield break;
            }

            if (config == null)
            {
                Debug.LogError("[MirageController] Session config is null");
                yield break;
            }

            // Step 2: Setup capture textures
            SetupCapture(config.inputWidth, config.inputHeight);

            // Step 3: Setup WebRTC
            SetupWebRTC();

            // Step 4: Connect signaling (WebSocket URL from session)
            _signaling = new MirageSignaling();
            _signaling.OnConnected += OnSignalingConnected;
            _signaling.OnAnswer += OnSdpAnswer;
            _signaling.OnIceCandidate += OnRemoteIceCandidate;
            _signaling.OnGenerationStarted += OnGenerationStarted;
            _signaling.OnError += e => Debug.LogError($"[MirageController] Signaling error: {e}");

            // Note: API key already validated via HTTP, WebSocket doesn't need it
            _signaling.Connect(config.websocketUrl);

            yield return null;
        }

        private void SetupCapture(int width, int height)
        {
            _captureRT = new RenderTexture(width, height, 24);
            _captureRT.Create();

            _captureTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _outputTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            if (outputDisplay != null)
            {
                outputDisplay.texture = _outputTexture;
            }

            Debug.Log($"[MirageController] Capture setup: {width}x{height}");
        }

        private void SetupWebRTC()
        {
            var configuration = new RTCConfiguration
            {
                iceServers = new[]
                {
                    new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
                }
            };

            _peerConnection = new RTCPeerConnection(ref configuration);

            _peerConnection.OnIceCandidate = candidate =>
            {
                var candidateStr = candidate.Candidate;
                var sdpMid = candidate.SdpMid;
                var sdpMLineIndex = candidate.SdpMLineIndex ?? 0;

                if (_offerSent)
                {
                    // Offer already sent, send candidate immediately
                    _signaling?.SendIceCandidate(candidateStr, sdpMid, sdpMLineIndex);
                }
                else
                {
                    // Buffer candidate until offer is sent
                    _pendingCandidates.Add((candidateStr, sdpMid, sdpMLineIndex));
                    Debug.Log($"[MirageController] Buffered ICE candidate (offer not sent yet)");
                }
            };

            _peerConnection.OnIceConnectionChange = state =>
            {
                Debug.Log($"[MirageController] ICE state: {state}");
                isConnected = state == RTCIceConnectionState.Connected;

                // Start capture loop when connected (don't wait for generation_started)
                if (state == RTCIceConnectionState.Connected && !isStreaming)
                {
                    isStreaming = true;
                    StartCoroutine(CaptureLoop());
                }
            };

            _peerConnection.OnTrack = e =>
            {
                Debug.Log($"[MirageController] Received track: {e.Track.Kind}");

                // Store the streams to prevent GC
                if (e.Streams != null && e.Streams.Any())
                {
                    _receiveStream = e.Streams.First();
                    Debug.Log($"[MirageController] Stored receive stream: {_receiveStream.Id}");
                }

                if (e.Track is VideoStreamTrack videoTrack)
                {
                    Debug.Log($"[MirageController] Subscribing to video track: {videoTrack.Id}, Enabled: {videoTrack.Enabled}");
                    // Store reference to prevent GC
                    _incomingVideoTrack = videoTrack;
                    // Ensure track is enabled
                    _incomingVideoTrack.Enabled = true;

                    // Method 1: Subscribe to incoming frames callback
                    videoTrack.OnVideoReceived += OnVideoFrameReceived;
                    Debug.Log("[MirageController] Subscribed to OnVideoReceived");

                    // Method 2: Also start polling the Texture property as fallback
                    StartCoroutine(PollIncomingTexture());
                }
            };

            // Create video track from camera
            _videoTrack = new VideoStreamTrack(_captureRT);
            _sendStream = new MediaStream();
            _sendStream.AddTrack(_videoTrack);

            // Add transceiver for sending video (also enables receiving)
            var transceiver = _peerConnection.AddTransceiver(_videoTrack);
            transceiver.Direction = RTCRtpTransceiverDirection.SendRecv;

            Debug.Log($"[MirageController] Added transceiver with direction: {transceiver.Direction}");
        }

        #endregion

        #region Signaling Handlers

        private void OnSignalingConnected()
        {
            Debug.Log("[MirageController] Signaling connected, sending offer...");

            // Create and send offer immediately after connection
            StartCoroutine(CreateAndSendOffer());
        }

        private IEnumerator CreateAndSendOffer()
        {
            var offer = _peerConnection.CreateOffer();
            yield return offer;

            if (offer.IsError)
            {
                Debug.LogError($"[MirageController] Failed to create offer: {offer.Error.message}");
                yield break;
            }

            var desc = offer.Desc;
            var setLocal = _peerConnection.SetLocalDescription(ref desc);
            yield return setLocal;

            if (setLocal.IsError)
            {
                Debug.LogError($"[MirageController] Failed to set local description: {setLocal.Error.message}");
                yield break;
            }

            _signaling.SendOffer(desc.sdp);
            Debug.Log("[MirageController] Offer sent");

            // Send prompt immediately after offer (before ICE candidates)
            _signaling.SendPrompt(prompt, enhancePrompt);
            _lastPrompt = prompt;  // Track to detect Inspector changes
            activePrompt = prompt;
            Debug.Log($"[MirageController] Initial prompt sent: {prompt}");

            _offerSent = true;

            // Now send any buffered ICE candidates
            foreach (var pending in _pendingCandidates)
            {
                _signaling.SendIceCandidate(pending.candidate, pending.sdpMid, pending.sdpMLineIndex);
            }
            if (_pendingCandidates.Count > 0)
            {
                Debug.Log($"[MirageController] Sent {_pendingCandidates.Count} buffered ICE candidates");
            }
            _pendingCandidates.Clear();
        }

        private void OnSdpAnswer(string sdp)
        {
            StartCoroutine(SetRemoteDescription(sdp));
        }

        private IEnumerator SetRemoteDescription(string sdp)
        {
            var desc = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = sdp
            };

            var setRemote = _peerConnection.SetRemoteDescription(ref desc);
            yield return setRemote;

            if (setRemote.IsError)
            {
                Debug.LogError($"[MirageController] Failed to set remote description: {setRemote.Error.message}");
                yield break;
            }

            Debug.Log("[MirageController] Remote description set, WebRTC negotiation complete");
        }

        private void OnRemoteIceCandidate(string candidate, string sdpMid, int sdpMLineIndex)
        {
            try
            {
                var iceCandidate = new RTCIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidate,
                    sdpMid = sdpMid,
                    sdpMLineIndex = sdpMLineIndex
                });
                _peerConnection.AddIceCandidate(iceCandidate);
                Debug.Log($"[MirageController] Added ICE candidate: {candidate}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MirageController] Failed to add ICE candidate: {e.Message}");
            }
        }

        private void OnGenerationStarted()
        {
            isStreaming = true;
            StartCoroutine(CaptureLoop());
        }

        #endregion

        #region Frame Handling

        private IEnumerator CaptureLoop()
        {
            var config = _session.Config;
            float frameRate = config.FrameRate;
            float frameInterval = 1f / frameRate;

            Debug.Log($"[MirageController] Starting capture at {frameRate} FPS");

            while (isStreaming)
            {
                CaptureFrame();
                yield return new WaitForSeconds(frameInterval);
            }
        }

        private int _captureCount = 0;

        private void CaptureFrame()
        {
            if (sourceCamera == null || _captureRT == null)
            {
                Debug.LogWarning("[MirageController] Cannot capture: camera or RT is null");
                return;
            }

            // Render camera to our capture texture
            var originalRT = sourceCamera.targetTexture;
            sourceCamera.targetTexture = _captureRT;
            sourceCamera.Render();
            sourceCamera.targetTexture = originalRT;

            _captureCount++;
            if (_captureCount < 5 || _captureCount % 100 == 0)
            {
                Debug.Log($"[MirageController] Captured frame {_captureCount}");
            }
        }

        /// <summary>
        /// Fallback polling method to check for incoming video texture.
        /// Unity WebRTC sometimes doesn't fire OnVideoReceived.
        /// </summary>
        private IEnumerator PollIncomingTexture()
        {
            Debug.Log("[MirageController] Starting texture polling");
            int pollCount = 0;

            while (isStreaming && _incomingVideoTrack != null)
            {
                pollCount++;
                var texture = _incomingVideoTrack.Texture;

                if (texture != null)
                {
                    if (pollCount <= 5 || pollCount % 100 == 0)
                    {
                        Debug.Log($"[MirageController] Polled texture {pollCount}: {texture.width}x{texture.height}");
                    }

                    // Update display with polled texture
                    if (outputDisplay != null)
                    {
                        outputDisplay.texture = texture;
                    }

                    if (outputRenderTexture != null)
                    {
                        Graphics.Blit(texture, outputRenderTexture);
                    }

                    _frameCount++;
                }
                else if (pollCount <= 10 || pollCount % 100 == 0)
                {
                    Debug.Log($"[MirageController] Poll {pollCount}: texture is null");
                }

                yield return null; // Poll every frame
            }

            Debug.Log("[MirageController] Texture polling stopped");
        }

        private void OnVideoFrameReceived(Texture texture)
        {
            if (texture == null)
            {
                Debug.LogWarning("[MirageController] Received null texture");
                return;
            }

            // Log first few frames and then periodically
            if (_frameCount < 5 || _frameCount % 100 == 0)
            {
                Debug.Log($"[MirageController] Received frame {_frameCount}: {texture.width}x{texture.height}");
            }

            // Update RawImage display directly with the received texture
            if (outputDisplay != null)
            {
                outputDisplay.texture = texture;
            }

            // Copy to optional RenderTexture (for use in materials/shaders)
            if (outputRenderTexture != null)
            {
                Graphics.Blit(texture, outputRenderTexture);
            }

            _frameCount++;
        }

        #endregion
    }
}
