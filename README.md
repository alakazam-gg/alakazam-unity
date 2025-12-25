# Alakazam Unity Test Project

Unity test project for testing the [alakazam-server](../alakazam-server) proxy with a real game engine.

## Components

### AlakazamClient (New - For Proxy Server)
Simple WebSocket client that connects to the alakazam-server proxy:
- No WebRTC required (server handles WebRTC to Decart)
- Sends JPEG frames over WebSocket
- Receives styled JPEG frames back
- Uses built-in `System.Net.WebSockets.ClientWebSocket`

**Location:** `Assets/NeuralAkazam/Runtime/AlakazamClient.cs`

### MirageController (Original - Direct to Decart)
Direct WebRTC connection to Decart's MirageLSD service:
- Requires Unity WebRTC package
- Handles SDP offer/answer and ICE candidates
- More complex but lower latency for local setups

**Location:** `Assets/NeuralAkazam/Runtime/MirageController.cs`

## Setup

1. Open project in Unity 6+
2. Add `AlakazamClient` component to any GameObject
3. Configure:
   - **Server URL:** `ws://localhost:9001` (or your server address)
   - **API Key:** Your Decart API key
   - **Prompt:** Style description (e.g., "minecraft village")
   - **Source Camera:** Camera to capture frames from

## Testing with alakazam-server

1. Start the proxy server:
   ```bash
   cd ../alakazam-server
   ./build/alakazam_server
   ```

2. In Unity, hit Play and call `Connect()` on the AlakazamClient

3. Styled frames will appear on the output display

## Requirements

- Unity 6000.0+
- alakazam-server running locally or remotely
- Valid Decart API key
