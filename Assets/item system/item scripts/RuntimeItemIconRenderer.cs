using System.Collections.Generic;
using UnityEngine;

namespace HitBoss.Items
{
    public static class RuntimeItemIconRenderer
    {
        const int PreviewLayer = 31;
        static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, PreviewDefinition> Definitions = new Dictionary<string, PreviewDefinition>();
        static PlayerCustomizationManager.ItemPreviewSettings settings = new PlayerCustomizationManager.ItemPreviewSettings();

        sealed class PreviewDefinition
        {
            public GameObject[] sources;
            public PlayerCustomizationManager.Category category;
        }

        public static void EnsureIcons(PlayerCustomizationManager manager)
        {
            if (manager == null) return;
            settings = manager.itemPreviewSettings ?? new PlayerCustomizationManager.ItemPreviewSettings();
            Ensure(manager.headAccessories, PlayerCustomizationManager.Category.Head);
            Ensure(manager.body, PlayerCustomizationManager.Category.Body);
            Ensure(manager.bags, PlayerCustomizationManager.Category.Bag);
            Ensure(manager.skates, PlayerCustomizationManager.Category.Skates);
        }

        static void Ensure(PlayerCustomizationManager.CategoryData category, PlayerCustomizationManager.Category categoryType)
        {
            if (category?.items == null) return;

            foreach (var item in category.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;
                Definitions[item.itemId] = new PreviewDefinition
                {
                    sources = item.objectsToEnable,
                    category = categoryType
                };
                if (!Cache.TryGetValue(item.itemId, out Sprite[] frames) && item.icon == null)
                {
                    frames = Render(item.objectsToEnable, item.itemId, categoryType);
                    if (frames != null && frames.Length > 0) Cache[item.itemId] = frames;
                }
                if (item.icon == null && frames != null && frames.Length > 0)
                    item.icon = frames[0];
            }
        }

        public static void ConfigureAnimation(UnityEngine.UI.Image image, string itemId)
        {
            if (image == null) return;
            Cache.TryGetValue(itemId ?? string.Empty, out Sprite[] frames);
            ItemPreviewSpinner spinner = image.GetComponent<ItemPreviewSpinner>();
            if (spinner == null) spinner = image.gameObject.AddComponent<ItemPreviewSpinner>();
            if (Definitions.TryGetValue(itemId ?? string.Empty, out PreviewDefinition definition) &&
                spinner.ConfigureLive(definition.sources, settings, definition.category))
                return;
            spinner.Configure(frames, settings.spinDegreesPerSecond, settings.clockwise);
        }

