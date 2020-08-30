using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// ReSharper disable once CheckNamespace
namespace LevelCaptureTool {

    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
    public class LevelCaptureTool : MonoBehaviour {

        private readonly Color _selectionBgColor = new Color(0.25f, 0f, 0f, 0.25f);

        public Vector2 Size {
            get => size;
            set {
                size = value;
                _modified = true;
            }
        }

        public float Margin {
            get => margin;
            set {
                margin = value;
                _modified = true;
            }
        }

        [SerializeField]
        private RenderTexture renderTexture = default;

        [SerializeField]
        private int pixelsPerUnit;

        [SerializeField]
        private LayerMask layerMask = default;

        [SerializeField, UsePropertyInInspector(nameof(Size))]
        private Vector2 size;

        [SerializeField, UsePropertyInInspector(nameof(Margin))]
        private float margin;

        [SerializeField]
        private float zOffset = -10f;

        public bool drawGizmos = true;

        [SerializeField]
        private bool saveLayersSeparately = default;

        private Camera _camera;
        private Bounds _bounds;
        private bool _modified;

        public void MarkDirty() => _modified = true;

        private void OnDisable() => ClearTexture();
        private void Awake() => _camera = GetComponent<Camera>();
        private void Start() => _bounds = new Bounds(transform.position, size);

        private void ClearTexture() {
            if (renderTexture != null) {
                RenderTexture.active = null;
                renderTexture.Release();
            }
        }

        private void Update() {
            _bounds.center = transform.position;
            if (!_modified) {
                return;
            }
            _bounds.size = size;
            UpdateCamera();
            EditorUtility.SetDirty(gameObject);
        }

        public void TrimBounds() {
            var colliders = Physics2D.OverlapAreaAll(_bounds.min, _bounds.max, layerMask);
            if (colliders == null || colliders.Length == 0) {
                Debug.Log("No matching colliders found in the selected area");
                return;
            }

            var rect = FindColliderRect(colliders);
            if (!rect.HasValue) {
                return;
            }
            var rectCenter = rect.Value.center;

            var rectSize = rect.Value.size;
            if (_bounds.size.x < rectSize.x || _bounds.size.y < rectSize.y) {
                return;
            }
            transform.position = new Vector3(rectCenter.x, rectCenter.y, zOffset);
            _bounds.center = rectCenter;
            _bounds.size = rectSize;
            size = _bounds.size;

            _modified = false;
            EditorUtility.SetDirty(gameObject);
            UpdateCamera();
        }

        private static Rect? FindColliderRect(Collider2D[] colliders) {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;

            var maxY = float.MinValue;

            // ReSharper disable once LocalVariableHidesMember
            foreach (var collider in colliders) {
                minX = Mathf.Min(minX, collider.bounds.min.x);
                maxX = Mathf.Max(maxX, collider.bounds.max.x);
                minY = Mathf.Min(minY, collider.bounds.min.y);
                maxY = Mathf.Max(maxY, collider.bounds.max.y);
            }
            if (minX >= float.MaxValue || maxX <= float.MinValue || minY >= float.MaxValue || maxY <= float.MinValue) {
                return null;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        public void UpdateCamera() {
            ClearTexture();
            UpdateZPosition();

            var maxSize = Mathf.Max(_bounds.size.x + margin, _bounds.size.y + margin) * pixelsPerUnit;
            var textureSize = CeilPower2((int) maxSize);
            renderTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.Default, 0);

            _camera = GetComponent<Camera>();
            _camera.aspect = 1f;
            _camera.orthographicSize = textureSize / (float) pixelsPerUnit / 2f;
            _camera.targetTexture = renderTexture;
        }

        public void Capture() {
            var path = EditorUtility.SaveFilePanelInProject("Save captured level part", "", "png", "");
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            var layerNames = new List<string>();
            for (var i = 0; i < 32; i++) {
                if (layerMask != (layerMask | (1 << i))) {
                    continue;
                }
                var layerName = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(layerName)) {
                    continue;
                }
                layerNames.Add(LayerMask.LayerToName(i));
            }
            if (saveLayersSeparately) {
                layerNames.ForEach(layerName => SaveLayerMask(path, true, layerName));
            } else {
                SaveLayerMask(path, false, layerNames.ToArray());
            }
        }

