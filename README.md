# Alakazam Portal - Unity SDK

Development repository for the Alakazam Portal Unity package.

## Repository Structure

```
alakazam-unity/
├── Assets/
│   ├── AlakazamPortal/          ← The distributed package
│   │   ├── Runtime/             ← Core SDK
│   │   ├── Demo/                ← UI & helpers
│   │   └── Editor/              ← Editor tools
│   └── ...                      ← Dev assets (not distributed)
└── ProjectSettings/
```

## Package Installation

Clients install via Unity Package Manager:

```
https://github.com/alakazam/portal-unity.git?path=Assets/AlakazamPortal
```

See [Package README](Assets/AlakazamPortal/README.md) for usage instructions.

## Development

### Requirements
- Unity 6000.0+
- Alakazam server running locally for testing

### Testing Locally
1. Open the project in Unity
2. Open any scene with low-poly assets
3. **AlakazamPortal → Add to Current Scene**
4. Configure server URL in AlakazamController
5. Play and press Space to start

### Recommended Test Assets
- SimplePoly City - Low Poly Assets
- LowPoly Environment Pack
- Elementary Dungeon Pack Lite

## Publishing

Package is distributed via GitHub Packages. See internal docs for release process.
