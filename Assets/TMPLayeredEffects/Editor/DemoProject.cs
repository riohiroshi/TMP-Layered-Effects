using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;

namespace TMPLayeredEffects.Editor
{
    public static class DemoProject
    {
        const string ScenePath = "Assets/Demo/LayeredEffects.unity";

        [MenuItem("Tools/TMP Layered Effects/Create Demo Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Rebuild demo?", "This replaces the demo scene. Save other work first.", "Rebuild", "Cancel")) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var font = DemoFont("jigmo/Jigmo.ttf", "快樂出發!ゲーム開始!VICTORY!");
            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            settings.FindProperty("m_fallbackFontAssets").ClearArray();
            settings.ApplyModifiedPropertiesWithoutUndo();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Demo Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(18, 22, 36, 255);
            camera.orthographic = true;
            var canvas = new GameObject("Demo Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 900);
            scaler.matchWidthOrHeight = 0.5f;
            Label(canvas.transform, font, "TMP LAYERED EFFECTS", 48, new Vector2(0, 366), new Vector2(1180, 70), Color.white);
            Label(canvas.transform, font, "Two outlines. Gradient colour. Soft shadow. One cached image.", 21, new Vector2(0, 308), new Vector2(1180, 45), new Color32(160, 174, 199, 255));
            Label(canvas.transform, font, "ORIGINAL TMP", 18, new Vector2(-310, 233), new Vector2(560, 35), new Color32(160, 174, 199, 255));
            Label(canvas.transform, font, "LAYERED COMPOSITOR", 18, new Vector2(310, 233), new Vector2(560, 35), new Color32(160, 174, 199, 255));
            string[] titles = { "快樂出發!", "ゲーム開始!", "VICTORY!" };
            string[] fontLabels = { "TRADITIONAL CHINESE / Jigmo - CC0", "JAPANESE / Jigmo - CC0", "ENGLISH / Jigmo - CC0" };
            TMP_FontAsset[] demoFonts = { font, font, font };
            Color[] faces = { new Color32(255, 225, 76, 255), new Color32(117, 247, 230, 255), new Color32(255, 177, 222, 255) };
            for (int i = 0; i < titles.Length; i++)
            {
                float y = 128 - 185 * i;
                Label(canvas.transform, font, fontLabels[i], 16, new Vector2(0, y + 68), new Vector2(1180, 28), new Color32(160, 174, 199, 255));
                Label(canvas.transform, demoFonts[i], titles[i], 57, new Vector2(-310, y), new Vector2(520, 100), faces[i]);
                var text = Label(canvas.transform, demoFonts[i], titles[i], 57, new Vector2(310, y), new Vector2(520, 100), faces[i]);
                text.gameObject.name = "Effect " + titles[i];
                var effect = text.gameObject.AddComponent<LayeredTextCompositor>();
                var so = new SerializedObject(effect);
                string[] fields = { "maskShader", "distanceShader", "compositeShader" };
                string[] names = { "Mask", "Distance", "Composite" };
                for (int s = 0; s < fields.Length; s++)
                    so.FindProperty(fields[s]).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(
                        "Assets/TMPLayeredEffects/Shaders/LayeredText" + names[s] + ".shader");
                so.FindProperty("innerRadius").floatValue = i == 1 ? 4 : 7;
                so.FindProperty("outerRadius").floatValue = i == 1 ? 8 : 13;
                so.FindProperty("shadowRadius").floatValue = 16;
                if (i == 1) so.FindProperty("outerTopColor").colorValue = new Color32(35, 153, 168, 255);
                if (i == 2) so.FindProperty("outerBottomColor").colorValue = new Color32(161, 86, 166, 255);
                so.ApplyModifiedPropertiesWithoutUndo();
                effect.MarkDirty();
            }
            Label(canvas.transform, font, "Select an Effect object to edit text, outline colours, radii and shadow.\nPreview works in Edit Mode and Play Mode. Best viewed at 1280 x 900.", 19, new Vector2(0, -366), new Vector2(1180, 65), new Color32(160, 174, 199, 255));
            PlayerSettings.companyName = "TMPLayeredEffects";
            PlayerSettings.productName = "TMP Layered Effects Demo";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 900;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            VerifyAndCapture(camera, canvas);
            Debug.Log("TMP_LAYERED_DEMO_PASS: scene saved, shaders checked, GPU pixels verified.");
        }

