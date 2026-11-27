using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HitBoss.Items.Editor
{
    // Uses an isolated editor preview scene; editing sliders never alters the player.
    public sealed class ItemInspectorPreview : IDisposable
    {
        PreviewRenderUtility utility;
        GameObject root;
        PlayerCustomizationManager.CustomizationItem current;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        readonly List<Material> originals = new List<Material>();
        float radius = 1, zoom = 1;
        Vector2 orbit = new Vector2(145, 10);

        public void Draw(Rect rect, PlayerCustomizationManager.CustomizationItem item, Material replacement,
            Color color, float metallic, float roughness, float alpha)
        {
            if (item == null) return;
            if (current != item || utility == null) Build(item);
            if (root == null || materials.Count == 0)
            {
                GUI.Box(rect, "Assign a model to preview this item.");
                return;
            }
            var e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    orbit.x += e.delta.x; orbit.y = Mathf.Clamp(orbit.y + e.delta.y, -80, 80);
                    e.Use(); GUI.changed = true;
                }
                if (e.type == EventType.ScrollWheel)
                {
                    zoom = Mathf.Clamp(zoom + e.delta.y * .04f, .5f, 2.5f);
                    e.Use(); GUI.changed = true;
                }
            }
            if (Event.current.type != EventType.Repaint) return;
            color.a = alpha;
            for (int i = 0; i < materials.Count; i++)
            {
                Material source = replacement != null ? replacement : originals[i];
                var m = materials[i];
                m.shader = source.shader;
                m.CopyPropertiesFromMaterial(source);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1 - roughness);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 1 - roughness);
                bool transparent = alpha < .999f;
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", transparent ? 1 : 0);
                if (m.HasProperty("_Mode")) m.SetFloat("_Mode", transparent ? 3 : 0);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", transparent ? 5 : 1);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", transparent ? 10 : 0);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", transparent ? 0 : 1);
                m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                if (transparent) { m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHABLEND_ON"); }
                else { m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHABLEND_ON"); }
                m.renderQueue = transparent ? 3000 : 2000;
            }
            utility.BeginPreview(rect, GUIStyle.none);
            float fit = radius / Mathf.Sin(15 * Mathf.Deg2Rad);
            float aspectFit = Mathf.Max(1, rect.height / Mathf.Max(1, rect.width));
            utility.camera.transform.position = Quaternion.Euler(orbit.y, orbit.x, 0) * Vector3.back * fit * 1.15f * zoom * aspectFit;
            utility.camera.transform.LookAt(Vector3.zero);
            utility.camera.nearClipPlane = .01f;
            utility.camera.farClipPlane = fit * 10 + 10;
            utility.Render(true);
            var texture = utility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        void Build(PlayerCustomizationManager.CustomizationItem item)
        {
            Dispose(); current = item;
            utility = new PreviewRenderUtility();
            utility.cameraFieldOfView = 30;
            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = new Color(.09f, .11f, .17f);
            utility.camera.allowHDR = false;
            utility.lights[0].intensity = 1.1f;
            utility.lights[0].transform.rotation = Quaternion.Euler(35, 30, 0);
            utility.lights[1].intensity = .7f;
            utility.lights[1].transform.rotation = Quaternion.Euler(320, 210, 0);
            utility.ambientColor = new Color(.25f, .25f, .25f);
            root = new GameObject("Inspector Item Preview") { hideFlags = HideFlags.HideAndDontSave };
            utility.AddSingleGO(root);
            Bounds bounds = default; bool found = false;
            var seen = new HashSet<Renderer>();
            foreach (var source in item.objectsToEnable ?? Array.Empty<GameObject>())
            {
                if (source == null) continue;
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    if (!seen.Add(renderer)) continue;
                    Mesh mesh = null;
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                        skinned.BakeMesh(mesh); meshes.Add(mesh);
                    }
                    else if (renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                    if (mesh == null) continue;
                    var go = new GameObject("Item Mesh", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                    go.transform.localScale = renderer.transform.lossyScale;
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] == null) continue;
                        originals.Add(slots[i]);
                        slots[i] = new Material(slots[i]) { hideFlags = HideFlags.HideAndDontSave };
                        materials.Add(slots[i]);
                    }
                    var output = go.GetComponent<MeshRenderer>(); output.sharedMaterials = slots;
                    if (!found) { bounds = output.bounds; found = true; } else bounds.Encapsulate(output.bounds);
                }
            }
            if (found) { root.transform.position = -bounds.center; radius = Mathf.Max(.05f, bounds.extents.magnitude); }
        }

        public void Dispose()
        {
            utility?.Cleanup(); utility = null; root = null; current = null;
            foreach (var m in materials) if (m != null) UnityEngine.Object.DestroyImmediate(m);
            foreach (var m in meshes) if (m != null) UnityEngine.Object.DestroyImmediate(m);
            materials.Clear(); originals.Clear(); meshes.Clear();
        }
    }
}
