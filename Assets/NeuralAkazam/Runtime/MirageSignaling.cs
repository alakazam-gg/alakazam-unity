using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NeuralAkazam
{
    /// <summary>
    /// WebSocket signaling for WebRTC connection establishment.
    /// Handles SDP offer/answer exchange and ICE candidate relay.
    ///
    /// Protocol (based on Decart Oasis 2.0):
    /// - Client sends: offer, ice-candidate, prompt
    /// - Server sends: answer, ice-candidate, prompt_ack, generation_started, error
    /// </summary>
    public class MirageSignaling : IDisposable
    {
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private readonly Queue<SignalingMessage> _messageQueue = new Queue<SignalingMessage>();
        private readonly object _queueLock = new object();
        private bool _isConnected;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;
        public event Action<string> OnAnswer;           // SDP answer
        public event Action<string, string, int> OnIceCandidate;  // candidate, sdpMid, sdpMLineIndex
        public event Action OnPromptAck;
        public event Action OnGenerationStarted;

        public async void Connect(string websocketUrl)
        {
            if (_webSocket != null)
            {
                _webSocket.Dispose();
            }

            Debug.Log($"[MirageSignaling] Connecting to {websocketUrl}");

            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            try
            {
                await _webSocket.ConnectAsync(new Uri(websocketUrl), _cts.Token);
                _isConnected = true;
                Debug.Log("[MirageSignaling] Connected");
                OnConnected?.Invoke();

                // Start receiving messages
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MirageSignaling] Connection failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        private async Task ReceiveLoop()
        {
            Debug.Log("[MirageSignaling] Receive loop started");
            var buffer = new byte[8192];

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    Debug.Log("[MirageSignaling] Waiting for message...");
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), _cts.Token);

                    Debug.Log($"[MirageSignaling] Received: type={result.MessageType}, count={result.Count}");

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        Debug.Log("[MirageSignaling] Server closed connection");
                        OnDisconnected?.Invoke("Server closed connection");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleMessage(json);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MirageSignaling] Receive error: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                _isConnected = false;
            }
        }

        private void HandleMessage(string json)
        {
            Debug.Log($"[MirageSignaling] Raw message: {json}");

            try
            {
                // First parse to get message type
                var baseMsg = JsonUtility.FromJson<SignalingMessage>(json);

                // For ice-candidate, we need special handling due to nested structure
                if (baseMsg.type == "ice-candidate")
                {
                    var iceMsg = JsonUtility.FromJson<IncomingIceCandidateMessage>(json);
                    if (iceMsg.candidate != null)
                    {
                        // Convert nested candidate to the flat format expected by ProcessMessage
                        baseMsg.candidate = iceMsg.candidate.candidate;
                        baseMsg.sdpMid = iceMsg.candidate.sdpMid;
                        baseMsg.sdpMLineIndex = iceMsg.candidate.sdpMLineIndex;
                    }
                }

                lock (_queueLock)
                {
                    _messageQueue.Enqueue(baseMsg);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MirageSignaling] Failed to parse message: {ex.Message}\n{json}");
            }
        }

        /// <summary>
        /// Process queued messages on the main thread.
        /// Call this from Update().
        /// </summary>
        public void ProcessMessages()
        {
            lock (_queueLock)
            {
                while (_messageQueue.Count > 0)
                {
                    var msg = _messageQueue.Dequeue();
                    ProcessMessage(msg);
                }
            }
        }

        private void ProcessMessage(SignalingMessage msg)
        {
            switch (msg.type)
            {
                case "answer":
                    Debug.Log("[MirageSignaling] Received SDP answer");
                    OnAnswer?.Invoke(msg.sdp);
                    break;

                case "ice-candidate":
                    Debug.Log($"[MirageSignaling] Received ICE candidate: {msg.candidate}");
                    OnIceCandidate?.Invoke(msg.candidate, msg.sdpMid, msg.sdpMLineIndex);
                    break;

                case "prompt_ack":
                    Debug.Log($"[MirageSignaling] Prompt acknowledged: {msg.success}");
                    if (msg.success)
                        OnPromptAck?.Invoke();
                    else
                        OnError?.Invoke($"Prompt failed: {msg.error}");
                    break;

                case "generation_started":
                    Debug.Log("[MirageSignaling] Generation started");
                    OnGenerationStarted?.Invoke();
                    break;

                case "error":
                    Debug.LogError($"[MirageSignaling] Server error: {msg.error}");
                    OnError?.Invoke(msg.error);
                    break;

                default:
                    Debug.LogWarning($"[MirageSignaling] Unknown message type: {msg.type}");
                    break;
            }
        }

        /// <summary>
        /// Send SDP offer to server.
        /// </summary>
        public void SendOffer(string sdp)
        {
            var offerMsg = new OfferMessage
            {
                type = "offer",
                sdp = sdp
            };
            SendJson(JsonUtility.ToJson(offerMsg));
        }

        /// <summary>
        /// Send ICE candidate to server (nested format per Decart protocol).
        /// </summary>
        public void SendIceCandidate(string candidate, string sdpMid, int sdpMLineIndex)
        {
            // Decart protocol uses nested candidate object
            var iceCandidateMsg = new IceCandidateMessage
            {
                type = "ice-candidate",
                candidate = new IceCandidateData
                {
                    candidate = candidate,
                    sdpMid = sdpMid,
                    sdpMLineIndex = sdpMLineIndex
                }
            };
            SendJson(JsonUtility.ToJson(iceCandidateMsg));
        }

        /// <summary>
        /// Send prompt to server (Decart protocol format).
        /// </summary>
        public void SendPrompt(string promptText, bool enhance = true)
        {
            var promptMsg = new PromptMessage
            {
                type = "prompt",
                prompt = promptText,
                enhance_prompt = enhance
            };
            SendJson(JsonUtility.ToJson(promptMsg));
            Debug.Log($"[MirageSignaling] Sent prompt: {promptText}");
        }

        private void Send(SignalingMessage msg)
        {
            SendJson(JsonUtility.ToJson(msg));
        }

        private async void SendJson(string json)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[MirageSignaling] Cannot send, not connected");
                return;
            }

            try
            {
                Debug.Log($"[MirageSignaling] Sending: {json}");
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MirageSignaling] Send error: {ex.Message}");
            }
        }

        public async void Disconnect()
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client disconnecting",
                        CancellationToken.None);
                }
                catch { }
            }

            _cts?.Cancel();
            _isConnected = false;
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
            _webSocket?.Dispose();
            _webSocket = null;
        }

        #region Data Classes

        /// <summary>
        /// Generic incoming message (for parsing server responses).
        /// </summary>
        [Serializable]
        private class SignalingMessage
        {
            // Common
            public string type;

            // SDP (answer)
            public string sdp;

            // ICE candidate (flat format after parsing)
            public string candidate;
            public string sdpMid;
            public int sdpMLineIndex;

            // Response
            public bool success;
            public string error;
        }

        /// <summary>
        /// Outgoing SDP offer message (Decart protocol).
        /// </summary>
        [Serializable]
        private class OfferMessage
        {
            public string type;
            public string sdp;
        }

        /// <summary>
        /// Outgoing ICE candidate message with nested structure (Decart protocol).
        /// </summary>
        [Serializable]
        private class IceCandidateMessage
        {
            public string type;
            public IceCandidateData candidate;
        }

        /// <summary>
        /// Incoming ICE candidate message (Decart protocol).
        /// </summary>
        [Serializable]
        private class IncomingIceCandidateMessage
        {
            public string type;
            public IceCandidateData candidate;
        }

        [Serializable]
        private class IceCandidateData
        {
            public string candidate;
            public string sdpMid;
            public int sdpMLineIndex;
        }

        /// <summary>
        /// Outgoing prompt message (Decart protocol).
        /// </summary>
        [Serializable]
        private class PromptMessage
        {
            public string type;
            public string prompt;
            public bool enhance_prompt;
        }

        #endregion
    }
}
