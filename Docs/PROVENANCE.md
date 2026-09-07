# Provenance

The compositor and three shaders originate from Rio's `02b5f58ab` (2026-08-18), “feat(ui): add layered text composite and distance for game start”. The extraction includes toofan's `04ac8048b07fde2b3857706baecab5faacc80ed3` (2026-08-26) graphics-device fallback.

Only `LayeredTextCompositor.cs`, `LayeredTextMask.shader`, `LayeredTextDistance.shader` and `LayeredTextComposite.shader` were extracted. Cliff's shadow components, game UI controllers, game fonts, prefabs and scenes are not included.

Standalone changes:

- Namespace: `PeeKaBlock.UI.HUD` → `TMPLayeredEffects`.
- Hidden shader prefix: `Hidden/Mottle/` → `Hidden/TMPLayeredEffects/`.
- Diagnostic and GPU resource labels use the standalone project name.
- New Unity project configuration and editor-authored demo use standard TMP resources.

The rendering algorithm and serialized settings remain otherwise unchanged. This extraction does not implement the proposed performance or compatibility improvements.
