using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NeuralAkazam
{
    /// <summary>
    /// Handles MirageLSD session configuration.
    /// Uses Decart's platform API - first creates session via HTTP, then connects via WebSocket.
    ///
    /// API Flow:
    /// 1. POST to https://oasis2.decart.ai/api/create-session with API key
    /// 2. Get WebSocket URL from response
    /// 3. Connect to WebSocket for signaling
    /// </summary>
    public class MirageSession
    {
        private const string CREATE_SESSION_URL = "https://oasis2.decart.ai/api/create-session";

        private readonly string _apiKey;
        private SessionConfig _config;

        public SessionConfig Config => _config;
        public bool IsConfigured => _config != null;
        public string ApiKey => _apiKey;

        public MirageSession(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>
        /// Create session via HTTP POST and get WebSocket URL.
        /// Returns IEnumerator for use in coroutines.
        /// </summary>
        public IEnumerator CreateSession(Action<SessionConfig> onSuccess, Action<string> onError)
        {
            Debug.Log("[MirageSession] Creating session...");

            // Build UserAgent JSON (matching Minecraft mod format exactly)
            var userAgent = new UserAgentData
            {
                javaVersion = "unity-" + Application.unityVersion,
                minecraftVersion = "unity",
                modId = "neuralakazam",
                modVersion = "1.0.0",
                osName = SystemInfo.operatingSystem,
                osArch = Environment.Is64BitOperatingSystem ? "amd64" : "x86",
                cpu = SystemInfo.processorType,
                gpuRenderer = SystemInfo.graphicsDeviceName,
                gpuVendor = SystemInfo.graphicsDeviceVendor,
                gpuBackend = SystemInfo.graphicsDeviceType.ToString(),
                gpuVersion = SystemInfo.graphicsDeviceVersion,
                apiKey = _apiKey,
                width = Screen.width,
                height = Screen.height
            };

            string jsonBody = JsonUtility.ToJson(userAgent);
            Debug.Log($"[MirageSession] Request body: {jsonBody}");

            using (var request = new UnityWebRequest(CREATE_SESSION_URL, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[MirageSession] HTTP error: {request.error}");
                    Debug.LogError($"[MirageSession] Response: {request.downloadHandler.text}");
                    onError?.Invoke($"Session creation failed: {request.error}");
                    yield break;
                }

                string response = request.downloadHandler.text;
                Debug.Log($"[MirageSession] Response: {response}");

                try
                {
                    _config = JsonUtility.FromJson<SessionConfig>(response);
                    Debug.Log($"[MirageSession] Session created: {_config.sessionId}");
                    Debug.Log($"[MirageSession] WebSocket URL: {_config.websocketUrl}");
                    Debug.Log($"[MirageSession] Resolution: {_config.inputWidth}x{_config.inputHeight}");
                    onSuccess?.Invoke(_config);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MirageSession] Failed to parse response: {e.Message}");
                    onError?.Invoke($"Failed to parse session config: {e.Message}");
                }
            }
        }

        public void Dispose()
        {
            _config = null;
        }
    }

    /// <summary>
    /// User agent data sent to create-session endpoint.
    /// Field names must match exactly what the server expects.
    /// </summary>
    [Serializable]
    public class UserAgentData
    {
        public string javaVersion;
        public string minecraftVersion;
        public string modId;
        public string modVersion;
        public string osName;
        public string osArch;
        public string cpu;
        public string gpuRenderer;
        public string gpuVendor;
        public string gpuBackend;
        public string gpuVersion;
        public string apiKey;
        public int width;
        public int height;
    }

    /// <summary>
    /// Configuration for MirageLSD session (returned from create-session).
    /// Field names match the server response exactly.
    /// </summary>
    [Serializable]
    public class SessionConfig
    {
        // Session info
        public string sessionId;
        public string websocketUrl;

        // Timeouts
        public long websocketConnectTimeoutMs;
        public long totalConnectTimeoutMs;

        // Frame rate (may be fractional)
        public int fpsNumerator;
        public int fpsDenominator;
        public float fpsStableMinimum;

        // Video dimensions (note different field names)
        public int inputVideoWidth;
        public int inputVideoHeight;
        public int maxOutputVideoWidth;
        public int maxOutputVideoHeight;

        // Helper properties
        public float FrameRate => fpsDenominator > 0 ? (float)fpsNumerator / fpsDenominator : 20f;
        public int inputWidth => inputVideoWidth;
        public int inputHeight => inputVideoHeight;
        public int outputWidth => maxOutputVideoWidth;
        public int outputHeight => maxOutputVideoHeight;
    }
}
