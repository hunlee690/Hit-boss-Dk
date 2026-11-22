using UnityEngine;
using UnityEngine.Rendering;

namespace HitBoss.Multiplayer.Skate
{
    // Lightweight procedural effects keep combat readable without requiring
    // artists to assign extra prefabs to every network item.
    public static class SkateCombatEffects
    {
        static Material particleMaterial;
        static Material trailMaterial;
        static AudioClip pickupSound;

        public static void ConfigureMaterials(Material particles, Material trails, AudioClip pickup=null)
        {
            if (particles != null) particleMaterial = particles;
            if (trails != null) trailMaterial = trails;
            pickupSound=pickup;
        }

        public static void SpawnPickupPop(Vector3 position)
        {
            if(!AllowEffect(position,45f))return;
            var pop=CreateParticles("Mystery box pickup pop",position,new Color(.3f,.9f,1f,1f),new Color(.8f,.35f,1f,0),16,3.8f,.12f,.5f,.25f);
            var shape=pop.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.4f;
            var main=pop.main;main.startColor=new ParticleSystem.MinMaxGradient(new Color(.3f,.9f,1f),new Color(1f,.8f,.18f));
            RestartBurst(pop,16);
            var ring=CreateParticles("Pickup sparkle ring",position,new Color(1f,.9f,.4f,1),Color.clear,12,2f,.07f,.32f,0);
            var ringShape=ring.shape;ringShape.shapeType=ParticleSystemShapeType.Circle;ringShape.radius=.5f;
            ring.transform.rotation=Quaternion.Euler(90,0,0);RestartBurst(ring,12);
            if(pickupSound!=null)AudioSource.PlayClipAtPoint(pickupSound,position,.28f);
        }

        static int budgetFrame, effectsThisFrame;
        public static bool Nearby(Vector3 position,float distance=65f)
        {
            Camera camera=null;
            foreach(var player in SkateCombat.Players)
                if(player!=null && player.IsOwner && !player.Player.IsBot.Value) { camera=player.Player.playerCamera!=null?player.Player.playerCamera.GetComponent<Camera>():null; break; }
            if(camera==null)camera=Camera.main;
            return camera==null || (camera.transform.position-position).sqrMagnitude<distance*distance;
        }
        static bool AllowEffect(Vector3 position,float distance)
        {
            if(!Nearby(position,distance))return false;
            if(budgetFrame!=Time.frameCount){budgetFrame=Time.frameCount;effectsThisFrame=0;}
            return ++effectsThisFrame<=6;
        }
        public static void SpawnBoost(Transform skater, float seconds)
        {
            if(skater==null || !AllowEffect(skater.position,45f))return;
            var green = new Color(.62f, 1f, .15f, .95f);
            var gold = new Color(1f, .9f, .25f, 0f);
            var burst = CreateParticles("Boost flash", skater.position - skater.forward * .45f,
                green, gold, 12, 6f, .1f, .5f, 0f);
            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = .35f;
            burst.transform.rotation = Quaternion.LookRotation(-skater.forward);
            RestartBurst(burst, 12);

            for (int side = -1; side <= 1; side += 2)
            {
                var streak = new GameObject("Speed Boost Streak");
                var trail = streak.AddComponent<TrailRenderer>();
                ConfigureTrail(trail, .25f, .36f, green, gold);
                streak.AddComponent<SkateTemporaryTrail>().Configure(
                    skater, new Vector3(side * .38f, -.45f, -.55f), seconds, .4f);
            }
        }

        public static void SpawnMelee(Transform fighter, bool slide)
        {
            if (fighter == null || !AllowEffect(fighter.position,40f)) return;
            Vector3 position = fighter.position + Vector3.up * (slide ? .35f : 1.05f)
                + fighter.forward * (slide ? 1.15f : .9f);
            Color primary = slide
                ? new Color(.15f, .8f, 1f, .9f)
                : new Color(1f, .8f, .2f, .95f);
            Color secondary = slide
                ? new Color(.75f, 1f, 1f, .1f)
                : new Color(1f, .25f, .08f, .1f);

            ParticleSystem particles = CreateParticles(
                slide ? "Slide Spark" : "Punch Spark",
                position,
                primary,
                secondary,
                slide ? 13 : 9,
                slide ? 2.8f : 2.1f,
                slide ? .13f : .1f,
                slide ? .34f : .25f,
                .08f);

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = slide ? 16f : 25f;
            shape.radius = .12f;
            particles.transform.rotation = Quaternion.LookRotation(fighter.forward);
            RestartBurst(particles, slide ? 13 : 9);
            if (slide) SpawnSlideTrail(fighter);
        }

