using System.Linq;
using UnityEngine;

namespace HitBoss.Multiplayer.Skate
{
    // Distance is measured along the closed route, never straight to the finish line.
    internal sealed class SkateRaceTrack
    {
        readonly Vector3[] points;
        readonly float[] lengths;
        readonly float halfWidth;
        public float Length => lengths[points.Length];

        public SkateRaceTrack(MapArenaSetup arena)
        {
            points = arena.raceRoute.Where(p => p != null).Select(p => p.position).ToArray();
            lengths = new float[points.Length + 1];
            // Match Ring's outer bank extends farther than the flat lane around the centerline.
            halfWidth = Mathf.Max(arena.raceTrackHalfWidth, 32f);
            for (int i = 0; i < points.Length; i++)
                lengths[i + 1] = lengths[i] + Flat(points[(i + 1) % points.Length] - points[i]).magnitude;
        }

        public bool Project(Vector3 position, out float distance)
        {
            float closest = halfWidth * halfWidth;
            distance = 0;
            bool found = false;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 start = points[i], offset = points[(i + 1) % points.Length] - start;
                Vector3 flat = Flat(offset);
                if (flat.sqrMagnitude < .001f) continue;
                float t = Mathf.Clamp01(Vector3.Dot(Flat(position - start), flat) / flat.sqrMagnitude);
                Vector3 sample = start + offset * t;
                float squared = Flat(position - sample).sqrMagnitude;
                if (squared >= closest || Mathf.Abs(position.y - sample.y) > 35f) continue;
                closest = squared;
                distance = lengths[i] + flat.magnitude * t;
                found = true;
            }
            return found;
        }

        public float Delta(float previous, float next) =>
            Mathf.Repeat(next - previous + Length * .5f, Length) - Length * .5f;

        public Vector3 PointAt(float distance)
        {
            float wrapped = Mathf.Repeat(distance, Length);
            int index = System.Array.BinarySearch(lengths, wrapped);
            if (index < 0) index = ~index - 1;
            return Vector3.Lerp(points[index], points[(index + 1) % points.Length],
                Mathf.InverseLerp(lengths[index], lengths[index + 1], wrapped));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