        private void SaveLayerMask(string path, bool appendLayerNames, params string[] layerNames) {
            Debug.Log($"Capturing layers: {string.Join(", ", layerNames)}");
            UpdateCamera();

            var cameraMask = _camera.cullingMask;
            _camera.cullingMask = LayerMask.GetMask(layerNames);
            _camera.Render();
            _camera.cullingMask = cameraMask;

            var active = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var texture = new Texture2D(renderTexture.width, renderTexture.height, renderTexture.graphicsFormat, 0, TextureCreationFlags.None);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);

            IncludeOnlyBounds(texture, _bounds);

            texture.Apply();
            RenderTexture.active = active;

            if (appendLayerNames) {
                var extensionIndex = path.LastIndexOf(".", StringComparison.Ordinal);
                var extension = path.Substring(extensionIndex + 1);
                var pathWithoutExtension = path.Substring(0, extensionIndex);
                var newPath = $"{pathWithoutExtension}_{string.Join("_", layerNames)}.{extension}";

                File.WriteAllBytes(newPath, texture.EncodeToPNG());
            } else {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            ClearTexture();
        }

        private void IncludeOnlyBounds(Texture2D texture, Bounds bounds) {
            var texturePixelRect = new RectInt(
                0,
                0,
                renderTexture.width,
                renderTexture.height
            );
            var pixelBounds = new RectInt(
                (int) (texturePixelRect.width / 2f - Mathf.Abs((bounds.size.x + margin) * pixelsPerUnit / 2f)),
                (int) (texturePixelRect.height / 2f - Mathf.Abs((bounds.size.y + margin) * pixelsPerUnit / 2f)),
                (int) ((bounds.size.x + margin) * pixelsPerUnit),
                (int) ((bounds.size.y + margin) * pixelsPerUnit)
            );
            var borderRects = new[] {
                new RectInt(0, 0, pixelBounds.xMin, texturePixelRect.height),
                new RectInt(pixelBounds.xMin, pixelBounds.yMax, pixelBounds.width, texturePixelRect.height - pixelBounds.yMax),
                new RectInt(pixelBounds.xMax, 0, texture.width - pixelBounds.xMax, texturePixelRect.height),
                new RectInt(pixelBounds.xMin, 0, pixelBounds.width, pixelBounds.yMin)
            };
            foreach (var borderRect in borderRects) {
                FillRect(texture, borderRect, Color.clear);
            }
        }

        private void FillRect(Texture2D texture, RectInt texturePixelRect, Color color) {
            var colors = new Color[texturePixelRect.width * texturePixelRect.height];
            for (var i = 0; i < colors.Length; i++) {
                colors[i] = color;
            }
            texture.SetPixels(texturePixelRect.xMin, texturePixelRect.yMin, texturePixelRect.width, texturePixelRect.height, colors);
        }

        /// <remarks>Taken from https://stackoverflow.com/a/54065600</remarks>
        private static int CeilPower2(int x) {
            if (x < 2) {
                return 1;
            }

            return (int) Math.Pow(2, (int) Math.Log(x - 1, 2) + 1);
        }

        private void OnDrawGizmosSelected() {
            if (!drawGizmos || _bounds.size == Vector3.zero) {
                return;
            }
            Gizmos.color = _selectionBgColor;
            Gizmos.DrawCube(_bounds.center, _bounds.size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_bounds.center, _bounds.size);

            if (margin > 0f) {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_bounds.center, new Vector3(_bounds.size.x + margin, _bounds.size.y + margin));
            }
        }

        private void OnValidate() {
            if (_camera == null) {
                _camera = GetComponent<Camera>();
            }
            _camera.orthographic = true;
            _camera.aspect = 1f;
            size.x = Mathf.Max(size.x, 0f);
            size.y = Mathf.Max(size.y, 0f);
            pixelsPerUnit = Mathf.Max(pixelsPerUnit, 1);
            margin = Mathf.Max(margin, 0f);
            UpdateZPosition();
        }

        private void UpdateZPosition() {
            transform.position = new Vector3(
                transform.position.x,
                transform.position.y,
                zOffset
            );
        }

    }
}