        static TMP_FontAsset DemoFont(string relativePath, string sample)
        {
            string sourcePath = "Assets/Demo/Fonts/" + relativePath;
            string assetPath = Path.ChangeExtension(sourcePath, ".asset");
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset != null)
            {
                if (!asset.HasCharacters(sample, out uint[] missing, false, true))
                    throw new InvalidOperationException("Missing demo glyphs in " + sourcePath);
                return asset;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing source font: " + sourcePath);
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 12, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("Cannot create SDF font: " + sourcePath);
            asset.name = Path.GetFileNameWithoutExtension(sourcePath) + " SDF";
            asset.material.name = asset.name + " Material";
            // Bake the displayed glyphs so the scene works immediately, retain the source for new text.
            string characters = sample + new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray());
            if (!asset.TryAddCharacters(characters, out string missingCharacters))
                throw new InvalidOperationException("Missing font glyphs: " + missingCharacters + " in " + sourcePath);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string value, int size, Vector2 position, Vector2 dimensions, Color colour)
        {
            var text = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = dimensions;
            text.rectTransform.anchoredPosition = position;
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.raycastTarget = false;
            return text;
        }

        static void VerifyAndCapture(Camera camera, Canvas canvas)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Visual verification requires a GPU. Do not use -nographics.");
            Directory.CreateDirectory("Artifacts");
            var previous = RenderTexture.active;
            var target = new RenderTexture(1280, 900, 24);
            target.Create();
            camera.targetTexture = target;
            Texture2D capture = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>())
                {
                    string characters = text.text.Replace("\n", "").Replace("\r", "");
                    if (!text.font.HasCharacters(characters, out uint[] absent, false, false))
                        throw new InvalidOperationException("Missing glyphs in " + text.name);
                    if (!AssetDatabase.GetAssetPath(text.font).StartsWith("Assets/Demo/Fonts/jigmo/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Non-CC0 font in demo: " + text.name);
                }
                foreach (var effect in canvas.GetComponentsInChildren<LayeredTextCompositor>())
                {
                    var label = effect.GetComponent<TMP_Text>();
                    if (!label.font.HasCharacters(label.text, out uint[] missing, false, false))
                        throw new InvalidOperationException("Missing characters in " + label.name);
                    effect.SendMessage("LateUpdate");
                    var output = effect.OutputTexture;
                    if (output == null) throw new InvalidOperationException("Missing composite texture.");
                    RenderTexture.active = output;
                    var pixels = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false, true);
                    try
                    {
                        pixels.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
                        pixels.Apply();
                        var values = pixels.GetPixels32();
                        if (values.Count(p => p.a > 220) < 100 || values.Count(p => p.a > 15 && p.a < 220 && p.r < 40 && p.g < 40 && p.b < 40) < 100)
                            throw new InvalidOperationException("Composite lacks opaque text or soft black shadow.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(pixels); }
                }
                foreach (string name in new[] { "Mask", "Distance", "Composite" })
                {
                    var shader = Shader.Find("Hidden/TMPLayeredEffects/Layered Text " + name);
                    if (shader == null || ShaderUtil.GetShaderMessages(shader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                        throw new InvalidOperationException("Shader failure: " + name);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                capture.Apply();
                File.WriteAllBytes("Artifacts/demo.png", capture.EncodeToPNG());
                File.WriteAllText("Artifacts/verification.txt", "PASS: Chinese, Japanese and English glyphs present in their assigned fonts; 3 composites contain opaque text and soft shadow; shaders have no compiler errors.\nUnity " + Application.unityVersion + " / " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName + "\n");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
