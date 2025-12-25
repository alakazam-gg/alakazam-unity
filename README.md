# Alakazam Unity

Unity SDK for real-time AI video stylization via Alakazam Server.

## Components

### AlakazamController
Simple WebSocket client that connects to Alakazam Server:
- No WebRTC required (server handles AI connection)
- Captures rendered frames from your camera
- Returns stylized frames in real-time
- Simple API: `StartAlakazam()`, `Stop()`, `SetPrompt()`

**Location:** `Assets/AlakazamPortal/Runtime/AlakazamController.cs`

### AlakazamBootstrap (Demo Setup)
Drop this on any GameObject to create a complete demo scene:
- Creates camera, lights, mannequin, decorative objects
- Sets up AlakazamController with your config
- Creates ShowcaseUI

**Location:** `Assets/AlakazamPortal/Demo/AlakazamBootstrap.cs`

### ShowcaseUI
Demo UI with:
- Style preset buttons (numbered 1-8)
- Editable prompt field
- Start/Stop button
- Split-screen comparison (B key)
- Screenshot capture (R key)

**Location:** `Assets/AlakazamPortal/Demo/ShowcaseUI.cs`

## Quick Start

### Option 1: Use AlakazamBootstrap (Recommended)
1. Create an empty scene
2. Create empty GameObject, add `AlakazamBootstrap` component
3. Set your **Server URL** in Inspector
4. Hit Play
5. Press ENTER to start streaming

### Option 2: Manual Setup
1. Add `AlakazamController` to any GameObject
2. Configure in Inspector:
   - **Server URL:** `ws://localhost:9001` (or your server)
   - **Source Camera:** Camera to capture
   - **Prompt:** Style description
3. Add `ShowcaseUI` for UI (optional)
4. Call `StartAlakazam()` to begin

## Testing with Alakazam Server

1. Start the server:
   ```bash
   cd alakazam-server
   PYTHONPATH=python ./venv/bin/python python/server.py
   ```

2. In Unity, hit Play and press ENTER (or call `StartAlakazam()`)

3. Stylized output will appear on the fullscreen display

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Space | Start/Stop streaming |
| 1-8 | Select style preset |
| Tab | Toggle UI visibility |
| B | Split-screen comparison |
| R | Capture screenshot |

## Requirements

- Unity 6000.0+
- Alakazam Server running locally or remotely
