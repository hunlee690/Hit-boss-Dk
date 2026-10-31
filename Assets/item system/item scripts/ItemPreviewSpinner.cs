using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Items
{
    public sealed class ItemPreviewSpinner : MonoBehaviour
    {
        const int PreviewLayer = 31;
        static int nextStage;
        static GameObject keyLight;
        static GameObject fillLight;

        public float degreesPerSecond = 45f;
        public bool clockwise = true;

        readonly List<GameObject> previewObjects = new List<GameObject>();
        readonly List<Mesh> bakedMeshes = new List<Mesh>();
        Image image;
        RawImage liveImage;
        Camera previewCamera;
        GameObject pivot;
        RenderTexture target;
        Sprite[] fallbackFrames;
        Color baseColor = Color.white;
        PlayerCustomizationManager.ItemPreviewSettings liveSettings;
        PlayerCustomizationManager.Category liveCategory;
        Vector3 liveStageCenter;
        float liveRadius;

        public bool ConfigureLive(
            GameObject[] sources,
            PlayerCustomizationManager.ItemPreviewSettings settings,
            PlayerCustomizationManager.Category category)
        {
            image = GetComponent<Image>();
            if (image == null || sources == null || sources.Length == 0 || settings == null)
                return false;

            CleanupLivePreview();
            degreesPerSecond = Mathf.Max(0f, settings.spinDegreesPerSecond);
            clockwise = settings.clockwise;
            baseColor = image.color;
            liveSettings = settings;
            liveCategory = category;

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (GameObject source in sources)
            {
                if (source == null) continue;
                foreach (Renderer sourceRenderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = null;
                    if (sourceRenderer is SkinnedMeshRenderer skinned)
                    {
                        mesh = new Mesh { name = source.name + " Live Preview Mesh" };
                        skinned.BakeMesh(mesh);
                        bakedMeshes.Add(mesh);
                    }
                    else
                    {
                        MeshFilter filter = sourceRenderer.GetComponent<MeshFilter>();
                        if (filter != null) mesh = filter.sharedMesh;
                    }

                    if (mesh == null) continue;
                    var preview = new GameObject("Live Item Preview", typeof(MeshFilter), typeof(MeshRenderer));
                    preview.hideFlags = HideFlags.HideAndDontSave;
                    preview.layer = PreviewLayer;
                    preview.transform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
                    preview.transform.localScale = sourceRenderer.transform.lossyScale;
                    preview.GetComponent<MeshFilter>().sharedMesh = mesh;
                    preview.GetComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                    previewObjects.Add(preview);

                    Bounds rendererBounds = preview.GetComponent<Renderer>().bounds;
                    if (!hasBounds) { bounds = rendererBounds; hasBounds = true; }
                    else bounds.Encapsulate(rendererBounds);
                }
            }

            if (!hasBounds)
            {
                CleanupLivePreview();
                return false;
            }

            Vector3 stageCenter = new Vector3(10000f + nextStage++ * 100f, -10000f, 10000f);
            liveStageCenter = stageCenter;
            pivot = new GameObject("Live Item Preview Pivot");
            pivot.hideFlags = HideFlags.HideAndDontSave;
            pivot.transform.position = stageCenter;

            Vector3 offset = stageCenter - bounds.center;
            foreach (GameObject preview in previewObjects)
            {
                preview.transform.position += offset;
                preview.transform.SetParent(pivot.transform, true);
            }

            bounds = previewObjects[0].GetComponent<Renderer>().bounds;
            for (int i = 1; i < previewObjects.Count; i++)
                bounds.Encapsulate(previewObjects[i].GetComponent<Renderer>().bounds);

            GameObject cameraObject = new GameObject("Live Item Preview Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = settings.backgroundColor;
            previewCamera.orthographic = false;
            previewCamera.allowHDR = false;
            previewCamera.fieldOfView = Mathf.Clamp(settings.fieldOfView, 15f, 60f);
            previewCamera.nearClipPlane = .01f;

            liveRadius = Mathf.Max(.15f, bounds.extents.magnitude);
            EnsureLights();
            EnsureLiveImage();
            EnsureRenderTarget();
            ApplyCameraSettings();
            liveImage.texture = target;
            liveImage.color = Color.white;
            liveImage.gameObject.SetActive(true);
            image.enabled = false;
            fallbackFrames = null;
            enabled = true;
            RenderCurrentFrame();
            return true;
        }

        public void Configure(Sprite[] animationFrames, float speed, bool clockwiseSpin)
        {
            CleanupLivePreview();
            image = GetComponent<Image>();
            fallbackFrames = animationFrames;
            degreesPerSecond = Mathf.Max(0f, speed);
            clockwise = clockwiseSpin;
            if (image != null)
            {
                image.enabled = true;
                baseColor = image.color;
            }
            if (liveImage != null) liveImage.gameObject.SetActive(false);
            enabled = image != null;
        }

        void EnsureLiveImage()
        {
            if (liveImage == null)
            {
                Transform existing = transform.Find("Live Preview");
                if (existing != null) liveImage = existing.GetComponent<RawImage>();
            }
            if (liveImage != null) return;

            var value = new GameObject("Live Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            value.layer = gameObject.layer;
            value.transform.SetParent(transform, false);
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            liveImage = value.GetComponent<RawImage>();
            liveImage.raycastTarget = false;
        }

        static void EnsureLights()
        {
            if (keyLight == null)
                keyLight = CreateLight("Live Item Preview Key", new Vector3(35f, -35f, 0f), 1.15f);
            if (fillLight == null)
                fillLight = CreateLight("Live Item Preview Fill", new Vector3(320f, 140f, 0f), .65f);
        }

        static GameObject CreateLight(string name, Vector3 euler, float intensity)
        {
            var value = new GameObject(name, typeof(Light));
            value.hideFlags = HideFlags.HideAndDontSave;
            value.transform.rotation = Quaternion.Euler(euler);
            Light light = value.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << PreviewLayer;
            // The light exists only for manual preview-camera renders. Keeping
            // it disabled prevents it from lighting the menu player.
            value.SetActive(false);
            return value;
        }

        void LateUpdate()
        {
            if (previewCamera != null && pivot != null)
            {
                degreesPerSecond = Mathf.Max(0f, liveSettings.spinDegreesPerSecond);
                clockwise = liveSettings.clockwise;
                EnsureRenderTarget();
                ApplyCameraSettings();
                float direction = clockwise ? 1f : -1f;
                float angle = Mathf.Repeat(Time.unscaledTime * degreesPerSecond * direction, 360f);
                pivot.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                RenderCurrentFrame();
                return;
            }

            if (image != null && fallbackFrames != null && fallbackFrames.Length > 0)
            {
                image.color = baseColor;
                image.sprite = fallbackFrames[0];
                float angle = Mathf.Repeat(Time.unscaledTime * degreesPerSecond * (clockwise ? 1f : -1f), 360f);
                transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        void ApplyCameraSettings()
        {
            if (previewCamera == null || liveSettings == null) return;

            previewCamera.backgroundColor = liveSettings.backgroundColor;
            previewCamera.fieldOfView = Mathf.Clamp(liveSettings.fieldOfView, 15f, 60f);
            float distance = Mathf.Max(.5f,
                liveRadius * Mathf.Max(2f, liveSettings.Distance(liveCategory)) /
                Mathf.Max(.6f, liveSettings.zoom));
            previewCamera.farClipPlane = Mathf.Max(10f, distance + liveRadius * 4f);
            Vector3 cameraOffset = Quaternion.Euler(
                liveSettings.cameraPitch,
                liveSettings.startingYaw,
                0f) * Vector3.back;
            previewCamera.transform.position = liveStageCenter + cameraOffset * distance;
            previewCamera.transform.LookAt(liveStageCenter);
        }

        void EnsureRenderTarget()
        {
            if (previewCamera == null || liveSettings == null) return;
            int size = Mathf.Clamp(liveSettings.resolution, 96, 512);
            if (target != null && target.width == size && target.height == size) return;

            if (target != null)
            {
                previewCamera.targetTexture = null;
                target.Release();
                Destroy(target);
            }
            target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                name = gameObject.name + " Live Preview",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 4
            };
            target.Create();
            previewCamera.targetTexture = target;
            if (liveImage != null) liveImage.texture = target;
        }

        void RenderCurrentFrame()
        {
            if (previewCamera == null || target == null || !isActiveAndEnabled)
                return;

            try
            {
                if (keyLight != null) keyLight.SetActive(true);
                if (fillLight != null) fillLight.SetActive(true);
                previewCamera.Render();
            }
            finally
            {
                // Unity renders the normal menu camera after LateUpdate, so
                // these must be off again before that render begins.
                if (keyLight != null) keyLight.SetActive(false);
                if (fillLight != null) fillLight.SetActive(false);
            }
        }

        void OnDisable()
        {
            transform.localRotation = Quaternion.identity;
        }

        void OnDestroy()
        {
            CleanupLivePreview();
        }

        void CleanupLivePreview()
        {
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
                previewCamera.gameObject.SetActive(false);
                Destroy(previewCamera.gameObject);
                previewCamera = null;
            }
            bool objectsBelongToPivot = pivot != null;
            foreach (GameObject value in previewObjects)
                if (value != null)
                {
                    value.SetActive(false);
                    if (!objectsBelongToPivot) Destroy(value);
                }
            previewObjects.Clear();
            if (pivot != null)
            {
                pivot.SetActive(false);
                Destroy(pivot);
                pivot = null;
            }
            foreach (Mesh value in bakedMeshes)
                if (value != null) Destroy(value);
            bakedMeshes.Clear();
            if (target != null)
            {
                target.Release();
                Destroy(target);
                target = null;
            }
            if (liveImage != null) liveImage.texture = null;
            liveSettings = null;
        }
    }
}