        static Sprite[] Render(GameObject[] sources, string itemId, PlayerCustomizationManager.Category category)
        {
            if (sources == null || sources.Length == 0) return null;

            var temporary = new List<GameObject>();
            var meshes = new List<Mesh>();
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (GameObject source in sources)
            {
                if (source == null) continue;
                foreach (Renderer sourceRenderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = null;
                    Material[] materials = sourceRenderer.sharedMaterials;

                    if (sourceRenderer is SkinnedMeshRenderer skinned)
                    {
                        mesh = new Mesh { name = itemId + " Preview Mesh" };
                        skinned.BakeMesh(mesh);
                        meshes.Add(mesh);
                    }
                    else
                    {
                        MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                        if (sourceFilter != null) mesh = sourceFilter.sharedMesh;
                    }

                    if (mesh == null) continue;

                    var preview = new GameObject("Item Preview", typeof(MeshFilter), typeof(MeshRenderer));
                    preview.hideFlags = HideFlags.HideAndDontSave;
                    preview.layer = PreviewLayer;
                    preview.transform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
                    preview.transform.localScale = sourceRenderer.transform.lossyScale;
                    preview.GetComponent<MeshFilter>().sharedMesh = mesh;
                    preview.GetComponent<MeshRenderer>().sharedMaterials = materials;
                    temporary.Add(preview);

                    Bounds rendererBounds = preview.GetComponent<Renderer>().bounds;
                    if (!hasBounds) { bounds = rendererBounds; hasBounds = true; }
                    else bounds.Encapsulate(rendererBounds);
                }
            }

            if (!hasBounds)
            {
                Cleanup(temporary, meshes);
                return null;
            }

            // Move the selected meshes into a private render stage. This keeps
            // the player model and every other hidden customization object out
            // of the preview even when they share the same character rig.
            Vector3 previewCenter = new Vector3(10000f, -10000f, 10000f);
            Vector3 stageOffset = previewCenter - bounds.center;
            foreach (GameObject value in temporary)
                value.transform.position += stageOffset;

            bounds = temporary[0].GetComponent<Renderer>().bounds;
            for (int i = 1; i < temporary.Count; i++)
                bounds.Encapsulate(temporary[i].GetComponent<Renderer>().bounds);

            GameObject cameraObject = new GameObject("Item Preview Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // An opaque background prevents the menu character behind the UI
            // from showing through the empty pixels of the item image.
            camera.backgroundColor = settings.backgroundColor;
            camera.orthographic = false;
            camera.fieldOfView = Mathf.Clamp(settings.fieldOfView, 15f, 60f);
            camera.nearClipPlane = .01f;

            GameObject keyLight = CreateLight("Item Preview Key", new Vector3(35, -35, 0), 1.15f);
            GameObject fillLight = CreateLight("Item Preview Fill", new Vector3(320, 140, 0), .65f);

            int size = Mathf.Clamp(settings.resolution, 96, 512);
            const int frameCount = 1;
            var renderTexture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 4
            };
            camera.targetTexture = renderTexture;
            RenderTexture previous = RenderTexture.active;
            var frames = new Sprite[frameCount];
            float radius = Mathf.Max(.15f, bounds.extents.magnitude);
            float distance = Mathf.Max(.5f, radius * Mathf.Max(2f, settings.Distance(category)) / Mathf.Max(.6f, settings.zoom));
            camera.farClipPlane = Mathf.Max(10f, distance + radius * 4f);

            for (int i = 0; i < frameCount; i++)
            {
                float yaw = settings.startingYaw + i * (360f / frameCount);
                Vector3 cameraOffset = Quaternion.Euler(settings.cameraPitch, yaw, 0) * Vector3.back;
                camera.transform.position = bounds.center + cameraOffset * distance;
                camera.transform.LookAt(bounds.center);
                camera.Render();
                RenderTexture.active = renderTexture;

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = itemId + " Preview " + i,
                    hideFlags = HideFlags.DontSave
                };
                texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                texture.Apply();

                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
                sprite.name = itemId + " Preview " + i;
                sprite.hideFlags = HideFlags.DontSave;
                frames[i] = sprite;
            }

            RenderTexture.active = previous;
            camera.targetTexture = null;
            renderTexture.Release();
            cameraObject.SetActive(false);
            keyLight.SetActive(false);
            fillLight.SetActive(false);
            Object.Destroy(renderTexture);
            Object.Destroy(cameraObject);
            Object.Destroy(keyLight);
            Object.Destroy(fillLight);
            Cleanup(temporary, meshes);
            return frames;
        }

        static GameObject CreateLight(string name, Vector3 euler, float intensity)
        {
            var lightObject = new GameObject(name, typeof(Light));
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.transform.rotation = Quaternion.Euler(euler);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
            return lightObject;
        }

        static void Cleanup(List<GameObject> objects, List<Mesh> meshes)
        {
            // Destroy is delayed until the end of the frame. Disable each
            // renderer now so it cannot leak into the next item's preview.
            foreach (GameObject value in objects)
                if (value != null)
                {
                    value.SetActive(false);
                    Object.Destroy(value);
                }
            foreach (Mesh value in meshes) if (value != null) Object.Destroy(value);
        }
    }
}
