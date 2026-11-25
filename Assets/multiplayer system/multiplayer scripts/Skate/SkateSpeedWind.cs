using UnityEngine;
using UnityEngine.Rendering;

namespace HitBoss.Multiplayer.Skate
{
    [RequireComponent(typeof(Camera), typeof(PlayerCamera))]
    internal sealed class SkateSpeedWind : MonoBehaviour
    {
        Camera view;
        PlayerCamera chase;
        ParticleSystem wind;
        float nextParticle;

        void Awake()
        {
            view = GetComponent<Camera>();
            chase = GetComponent<PlayerCamera>();
            var host = new GameObject("Speed wind", typeof(ParticleSystem));
            host.transform.SetParent(transform, false);
            wind = host.GetComponent<ParticleSystem>();
            wind.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = wind.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 24;
            var shape = wind.shape;
            shape.enabled = false;
            var emission = wind.emission;
            emission.enabled = false;
            var colors = wind.colorOverLifetime;
            colors.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.65f, .15f), new GradientAlphaKey(0f, 1f) });
            colors.color = fade;
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SkateCombatEffects.EffectMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 5f;
            renderer.velocityScale = .08f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            wind.Play();
        }

        void LateUpdate()
        {
            float strength = chase.isActiveAndEnabled && view.isActiveAndEnabled ? chase.RaceSpeedEffect : 0f;
            if (strength < .1f || Time.time < nextParticle) return;
            nextParticle = Time.time + Mathf.Lerp(.2f, .045f, strength);
            float edge = Random.Range(.025f, .1f);
            var viewport = new Vector3(Random.value < .5f ? edge : 1f - edge, Random.Range(.12f, .88f), 3.8f);
            Vector3 origin = transform.InverseTransformPoint(view.ViewportToWorldPoint(viewport));
            var particle = new ParticleSystem.EmitParams
            {
                position = origin,
                velocity = new Vector3(origin.x * 2f, origin.y * 2f, -Mathf.Lerp(3f, 9f, strength)),
                startLifetime = Random.Range(.19f, .31f),
                startSize = Mathf.Lerp(.018f, .028f, strength),
                startColor = new Color(.72f, .94f, 1f, Mathf.Lerp(.1f, .28f, strength))
            };
            wind.Emit(particle, 1);
        }
    }
}
