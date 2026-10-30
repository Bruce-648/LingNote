# Current application icon

The approved icon is a compact black ghost silhouette with white oval eyes and a single white audio capsule. It has a transparent background and no notebook or small outline details.

- Generation source: `src/LingNote/Assets/app-ghost-original.png`.
- In-app display asset: `src/LingNote/Assets/app-logo.png`.
- Windows application icon: `src/LingNote/Assets/app.ico`.
- Packaging: `tools/package-icon.py` crops the visible mark, preserves its aspect ratio, and creates ICO sizes 16, 20, 24, 32, 40, 48, 64, 128 and 256.
- Wordmark source: `assets/wordmark-original.png`; `tools/package-wordmark.py` preserves the full cropped source resolution.

The icon and wordmark were generated with the built-in image generation tool. Native UI controls use `UiIcons.cs` vector drawings.
