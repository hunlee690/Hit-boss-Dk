using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace HitBoss.Multiplayer.Skate
{
    public sealed class SkatePickupPresentation : MonoBehaviour
    {
        static Material boxMaterial;
        static Material frameMaterial;
        Transform floating, caption;
        Vector3 basePosition;
        float phase, appearedAt;
        bool configured;

        public void Configure()
        {
            if (configured) return;
            configured = true;
            var source = GetComponent<MeshFilter>();
            var sourceRenderer = GetComponent<MeshRenderer>();
            if (source == null || source.sharedMesh == null) { enabled = false; return; }
            if (sourceRenderer != null) sourceRenderer.enabled = false;
            floating = new GameObject("Floating item box").transform;
            floating.SetParent(transform, false);
            Vector3 scale = transform.lossyScale;
            floating.localScale = new Vector3(1.35f / Mathf.Max(.01f, Mathf.Abs(scale.x)),
                1.35f / Mathf.Max(.01f, Mathf.Abs(scale.y)), 1.35f / Mathf.Max(.01f, Mathf.Abs(scale.z)));
            basePosition = Vector3.up * (.65f / Mathf.Max(.01f, Mathf.Abs(scale.y)));
            floating.localPosition = basePosition;
            phase = Mathf.Repeat(transform.position.x * .31f + transform.position.z * .17f, Mathf.PI * 2f);
            appearedAt = Time.time;

            var body = new GameObject("Box", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(floating, false);
            body.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BoxMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var border = new GameObject("Bright edges", typeof(LineRenderer));
            border.transform.SetParent(floating, false);
            var frame = border.GetComponent<LineRenderer>();
            frame.useWorldSpace = false;
            frame.sharedMaterial = FrameMaterial();
            frame.startWidth = frame.endWidth = .026f;
            frame.shadowCastingMode = ShadowCastingMode.Off;
            frame.receiveShadows = false;
            var corners = Corners();
            frame.positionCount = corners.Length;
            frame.SetPositions(corners);

            var labelObject = new GameObject("Mystery label", typeof(TextMeshPro));
            caption = labelObject.transform;
            caption.SetParent(floating, false);
            caption.localPosition = Vector3.up * .8f;
            var label = labelObject.GetComponent<TextMeshPro>();
            label.text = "?";
            label.fontSize = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(3f, .6f);
        }

        void LateUpdate()
        {
            if (!configured || floating == null) return;
            float age = Time.time - appearedAt;
            float bob = Mathf.Sin(Time.time * 2.4f + phase) * .18f;
            float spring = Mathf.Sin(age * 12f) * Mathf.Exp(-age * 3f) * .2f;
            float scaleY = Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.y));
            floating.localPosition = basePosition + Vector3.up * ((bob + spring) / scaleY);
            floating.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.7f + phase) * 5f,
                Time.time * 40f + phase * Mathf.Rad2Deg, 5f);
            Camera view = Camera.main;
            if (view != null && caption != null) caption.rotation = view.transform.rotation;
        }

        static Material BoxMaterial()
        {
            if (boxMaterial == null) boxMaterial = MakeMaterial(new Color(.6f, .22f, 1f));
            return boxMaterial;
        }
        static Material FrameMaterial()
        {
            if (frameMaterial == null) frameMaterial = MakeMaterial(new Color(.85f, .95f, 1f));
            return frameMaterial;
        }
        static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            return material;
        }
        static Vector3[] Corners()
        {
            const float s = .51f;
            var a = new Vector3(-s,-s,-s); var b = new Vector3(-s,-s,s);
            var c = new Vector3(s,-s,s); var d = new Vector3(s,-s,-s);
            var e = new Vector3(-s,s,-s); var f = new Vector3(-s,s,s);
            var g = new Vector3(s,s,s); var h = new Vector3(s,s,-s);
            return new[] { a,b,c,d,a,e,f,b,f,g,c,g,h,d,h,e };
        }
    }
}
