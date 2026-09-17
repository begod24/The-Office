using Office.Data;
using Office.Enemies;
using Office.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Office.Editor
{
    // The three rigged office enemies: a view prefab built out of each FBX with its procedural
    // walker wired to the rig's own IK and pole bones, and the definition that spawns it.
    //
    // Nothing here is authored by hand. The rigs arrive from Blender with fixed bone names
    // (Leg_*_Thigh, IK_Leg_*, Pole_Leg_*, see each model's README), so wiring twenty-odd
    // references per enemy is a loop rather than an afternoon in the inspector — and a reimport
    // that renames a bone fails loudly here instead of silently animating nothing.
    internal static class RiggedEnemyBuilder
    {
        private const string ModelFolder = "Assets/Project/Art/Models/Enemies";
        private const string VfxFolder = "Assets/Project/Art/VFX";

        private const string PuddlePath =
            EnemyContentBuilder.PrefabFolder + "/VIEW_HZD_WaterPuddle.prefab";

        private static readonly (string Tag, int Group)[] QuadrupedLegs =
        {
            // Diagonals step together: the pairing every four legged thing on earth uses,
            // because the other two keep the body over its own footprint.
            ("FL", 0), ("BR", 0), ("FR", 1), ("BL", 1)
        };

        private static readonly (string Tag, int Group)[] BipedLegs = { ("L", 0), ("R", 1) };

        [MenuItem("Office/Content/Build Rigged Enemies", priority = 22)]
        public static void BuildAndRegister()
        {
            BuildAll();

            AssetDatabase.SaveAssets();
            ItemContentBuilder.RebuildRegistry();

            Debug.Log("[Enemy] Ceiling fan, water cooler and projector rebuilt. Add their " +
                      "EnemyPlacement markers to a level — the sandbox builder already carries " +
                      "one of each.");
        }

        internal static void BuildAll()
        {
            var puddle = BuildPuddle();

            BuildCeilingFan();
            BuildWaterCooler(puddle);
            BuildProjector();
        }

        private static void BuildCeilingFan()
        {
            var instance = OpenModel("CeilingFan", "VIEW_ENM_CeilingFan");
            if (instance == null) return;

            var body = Bone(instance, "Body");
            var muzzle = Bone(instance, "Muzzle_Wind");

            var walker = instance.AddComponent<ProceduralWalker>();

            WireLegs(walker, instance, body, QuadrupedLegs, hips: true, tarsus: false);

            Numbers(walker,
                ("stepReach", 0.75f), ("stepHeight", 0.4f),
                ("minStepDuration", 0.2f), ("maxStepDuration", 0.55f),
                ("settleFraction", 0.3f), ("hipYaw", 35f),
                ("bodyFollowSpeed", 6f), ("maxTilt", 10f),
                ("bobAmplitude", 0.07f), ("breatheAmplitude", 0.035f), ("breatheFrequency", 0.3f),
                ("leanPerSpeed", 1.6f), ("leanPerAcceleration", 0.5f), ("maxLean", 9f),
                ("collapseDrop", 0.9f), ("collapseTilt", 22f),
                ("probeUp", 1.8f), ("probeDown", 4f), ("teleportDistance", 4f));

            var gust = BuildGust(muzzle);
            var draft = BuildDraft(muzzle);

            var view = instance.AddComponent<CeilingFanView>();

            EnemyContentBuilder.Wire(view,
                ("Walker", walker),
                ("housing", Bone(instance, "Fan_Housing")),
                ("rotor", Bone(instance, "Fan_Rotor")),
                ("gust", gust),
                ("draft", draft));

            Numbers(view, ("aimTurnSpeed", 120f));

            var prefab = SaveView(instance, "VIEW_ENM_CeilingFan");

            WriteDefinition("ENM_CeilingFan", prefab, serialized =>
            {
                Set(serialized, "displayName", "CEILING FAN");

                Set(serialized, "maxHealth", 120f);
                Responses(serialized, (DamageType.Blunt, 1.6f));

                Set(serialized, "bodyRadius", 0.9f);
                Set(serialized, "bodyHeight", 2.5f);

                Set(serialized, "patrolSpeed", 1f);
                Set(serialized, "chaseSpeed", 2.6f);
                Set(serialized, "acceleration", 6f);
                Set(serialized, "turnSpeed", 150f);
                Set(serialized, "chaseStopDistance", 3.2f);

                Set(serialized, "sightRadius", 15f);
                Set(serialized, "sightAngle", 120f);
                Set(serialized, "memorySeconds", 5f);
                Set(serialized, "hearingRadius", 18f);

                Set(serialized, "attackDamage", 18f);
                Set(serialized, "attackDamageType", (int)DamageType.Blunt);
                Set(serialized, "attackRange", 5.5f);
                Set(serialized, "attackWindup", 1.1f);
                Set(serialized, "attackCooldown", 3f);
                Set(serialized, "attackConeAngle", 70f);
                Set(serialized, "attackKnockback", 9f);

                Set(serialized, "corpseSeconds", 14f);
            });
        }

        private static void BuildWaterCooler(GameObject puddle)
        {
            var instance = OpenModel("WaterCooler", "VIEW_ENM_WaterCooler");
            if (instance == null) return;

            var body = Bone(instance, "Body");
            var muzzle = Bone(instance, "Muzzle_Water");

            var walker = instance.AddComponent<ProceduralWalker>();

            WireLegs(walker, instance, body, QuadrupedLegs, hips: true, tarsus: false);

            Numbers(walker,
                ("stepReach", 0.3f), ("stepHeight", 0.18f),
                ("minStepDuration", 0.14f), ("maxStepDuration", 0.45f),
                ("settleFraction", 0.3f), ("hipYaw", 30f),
                ("bodyFollowSpeed", 8f), ("maxTilt", 12f),
                ("bobAmplitude", 0.035f), ("breatheAmplitude", 0.02f), ("breatheFrequency", 0.35f),
                ("leanPerSpeed", 1.8f), ("leanPerAcceleration", 0.6f), ("maxLean", 10f),
                ("collapseDrop", 0.55f), ("collapseTilt", 28f),
                ("probeUp", 1.2f), ("probeDown", 3f), ("teleportDistance", 3f));

            var jet = BuildJet(muzzle);
            var drip = BuildDrip(muzzle);

            var view = instance.AddComponent<WaterCoolerView>();

            EnemyContentBuilder.Wire(view,
                ("Walker", walker),
                ("nozzle", Bone(instance, "Nozzle")),
                ("jug", Bone(instance, "Jug")),
                ("jet", jet),
                ("drip", drip));

            var prefab = SaveView(instance, "VIEW_ENM_WaterCooler");

            WriteDefinition("ENM_WaterCooler", prefab, serialized =>
            {
                Set(serialized, "displayName", "WATER COOLER");

                Set(serialized, "maxHealth", 70f);
                Responses(serialized, (DamageType.Blunt, 1.6f));

                Set(serialized, "bodyRadius", 0.5f);
                Set(serialized, "bodyHeight", 1.8f);

                Set(serialized, "patrolSpeed", 1.2f);
                Set(serialized, "chaseSpeed", 3f);
                Set(serialized, "acceleration", 9f);
                Set(serialized, "turnSpeed", 260f);
                Set(serialized, "chaseStopDistance", 4.5f);

                Set(serialized, "sightRadius", 14f);
                Set(serialized, "sightAngle", 110f);
                Set(serialized, "memorySeconds", 4f);
                Set(serialized, "hearingRadius", 16f);

                Set(serialized, "attackDamage", 10f);
                Set(serialized, "attackDamageType", (int)DamageType.Water);
                Set(serialized, "attackRange", 7f);
                Set(serialized, "attackWindup", 0.7f);
                Set(serialized, "attackCooldown", 2.2f);
                Set(serialized, "attackConeAngle", 18f);
                Set(serialized, "attackKnockback", 2.5f);

                serialized.FindProperty("attackHazard").objectReferenceValue = puddle;

                Set(serialized, "corpseSeconds", 10f);
            });
        }

        private static void BuildProjector()
        {
            var instance = OpenModel("ProjectorCamera", "VIEW_ENM_Projector");
            if (instance == null) return;

            var body = Bone(instance, "Body");
            var lens = Bone(instance, "Lens_Light");

            var walker = instance.AddComponent<ProceduralWalker>();

            WireLegs(walker, instance, body, BipedLegs, hips: false, tarsus: true);

            Numbers(walker,
                ("stepReach", 0.13f), ("stepHeight", 0.08f),
                ("minStepDuration", 0.1f), ("maxStepDuration", 0.3f),
                ("settleFraction", 0.35f), ("hipYaw", 0f),
                ("bodyFollowSpeed", 10f), ("maxTilt", 14f),
                ("bobAmplitude", 0.02f), ("breatheAmplitude", 0.012f), ("breatheFrequency", 0.5f),
                ("leanPerSpeed", 2.4f), ("leanPerAcceleration", 0.8f), ("maxLean", 12f),
                ("collapseDrop", 0.22f), ("collapseTilt", 30f),
                ("probeUp", 0.6f), ("probeDown", 2f), ("teleportDistance", 2.5f));

            var lamp = BuildLamp(lens);
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);

            var view = instance.AddComponent<ProjectorView>();

            EnemyContentBuilder.Wire(view,
                ("Walker", walker),
                ("headPan", Bone(instance, "Head_Pan")),
                ("headTilt", Bone(instance, "Head_Tilt")),
                ("lamp", lamp),
                ("lensRenderer", renderer));

            Integer(view, "lensMaterial", LensMaterialIndex(renderer));

            var prefab = SaveView(instance, "VIEW_ENM_Projector");

            WriteDefinition("ENM_Projector", prefab, serialized =>
            {
                Set(serialized, "displayName", "PROJECTOR");

                // Fragile on purpose: GDD §9.1 answers the projector with "destroy the
                // projector", which is only advice if it can be destroyed quickly.
                Set(serialized, "maxHealth", 30f);
                Responses(serialized);

                Set(serialized, "bodyRadius", 0.25f);
                Set(serialized, "bodyHeight", 0.7f);

                Set(serialized, "patrolSpeed", 1.8f);
                Set(serialized, "chaseSpeed", 4f);
                Set(serialized, "acceleration", 14f);
                Set(serialized, "turnSpeed", 540f);
                Set(serialized, "chaseStopDistance", 6f);

                Set(serialized, "sightRadius", 16f);
                Set(serialized, "sightAngle", 100f);
                Set(serialized, "memorySeconds", 3f);
                Set(serialized, "hearingRadius", 20f);

                Set(serialized, "attackDamage", 6f);
                Set(serialized, "attackDamageType", (int)DamageType.Light);
                Set(serialized, "attackRange", 9f);
                Set(serialized, "attackWindup", 0.9f);
                Set(serialized, "attackCooldown", 2.5f);
                Set(serialized, "attackConeAngle", 0f);
                Set(serialized, "attackKnockback", 0f);

                Set(serialized, "corpseSeconds", 8f);
            });
        }

        private static GameObject BuildPuddle()
        {
            var root = new GameObject("VIEW_HZD_WaterPuddle");

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";

            Object.DestroyImmediate(visual.GetComponent<Collider>());

            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            visual.transform.localScale = new Vector3(2.8f, 0.004f, 2.8f);

            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = PuddleMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var zone = root.AddComponent<SlowZone>();

            Numbers(zone,
                ("radius", 1.4f), ("height", 1.2f), ("speedMultiplier", 0.55f),
                ("lifetime", 12f), ("spreadSeconds", 0.35f), ("drySeconds", 2f));

            EnemyContentBuilder.Wire(zone, ("visual", visual.transform));

            EnemyContentBuilder.EnsureFolder(EnemyContentBuilder.PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(root, PuddlePath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<GameObject>(PuddlePath);
        }

        private static GameObject OpenModel(string modelName, string viewName)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{modelName}.fbx");

            if (model == null)
            {
                Debug.LogError($"[Enemy] {ModelFolder}/{modelName}.fbx is missing. " +
                               $"'{viewName}' was not built.");
                return null;
            }

            var instance = Object.Instantiate(model);
            instance.name = viewName;

            // The Animator goes with it. Nothing here plays a clip, and an Animator left on the
            // view binds every bone to an animation stream that would fight the solver for them.
            var animator = instance.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);

            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Skinned bounds come from the bind pose, and these legs walk well outside it.
                // Widening them is far cheaper than recomputing per frame, and the failure it
                // prevents — the enemy vanishing when its root leaves the frustum — looks like a
                // rendering bug rather than a bounds one.
                renderer.updateWhenOffscreen = false;

                var bounds = renderer.localBounds;
                bounds.extents *= 1.5f;
                renderer.localBounds = bounds;
            }

            return instance;
        }

        private static GameObject SaveView(GameObject instance, string viewName)
        {
            var path = $"{EnemyContentBuilder.PrefabFolder}/{viewName}.prefab";

            EnemyContentBuilder.EnsureFolder(EnemyContentBuilder.PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void WireLegs(ProceduralWalker walker, GameObject root, Transform body,
            (string Tag, int Group)[] legs, bool hips, bool tarsus)
        {
            var serialized = new SerializedObject(walker);

            serialized.FindProperty("body").objectReferenceValue = body;

            var array = serialized.FindProperty("legs");
            array.arraySize = legs.Length;

            for (var i = 0; i < legs.Length; i++)
            {
                var (tag, group) = legs[i];
                var element = array.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("Hip").objectReferenceValue =
                    hips ? Bone(root, $"Leg_{tag}_Hip") : null;

                element.FindPropertyRelative("Thigh").objectReferenceValue =
                    Bone(root, $"Leg_{tag}_Thigh");

                element.FindPropertyRelative("Shin").objectReferenceValue =
                    Bone(root, $"Leg_{tag}_Shin");

                element.FindPropertyRelative("Tarsus").objectReferenceValue =
                    tarsus ? Bone(root, $"Leg_{tag}_Tarsus") : null;

                element.FindPropertyRelative("Foot").objectReferenceValue =
                    Bone(root, $"Leg_{tag}_Foot");

                element.FindPropertyRelative("Target").objectReferenceValue =
                    Bone(root, $"IK_Leg_{tag}");

                element.FindPropertyRelative("Pole").objectReferenceValue =
                    Bone(root, $"Pole_Leg_{tag}");

                element.FindPropertyRelative("Group").intValue = group;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ParticleSystem BuildGust(Transform parent)
        {
            var system = NewSystem("Gust", parent,
                ParticleMaterial("VFX_Wind", new Color(0.82f, 0.88f, 0.95f, 0.35f), additive: true));

            var main = system.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 18f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.maxParticles = 160;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

            var shape = system.shape;
            shape.angle = 20f;
            shape.radius = 0.35f;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 3f;

            return system;
        }

        private static ParticleSystem BuildDraft(Transform parent)
        {
            var system = NewSystem("Draft", parent,
                ParticleMaterial("VFX_Wind", new Color(0.82f, 0.88f, 0.95f, 0.35f), additive: true));

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.6f;
            main.startSpeed = 5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.maxParticles = 60;

            var emission = system.emission;
            emission.rateOverTime = 14f;

            var shape = system.shape;
            shape.angle = 26f;
            shape.radius = 0.3f;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2f;

            return system;
        }

        private static ParticleSystem BuildJet(Transform parent)
        {
            var system = NewSystem("Jet", parent,
                ParticleMaterial("VFX_Water", new Color(0.55f, 0.8f, 1f, 0.85f), additive: false));

            var main = system.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.gravityModifier = 0.45f;
            main.maxParticles = 300;

            var emission = system.emission;
            emission.rateOverTime = 220f;

            var shape = system.shape;
            shape.angle = 4f;
            shape.radius = 0.04f;

            // The jet breaking against the floor is what sells the puddle it leaves behind.
            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.dampen = 0.7f;
            collision.bounce = 0.15f;
            collision.lifetimeLoss = 0.5f;
            collision.collidesWith = PhysicsLayers.WalkableMask;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2.5f;

            return system;
        }

        private static ParticleSystem BuildDrip(Transform parent)
        {
            var system = NewSystem("Drip", parent,
                ParticleMaterial("VFX_Water", new Color(0.55f, 0.8f, 1f, 0.85f), additive: false));

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.gravityModifier = 1f;
            main.maxParticles = 40;

            var emission = system.emission;
            emission.enabled = false;
            emission.rateOverTime = 25f;

            var shape = system.shape;
            shape.angle = 12f;
            shape.radius = 0.03f;

            return system;
        }

        private static ParticleSystem NewSystem(string name, Transform parent, Material material)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);

            var system = host.AddComponent<ParticleSystem>();

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;

            // World space, or a blast fired from a body that keeps walking drags its own
            // particles along behind it.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;

            var emission = system.emission;
            emission.enabled = true;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Fade());

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return system;
        }

        private static Gradient Fade()
        {
            var gradient = new Gradient();

            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });

            return gradient;
        }

        private static Light BuildLamp(Transform lens)
        {
            var host = new GameObject("Lamp");
            host.transform.SetParent(lens, false);

            // The lens bone points along its own up axis, so the lamp is turned to match rather
            // than trusting the bone's forward — which faces the floor.
            host.transform.rotation = Quaternion.LookRotation(lens.up, Vector3.up);

            var lamp = host.AddComponent<Light>();

            lamp.type = LightType.Spot;
            lamp.range = 14f;
            lamp.spotAngle = 44f;
            lamp.innerSpotAngle = 16f;
            lamp.intensity = 4f;
            lamp.color = new Color(0.15f, 0.5f, 1f);
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = 0.85f;
            lamp.renderMode = LightRenderMode.ForcePixel;

            host.AddComponent<UniversalAdditionalLightData>();

            return lamp;
        }

        private static int LensMaterialIndex(Renderer renderer)
        {
            if (renderer == null) return 0;

            var materials = renderer.sharedMaterials;

            for (var i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].name.Contains("Eye"))
                    return i;

            Debug.LogWarning("[Enemy] The projector has no material with 'Eye' in its name — the " +
                             "lens will not change colour. Expected M_PJ_Eye_Lit.");

            return 0;
        }

        private static Material ParticleMaterial(string assetName, Color colour, bool additive)
        {
            var path = $"{VfxFolder}/M_{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnemyContentBuilder.EnsureFolder(VfxFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
            {
                color = colour,
                renderQueue = (int)RenderQueue.Transparent
            };

            material.SetTexture("_BaseMap",
                AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (int)BlendMode.One
                : (int)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);

            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        private static Material PuddleMaterial()
        {
            var path = $"{VfxFolder}/M_HZD_WaterPuddle.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnemyContentBuilder.EnsureFolder(VfxFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.08f, 0.16f, 0.22f, 0.55f),
                renderQueue = (int)RenderQueue.Transparent
            };

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.95f);
            material.SetFloat("_Metallic", 0.1f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);

            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        private static Transform Bone(GameObject root, string boneName)
        {
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == boneName)
                    return candidate;

            Debug.LogError($"[Enemy] '{root.name}' has no bone named '{boneName}'. The rig and " +
                           "this builder have drifted apart — check the model's README.");

            return null;
        }

        private static void WriteDefinition(string assetName, GameObject view,
            System.Action<SerializedObject> write)
        {
            var definition = EnemyContentBuilder.CreateOrLoad<EnemyDefinition>(
                $"{EnemyContentBuilder.DefinitionFolder}/{assetName}.asset");

            var serialized = new SerializedObject(definition);

            serialized.FindProperty("viewPrefab").objectReferenceValue = view;
            write(serialized);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void Responses(SerializedObject serialized,
            params (DamageType Type, float Multiplier)[] rows)
        {
            var table = serialized.FindProperty("responses").FindPropertyRelative("responses");

            table.arraySize = rows.Length;

            for (var i = 0; i < rows.Length; i++)
            {
                var element = table.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("Type").intValue = (int)rows[i].Type;
                element.FindPropertyRelative("Multiplier").floatValue = rows[i].Multiplier;
            }
        }

        private static void Set(SerializedObject serialized, string field, float value) =>
            Property(serialized, field).floatValue = value;

        private static void Set(SerializedObject serialized, string field, int value) =>
            Property(serialized, field).intValue = value;

        private static void Set(SerializedObject serialized, string field, string value) =>
            Property(serialized, field).stringValue = value;

        private static SerializedProperty Property(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);

            if (property == null)
                Debug.LogError($"[Enemy] '{serialized.targetObject.GetType().Name}' has no field " +
                               $"'{field}'. The builder and the type have drifted apart.");

            return property;
        }

        private static void Numbers(Object target, params (string Field, float Value)[] values)
        {
            var serialized = new SerializedObject(target);

            foreach (var (field, value) in values)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Enemy] '{target.GetType().Name}' has no field '{field}'.");
                    continue;
                }

                property.floatValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Integer(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[Enemy] '{target.GetType().Name}' has no field '{field}'.");
                return;
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