        public static void SpawnExplosion(Vector3 position, bool mine)
        {
            if(!AllowEffect(position,70f))return;
            Color hot = mine
                ? new Color(1f, .28f, .04f, 1f)
                : new Color(1f, .65f, .08f, 1f);
            Color smokeColor = new Color(.22f, .15f, .12f, .05f);

            ParticleSystem burst = CreateParticles(
                mine ? "Mine Blast" : "Bomb Blast",
                position + Vector3.up * .2f,
                hot,
                smokeColor,
                mine ? 22 : 28,
                mine ? 7f : 8.5f,
                .18f,
                mine ? .85f : 1.1f,
                .22f);
            ParticleSystem.ShapeModule burstShape = burst.shape;
            burstShape.shapeType = ParticleSystemShapeType.Sphere;
            burstShape.radius = .22f;
            RestartBurst(burst, mine ? 22 : 28);

            ParticleSystem sparks = CreateParticles(
                "Blast Sparks",
                position + Vector3.up * .25f,
                new Color(1f, .95f, .5f, 1f),
                new Color(1f, .15f, .02f, 0f),
                mine ? 12 : 16,
                mine ? 10f : 12f,
                .045f,
                .55f,
                .55f);
            ParticleSystem.ShapeModule sparkShape = sparks.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = .08f;
            RestartBurst(sparks, mine ? 12 : 16);

            ParticleSystem smoke = CreateParticles(
                "Blast Smoke",
                position + Vector3.up * .35f,
                new Color(.32f, .29f, .27f, .38f),
                new Color(.08f, .08f, .08f, 0f),
                mine ? 8 : 10,
                mine ? 2.2f : 2.8f,
                mine ? .4f : .5f,
                mine ? 1f : 1.2f,
                -.12f);
            ParticleSystem.ShapeModule smokeShape = smoke.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Sphere;
            smokeShape.radius = mine ? .35f : .48f;
            ParticleSystem.NoiseModule smokeNoise = smoke.noise;
            smokeNoise.enabled = true;
            smokeNoise.strength = .45f;
            smokeNoise.frequency = .35f;
            smokeNoise.scrollSpeed = .2f;
            RestartBurst(smoke, mine ? 8 : 10);
        }

        public static void AttachGroundTrail(Transform player, ulong networkId, bool bot)
        {
            if (player == null || player.GetComponent<SkateGroundTrail>() != null) return;
            Color color = bot
                ? Color.HSVToRGB(Mathf.Repeat((float)(networkId % 17) / 17f + .05f, 1f), .72f, 1f)
                : new Color(.15f, .85f, 1f, 1f);
            player.gameObject.AddComponent<SkateGroundTrail>().Configure(player, color);
        }

        static void SpawnSlideTrail(Transform fighter)
        {
            var trailObject = new GameObject("Slide Trail");
            var trail = trailObject.AddComponent<TrailRenderer>();
            ConfigureTrail(
                trail,
                .38f,
                .48f,
                new Color(.12f, .82f, 1f, .85f),
                new Color(.3f, .9f, 1f, 0f));
            trailObject.AddComponent<SkateTemporaryTrail>().Configure(
                fighter,
                new Vector3(0f, .08f, -.45f),
                .32f,
                .58f);
        }

