using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TMPLayeredEffects
{
    /// <summary>
    /// Caches the laid-out banner into a face coverage texture, derives a Euclidean distance field with
    /// Jump Flood, then composites independent shadow / pale / navy / face layers into one UI texture.
    /// TMP remains responsible only for text layout and atlas lookup; none of its outline or underlay
    /// rendering participates in the result.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LayeredTextCompositor : MonoBehaviour
    {
        const string VisualName = "LayeredTextComposite";
        const string MaskShaderName = "Hidden/TMPLayeredEffects/Layered Text Mask";
        const string DistanceShaderName = "Hidden/TMPLayeredEffects/Layered Text Distance";
        const string CompositeShaderName = "Hidden/TMPLayeredEffects/Layered Text Composite";

        [Header("Build dependencies")]
        [SerializeField] Shader maskShader;
        [SerializeField] Shader distanceShader;
        [SerializeField] Shader compositeShader;

        [Header("Cached render")]
        [SerializeField, Range(1f, 2f)] float renderScale = 2f;
        [SerializeField, Min(0f)] float innerRadius = 10f;
        [SerializeField, Min(0f)] float outerRadius = 17f;
        [SerializeField, Min(0f)] float shadowRadius = 21f;
        [SerializeField, Min(0f)] float shadowSoftness = 5f;
        [SerializeField] Vector2 shadowOffset = new Vector2(7f, -10f);

        [Header("Layer colours")]
        [SerializeField] Color innerColor = new Color32(0x03, 0x0D, 0x2D, 0xFF);
        [SerializeField] Color outerTopColor = new Color32(0xF2, 0xF3, 0xF4, 0xFF);
        [SerializeField] Color outerBottomColor = new Color32(0xA8, 0xAE, 0xB5, 0xFF);
        [SerializeField] Color shadowColor = new Color(0f, 0f, 0f, 0.72f);

        TMP_Text _text;
        RawImage _visual;
        Material _maskMaterial;
        Material _distanceMaterial;
        Material _compositeMaterial;
        RenderTexture _output;
        bool _dirty = true;
        bool _warned;
        int _signature;

        /// <summary>The cached final banner texture, exposed for deterministic visual tests.</summary>
        public RenderTexture OutputTexture => _output;

        internal static bool SupportsGraphicsDevice(GraphicsDeviceType graphicsDeviceType) =>
            graphicsDeviceType != GraphicsDeviceType.Null;

        void Awake() => _text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            _dirty = true;
            if (!SupportsGraphicsDevice(SystemInfo.graphicsDeviceType))
            {
                UseLiveTextFallback();
                return;
            }
            EnsureVisual();
        }

        void LateUpdate()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (!SupportsGraphicsDevice(SystemInfo.graphicsDeviceType))
            {
                UseLiveTextFallback();
                return;
            }
            int signature = ComputeSignature();
            if (_dirty || signature != _signature)
                Rebuild(signature);
        }

        void OnRectTransformDimensionsChange() => _dirty = true;

        void OnValidate() => _dirty = true;

        void OnDisable()
        {
            if (_text != null && _text.canvasRenderer != null)
                _text.canvasRenderer.cull = false;
            if (_visual != null) _visual.enabled = false;
        }

        void OnDestroy()
        {
            ReleaseOutput();
            DestroyOwned(_maskMaterial);
            DestroyOwned(_distanceMaterial);
            DestroyOwned(_compositeMaterial);
            if (_visual != null) DestroyOwned(_visual.gameObject);
        }

        public void MarkDirty() => _dirty = true;

        void UseLiveTextFallback()
        {
            if (_text != null && _text.canvasRenderer != null)
                _text.canvasRenderer.cull = false;
            if (_visual != null)
                _visual.enabled = false;
        }

        void EnsureVisual()
        {
            if (_visual != null) return;
            Transform existing = transform.Find(VisualName);
            if (existing != null) _visual = existing.GetComponent<RawImage>();
            if (_visual != null) return;

            var visualObject = new GameObject(VisualName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage));
            visualObject.hideFlags = HideFlags.HideAndDontSave;
            visualObject.layer = gameObject.layer;
            visualObject.transform.SetParent(transform, false);
            _visual = visualObject.GetComponent<RawImage>();
            _visual.raycastTarget = false;
            _visual.color = Color.white;
        }

        bool EnsureMaterials()
        {
            if (_maskMaterial != null && _distanceMaterial != null && _compositeMaterial != null)
                return true;

            // Authored references keep Hidden shaders in player builds. Shader.Find remains a fallback for
            // dynamically-added/legacy components and EditMode tests, not the build-inclusion mechanism.
            Shader mask = maskShader != null ? maskShader : Shader.Find(MaskShaderName);
            Shader distance = distanceShader != null ? distanceShader : Shader.Find(DistanceShaderName);
            Shader composite = compositeShader != null ? compositeShader : Shader.Find(CompositeShaderName);
            if (mask == null || distance == null || composite == null)
            {
                if (!_warned)
                {
                    Debug.LogError("[TMPLayeredEffects] compositor shaders are missing; leaving TMP visible.", this);
                    _warned = true;
                }
                return false;
            }

            _maskMaterial = CreateMaterial(mask, "Layered Text Mask (runtime)");
            _distanceMaterial = CreateMaterial(distance, "Layered Text Distance (runtime)");
            _compositeMaterial = CreateMaterial(composite, "Layered Text Composite (runtime)");
            return true;
        }

        void Rebuild(int signature)
        {
            _dirty = false;
            if (_text == null || !EnsureMaterials()) return;
            EnsureVisual();

            _text.ForceMeshUpdate(true, true);
            if (_text.textInfo == null || _text.textInfo.characterCount == 0)
            {
                _text.canvasRenderer.cull = false;
                _visual.enabled = false;
                _signature = signature;
                return;
            }

            Rect rect = _text.rectTransform.rect;
            float margin = Mathf.Ceil(Mathf.Max(outerRadius,
                shadowRadius + shadowSoftness + shadowOffset.magnitude) + 3f);
            float scale = Mathf.Clamp(renderScale, 1f, 2f);
            int width = Mathf.Max(8, Mathf.CeilToInt((rect.width + margin * 2f) * scale));
            int height = Mathf.Max(8, Mathf.CeilToInt((rect.height + margin * 2f) * scale));
            int maxTextureSize = SystemInfo.maxTextureSize;
            if (width > maxTextureSize || height > maxTextureSize)
            {
                float fit = Mathf.Min(maxTextureSize / (float)width, maxTextureSize / (float)height);
                scale *= fit;
                width = Mathf.Max(8, Mathf.CeilToInt((rect.width + margin * 2f) * scale));
                height = Mathf.Max(8, Mathf.CeilToInt((rect.height + margin * 2f) * scale));
            }

            EnsureOutput(width, height);
            ConfigureVisualRect(margin);

            RenderTexture face = null;
            RenderTexture seedA = null;
            RenderTexture seedB = null;
            try
            {
                face = GetTemporary(width, height, RenderTextureFormat.ARGB32, FilterMode.Bilinear,
                    "TMP Layered Effects Face");
                RenderFace(face, rect, margin);

                RenderTextureFormat seedFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat)
                    ? RenderTextureFormat.RGFloat
                    : RenderTextureFormat.ARGBFloat;
                seedA = GetTemporary(width, height, seedFormat, FilterMode.Point,
                    "TMP Layered Effects Seeds A");
                seedB = GetTemporary(width, height, seedFormat, FilterMode.Point,
                    "TMP Layered Effects Seeds B");

                Graphics.Blit(face, seedA, _distanceMaterial, 0);
                RenderTexture read = seedA;
                RenderTexture write = seedB;
                for (int jump = HighestPowerOfTwo(width, height); jump >= 1; jump >>= 1)
                {
                    _distanceMaterial.SetVector("_StepUV",
                        new Vector4(jump / (float)width, jump / (float)height, 0f, 0f));
                    Graphics.Blit(read, write, _distanceMaterial, 1);
                    RenderTexture swap = read;
                    read = write;
                    write = swap;
                }

                _compositeMaterial.SetTexture("_SeedTex", read);
                _compositeMaterial.SetVector("_TextureSize", new Vector4(width, height, 0f, 0f));
                _compositeMaterial.SetFloat("_InnerRadius", innerRadius * scale);
                _compositeMaterial.SetFloat("_OuterRadius", outerRadius * scale);
                _compositeMaterial.SetFloat("_ShadowRadius", shadowRadius * scale);
                _compositeMaterial.SetFloat("_ShadowSoftness", shadowSoftness * scale);
                _compositeMaterial.SetVector("_ShadowOffset", shadowOffset * scale);
                _compositeMaterial.SetColor("_InnerColor", innerColor);
                _compositeMaterial.SetColor("_OuterTopColor", outerTopColor);
                _compositeMaterial.SetColor("_OuterBottomColor", outerBottomColor);
                _compositeMaterial.SetColor("_ShadowColor", shadowColor);
                Graphics.Blit(face, _output, _compositeMaterial, 0);

                _visual.texture = _output;
                _visual.enabled = true;
                _text.canvasRenderer.cull = true;
                _signature = signature;
                _warned = false;
            }
            catch (System.Exception exception)
            {
                _text.canvasRenderer.cull = false;
                _visual.enabled = false;
                if (!_warned)
                {
                    Debug.LogError($"[TMPLayeredEffects] compositor rebuild failed: {exception.Message}", this);
                    _warned = true;
                }
            }
            finally
            {
                RenderTexture.active = null;
                if (face != null) RenderTexture.ReleaseTemporary(face);
                if (seedA != null) RenderTexture.ReleaseTemporary(seedA);
                if (seedB != null) RenderTexture.ReleaseTemporary(seedB);
            }
        }

        void RenderFace(RenderTexture target, Rect rect, float margin)
        {
            var command = new CommandBuffer { name = "TMP Layered Effects Face Mask" };
            try
            {
                command.SetRenderTarget(target);
                command.ClearRenderTarget(false, true, Color.clear);
                Matrix4x4 projection = Matrix4x4.Ortho(rect.xMin - margin, rect.xMax + margin,
                    rect.yMin - margin, rect.yMax + margin, -1f, 1f);
                command.SetViewProjectionMatrices(Matrix4x4.identity, projection);

                int materialCount = _text.textInfo.materialCount;
                for (int index = 0; index < materialCount; index++)
                {
                    TMP_MeshInfo meshInfo = _text.textInfo.meshInfo[index];
                    Mesh mesh = meshInfo.mesh;
                    if (mesh == null || mesh.vertexCount == 0) continue;
                    Texture atlas = meshInfo.material != null ? meshInfo.material.mainTexture : null;
                    if (atlas == null && _text.font != null) atlas = _text.font.atlasTexture;
                    if (atlas == null) continue;
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_MainTex", atlas);
                    command.DrawMesh(mesh, Matrix4x4.identity, _maskMaterial, 0, 0, properties);
                }
                Graphics.ExecuteCommandBuffer(command);
            }
            finally
            {
                command.Release();
            }
        }

        void ConfigureVisualRect(float margin)
        {
            var rect = (RectTransform)_visual.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-margin, -margin);
            rect.offsetMax = new Vector2(margin, margin);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
        }

        void EnsureOutput(int width, int height)
        {
            if (_output != null && _output.width == width && _output.height == height) return;
            ReleaseOutput();
            _output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "Layered Text Composite Cache",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            _output.Create();
        }

        void ReleaseOutput()
        {
            if (_visual != null && _visual.texture == _output) _visual.texture = null;
            if (_output == null) return;
            _output.Release();
            DestroyOwned(_output);
            _output = null;
        }

        int ComputeSignature()
        {
            if (_text == null) return 0;
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (_text.text?.GetHashCode() ?? 0);
                hash = hash * 31 + (_text.font != null ? _text.font.GetInstanceID() : 0);
                hash = hash * 31 + (_text.fontSharedMaterial != null
                    ? _text.fontSharedMaterial.GetInstanceID() : 0);
                hash = hash * 31 + Mathf.RoundToInt(_text.rectTransform.rect.width * 100f);
                hash = hash * 31 + Mathf.RoundToInt(_text.rectTransform.rect.height * 100f);
                hash = hash * 31 + Mathf.RoundToInt(_text.fontSize * 100f);
                VertexGradient gradient = _text.colorGradient;
                hash = hash * 31 + gradient.topLeft.GetHashCode();
                hash = hash * 31 + gradient.topRight.GetHashCode();
                hash = hash * 31 + gradient.bottomLeft.GetHashCode();
                hash = hash * 31 + gradient.bottomRight.GetHashCode();
                hash = hash * 31 + _text.color.GetHashCode();
                return hash;
            }
        }

        static int HighestPowerOfTwo(int width, int height)
        {
            int value = Mathf.Max(width, height);
            int power = 1;
            while (power < value && power <= 8192) power <<= 1;
            return Mathf.Max(1, power >> 1);
        }

        static RenderTexture GetTemporary(int width, int height, RenderTextureFormat format,
            FilterMode filter, string name)
        {
            RenderTexture texture = RenderTexture.GetTemporary(width, height, 0, format,
                RenderTextureReadWrite.Linear);
            texture.name = name;
            texture.filterMode = filter;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        static Material CreateMaterial(Shader shader, string name) => new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
        };

        static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
