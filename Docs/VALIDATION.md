# Validation — CC0 font edition, 2026-09-07

Environment: Unity 6000.3.13f1, macOS, Metal, Apple M5 Max; Built-in Render Pipeline.

## Passed on the current demo

- Unity batch import, C# compilation and scene generation (exit 0).
- All scene labels use the Jigmo SDF asset, including interface labels and three language examples.
- Every displayed character exists in the assigned font without fallback search.
- All three compositor outputs contain opaque text and partially transparent black shadow pixels; custom shaders have no compiler errors.
- Full 1280 x 900 camera render visually inspected. [Current preview](demo.png).
- Only one font binary exists under Assets: `Assets/Demo/Fonts/jigmo/Jigmo.ttf`.
- No removed font GUIDs remain in serialized scene, asset, material or prefab files.
- All three shaders remain explicitly referenced on each effect. Temporary compositor child objects are not serialized.
- Extracted font and source-document hashes match the recorded download. The downloaded archive's Git blob matches the author's official repository. The website's truncated/stale checksum is not claimed as passing.
- Core compositor and shaders were not changed by the font replacement; original source-equivalence evidence remains applicable.

## Reproduce

With this project closed in other Unity instances, run from the repository root:

```sh
mkdir -p Artifacts
"/Applications/Unity/Hub/Editor/6000.3.13f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD" \
  -executeMethod TMPLayeredEffects.Editor.DemoProject.Create \
  -quit -logFile "$PWD/Artifacts/cc0-demo.log"
```

This regenerates the scene and writes a camera screenshot and verification report under ignored `Artifacts/`. Do not use `-nographics`.

## Limits and corrected failure

Jigmo does not contain U+FF01 fullwidth exclamation. The first CC0 generation attempt correctly failed its glyph check; sample punctuation was changed to ASCII `!` and all checks passed on the next run. Missing glyph checks were not skipped or weakened.

Interactive Play Mode, Player builds, other graphics APIs/platforms, performance and comprehensive TMP compatibility remain unverified. Source/license checks establish the publisher's CC0 declaration and file provenance, not a guarantee against all legal claims.