        static ParticleSystem CreateParticles(
            string name,
            Vector3 position,
            Color start,
            Color end,
            int count,
            float speed,
            float size,
            float lifetime,
            float gravity)
        {
            var root = new GameObject(name, typeof(ParticleSystem));
            root.transform.position = position;
            ParticleSystem particles = root.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = .12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size);
            main.startColor = start;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(64, count + 8);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colors.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizes = particles.sizeOverLifetime;
            sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, .45f),
                    new Keyframe(.18f, 1f),
                    new Keyframe(1f, 0f)));

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EffectMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Object.Destroy(root, lifetime + .5f);
            return particles;
        }

        static void RestartBurst(ParticleSystem particles, int count)
        {
            particles.Clear(true);
            particles.Emit(count);
            particles.Play(true);
        }

        internal static Material EffectMaterial()
        {
            if (particleMaterial != null) return particleMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            particleMaterial = new Material(shader)
            {
                name = "Runtime Combat Effect",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3000
            };
            return particleMaterial;
        }

        internal static void ConfigureTrail(
            TrailRenderer trail,
            float width,
            float time,
            Color start,
            Color end)
        {
            trail.sharedMaterial = TrailMaterial();
            trail.time = time;
            trail.minVertexDistance = .08f;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, width),
                new Keyframe(1f, width * .12f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
        }

        static Material TrailMaterial()
        {
            if (trailMaterial != null) return trailMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            trailMaterial = new Material(shader)
            {
                name = "Runtime Skate Trail",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3000
            };
            if (trailMaterial.HasProperty("_Surface")) trailMaterial.SetFloat("_Surface", 1f);
            if (trailMaterial.HasProperty("_Blend")) trailMaterial.SetFloat("_Blend", 0f);
            if (trailMaterial.HasProperty("_BaseColor")) trailMaterial.SetColor("_BaseColor", Color.white);
            return trailMaterial;
        }
    }

    public sealed class SkateGroundTrail : MonoBehaviour
    {
        Transform target;
        TrailRenderer[] trails;
        Vector3 lastPosition;
        bool ready;
        Color normalColor;
        bool nearby = true;
        float nextVisibilityCheck;
        readonly RaycastHit[] groundHits = new RaycastHit[16];

        public void Configure(Transform value, Color color)
        {
            target = value;
            normalColor = color;
            lastPosition = target.position;
            trails = new TrailRenderer[2];
            for (int i = 0; i < trails.Length; i++)
            {
                var trailObject = new GameObject(i == 0 ? "Left Ground Trail" : "Right Ground Trail");
                trails[i] = trailObject.AddComponent<TrailRenderer>();
                SkateCombatEffects.ConfigureTrail(
                    trails[i],
                    .085f,
                    .8f,
                    new Color(color.r, color.g, color.b, .35f),
                    new Color(color.r, color.g, color.b, 0f));
            }
            ready = true;
        }

        public void SetBoosting(bool boosting)
        {
            Color color = boosting ? new Color(.7f, 1f, .13f) : normalColor;
            foreach (TrailRenderer trail in trails)
            {
                trail.startColor = new Color(color.r, color.g, color.b, boosting ? .65f : .35f);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                trail.widthMultiplier = boosting ? 1.5f : 1f;
            }
        }

        void LateUpdate()
        {
            if (!ready || target == null || trails == null) return;
            float moved = (target.position - lastPosition).magnitude;
            float speed = moved / Mathf.Max(Time.deltaTime, .001f);
            lastPosition = target.position;
            bool teleported = moved > 3f;
            if(Time.time>=nextVisibilityCheck){nearby=SkateCombatEffects.Nearby(target.position,45f);nextVisibilityCheck=Time.time+.2f;}
            bool moving = nearby && speed > .55f && !teleported;

            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                if (teleported) trails[i].Clear();
                float side = i == 0 ? -.18f : .18f;
                Vector3 wanted = target.position - target.forward * .38f + target.right * side + Vector3.up * .25f;
                Vector3 grounded = wanted;
                int hitCount = moving ? Physics.RaycastNonAlloc(wanted + Vector3.up * 1.5f, Vector3.down, groundHits, 4f, ~0, QueryTriggerInteraction.Ignore) : 0;
                float best = float.PositiveInfinity; bool hasGround=false;
                for(int h=0;h<hitCount;h++)
                {
                    var hit=groundHits[h];
                    if(hit.collider.GetComponentInParent<NetworkPlayer>()!=null || hit.collider.GetComponentInParent<SkateNetworkItem>()!=null || hit.normal.y<.4f || hit.distance>=best)continue;
                    best=hit.distance;hasGround=true;grounded=hit.point+hit.normal*.035f;
                }
                trails[i].transform.position = grounded;
                trails[i].emitting = moving && hasGround;
            }
        }

        void OnDisable()
        {
            if (trails == null) return;
            foreach (TrailRenderer trail in trails)
                if (trail != null) trail.emitting = false;
        }

        void OnDestroy()
        {
            if (trails == null) return;
            foreach (TrailRenderer trail in trails)
                if (trail != null) Destroy(trail.gameObject);
        }
    }

    public sealed class SkateTemporaryTrail : MonoBehaviour
    {
        Transform target;
        Vector3 localOffset;
        float followUntil;
        float destroyAt;
        TrailRenderer trail;

        public void Configure(Transform value, Vector3 offset, float followFor, float fadeFor)
        {
            target = value;
            localOffset = offset;
            followUntil = Time.time + followFor;
            destroyAt = followUntil + fadeFor;
            trail = GetComponent<TrailRenderer>();
            if (trail != null) trail.emitting = true;
            Follow();
        }

        void LateUpdate()
        {
            if (Time.time < followUntil && target != null)
            {
                Follow();
                return;
            }
            if (trail != null) trail.emitting = false;
            if (Time.time >= destroyAt) Destroy(gameObject);
        }

        void Follow()
        {
            transform.position = target.TransformPoint(localOffset);
        }
    }
}
