using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HitBoss.Multiplayer.Skate
{
    internal sealed class SkateBoostPad
    {
        const float HalfWidth = 2.7f;
        const float HalfLength = 2.7f;
        readonly Transform marker;
        readonly HashSet<SkateCombat> riders = new HashSet<SkateCombat>();
        readonly List<Mesh> fittedMeshes = new List<Mesh>();
        public bool FitsSurface { get; private set; } = true;

        static Mesh arrows, outline;
        static Material glowMaterial, outlineMaterial;

        public SkateBoostPad(Transform parent, Vector3 position, Quaternion direction, Collider surface = null)
        {
            marker = new GameObject("Speed Boost Pad").transform;
            marker.SetParent(parent, false);
            marker.SetPositionAndRotation(position, direction);
            CreateArrows(marker);
            if (surface != null) FitToSurface(surface);
        }

        void FitToSurface(Collider surface)
        {
            foreach (var filter in marker.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = Object.Instantiate(filter.sharedMesh);
                mesh.name = "Ground fitted boost arrows";
                Vector3[] vertices = mesh.vertices;
                float lift = filter.name == "Glowing arrows" ? .07f : .045f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = filter.transform.TransformPoint(vertices[i]);
                    if (surface.Raycast(new Ray(world + Vector3.up * 20f, Vector3.down), out var hit, 50f))
                        vertices[i] = filter.transform.InverseTransformPoint(hit.point + hit.normal * lift);
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                filter.sharedMesh = mesh;
                fittedMeshes.Add(mesh);
                foreach (var vertex in vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    if (!surface.Raycast(new Ray(point + Vector3.up, Vector3.down), out var hit, 2f) ||
                        Vector3.Distance(point, hit.point) > .25f)
                        FitsSurface = false;
                }
            }
        }

        public void Dispose()
        {
            foreach (var mesh in fittedMeshes)
            {
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
            }
            fittedMeshes.Clear();
            if (marker != null)
            {
                if (Application.isPlaying) Object.Destroy(marker.gameObject);
                else Object.DestroyImmediate(marker.gameObject);
            }
        }

        public void CheckRider(SkateCombat player)
        {
            Vector3 local = marker.InverseTransformPoint(player.Position);
            if (Mathf.Abs(local.x) > HalfWidth || Mathf.Abs(local.z) > HalfLength || local.y < 0 || local.y > 1.9f)
            {
                riders.Remove(player);
                return;
            }
            if (player.CanAct && player.Player.Skating.Value && riders.Add(player))
                player.ApplyBoostOnServer(1.7f);
        }

        static void CreateArrows(Transform parent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            if (arrows == null) arrows = BuildChevrons(.5f);
            if (outline == null) outline = BuildChevrons(.8f);
            if (glowMaterial == null) glowMaterial = CreateMaterial(shader, new Color(.78f, 2.1f, .15f));
            if (outlineMaterial == null) outlineMaterial = CreateMaterial(shader, new Color(.05f, .16f, .06f));

            AddLayer(parent, "Arrow outline", outline, outlineMaterial, .025f);
            AddLayer(parent, "Glowing arrows", arrows, glowMaterial, .045f);
        }

        static Material CreateMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            return material;
        }

        static void AddLayer(Transform parent, string name, Mesh mesh, Material material, float height)
        {
            var layer = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = Vector3.up * height;
            layer.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = layer.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh BuildChevrons(float width)
        {
            var points = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                float tail = -2.2f + i * 1.45f;
                Vector3 tip = new Vector3(0, 0, tail + 1.15f);
                AddBar(points, triangles, new Vector3(-2.3f, 0, tail), tip, width);
                AddBar(points, triangles, tip, new Vector3(2.3f, 0, tail), width);
            }
            var mesh = new Mesh { name = "Three Boost Chevrons", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(points);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static void AddBar(List<Vector3> points, List<int> triangles, Vector3 from, Vector3 to, float width)
        {
            Vector3 side = Vector3.Cross(Vector3.up, to - from).normalized * (width * .5f);
            int first = points.Count;
            points.Add(from - side);
            points.Add(to - side);
            points.Add(to + side);
            points.Add(from + side);
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }
    }
}
