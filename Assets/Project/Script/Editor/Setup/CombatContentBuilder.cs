using System.IO;
using Office.Data;
using Office.Gameplay;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Office.Editor
{
    /// <summary>
    /// Content pipeline for combat: the module assets that turn an item into a weapon, the
    /// target carrier and its definitions, and the greybox impact effects.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ItemContentBuilder"/> along the line the data itself draws.
    /// That file authors what an item <em>is</em>; this one authors what it <em>does</em>, and
    /// what it can be done to. Both write assets and neither writes code, which is the whole
    /// claim the module system makes.
    /// <para>
    /// Everything here is greybox and meant to be replaced: the meshes are primitives and the
    /// effects have no audio. What is not throwaway is the shape — a weapon is modules on a
    /// definition, a target is a definition plus one shared network prefab, and neither ever
    /// needs a netcode change.
    /// </para>
    /// </remarks>
    internal static class CombatContentBuilder
    {
        private const string ItemFolder = "Assets/Project/ScriptableObject/Items";
        private const string ModuleFolder = "Assets/Project/ScriptableObject/Weapons";
        private const string TargetFolder = "Assets/Project/ScriptableObject/Props";

        private const string PrefabFolder = "Assets/Project/Prefab";
        private const string TargetPrefabPath = PrefabFolder + "/Enemies/PF_Target.prefab";
        private const string TargetViewFolder = PrefabFolder + "/Props";
        private const string EffectFolder = PrefabFolder + "/Weapons";

        private const string MaterialFolder = "Assets/Project/Art/Materials/Items";

        private const string FlashlightOpticsPath = ModuleFolder + "/MOD_Light_Flashlight.asset";

        [MenuItem("Office/Content/Build Combat Content", priority = 15)]
        public static void BuildAll()
        {
            BuildWeaponModules();
            BuildFlashlightOptics();
            BuildTargetDefinitions();
            BuildTargetPrefab();
            BuildImpactEffects();

            AssetDatabase.SaveAssets();

            Debug.Log("[Combat] Combat content built. Run 'Rebuild Definition Registry' next, " +
                      "then rebuild the player, session and sandbox.");
        }

        // ------------------------------------------------------------------- weapons

        /// <summary>
        /// Turns the greybox items into the four weapons the MVP needs, purely by attaching
        /// modules.
        /// </summary>
        /// <remarks>
        /// The laser pointer is the important one. It is the only <see cref="DamageType.Light"/>
        /// source in the build, which makes it the only thing that can hurt a digital target —
        /// GDD §9.2's rule stops being a table in a test and becomes something a player can
        /// discover the moment both exist.
        /// </remarks>
        private static void BuildWeaponModules()
        {
            // Blunt, cheap, and it never breaks: the weapon a player falls back to. Costs
            // stamina, because swinging a full mug at something is work.
            var mugMelee = Melee("MOD_Melee_Mug", 8f, DamageType.Blunt, 2.0f, 0.55f, 6f, 8f, 0);
            Attach("ITM_CoffeeCup", mugMelee);

            // Fired, not swung — so no stamina, by construction. Wears out, which is what
            // makes finding another one matter.
            var staplerShot = Ranged("MOD_Ranged_Stapler", 16f, DamageType.Cutting, 18f,
                0.35f, 20f, 1);
            var staplerWear = Durability("MOD_Durability_Stapler", 40, null);
            Attach("ITM_Stapler", staplerShot, staplerWear);

            // Weak on contact, but the beam is the point. Melee because it is pressed against
            // the thing it hurts; batteries run out fast on purpose, so the answer to a digital
            // enemy stays a resource rather than a default.
            var laser = BuildLaserPointer();
            var laserMelee = Melee("MOD_Melee_LaserPointer", 6f,
                DamageType.Blunt | DamageType.Light, 2.6f, 0.4f, 3f, 2f, 1);
            var laserWear = Durability("MOD_Durability_LaserPointer", 25, null);

            if (laser != null) Attach("ITM_LaserPointer", laserMelee, laserWear);

            // The keycard stays a keycard. Swinging it falls through to the unarmed numbers,
            // which is the whole reason nothing asks "is this a weapon".
        }

        /// <summary>
        /// The optics for the light every player carries.
        /// </summary>
        /// <remarks>
        /// A <see cref="LightSourceModule"/> rather than fields on the player, so the built-in
        /// beam and a flashlight found on a desk are tuned in one asset. Narrow and not
        /// especially bright: GDD §14 wants the beam to be a window onto the room, not a
        /// replacement for the lights being on.
        /// </remarks>
        private static LightSourceModule BuildFlashlightOptics()
        {
            var module = CreateOrLoad<LightSourceModule>(FlashlightOpticsPath);
            var serialized = new SerializedObject(module);

            serialized.FindProperty("range").floatValue = 16f;
            serialized.FindProperty("angle").floatValue = 48f;
            serialized.FindProperty("intensity").floatValue = 3.2f;

            // Roughly seven minutes of continuous use out of a full charge. Long enough to
            // stop being a timer, short enough that leaving it on all run is a decision.
            serialized.FindProperty("drainPerSecond").floatValue = 0.25f;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            return module;
        }

        public static LightSourceModule LoadFlashlightOptics() =>
            AssetDatabase.LoadAssetAtPath<LightSourceModule>(FlashlightOpticsPath);

        private static ItemDefinition BuildLaserPointer()
        {
            var path = $"{ItemFolder}/ITM_LaserPointer.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (existing != null) return existing;

            Debug.LogWarning("[Combat] ITM_LaserPointer does not exist yet. Run " +
                             "'Office/Content/Build Sample Items' first — it authors the asset " +
                             "and its view prefab.");
            return null;
        }

        private static MeleeModule Melee(string assetName, float damage, DamageType type,
            float range, float cooldown, float staminaCost, float noiseRadius, int durabilityCost)
        {
            var module = CreateOrLoad<MeleeModule>($"{ModuleFolder}/{assetName}.asset");
            var serialized = new SerializedObject(module);

            serialized.FindProperty("damage").floatValue = damage;
            serialized.FindProperty("damageType").intValue = (int)type;
            serialized.FindProperty("range").floatValue = range;
            serialized.FindProperty("attackCooldown").floatValue = cooldown;
            serialized.FindProperty("staminaCost").floatValue = staminaCost;
            serialized.FindProperty("noiseRadius").floatValue = noiseRadius;
            serialized.FindProperty("durabilityCost").intValue = durabilityCost;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            return module;
        }

        private static RangedModule Ranged(string assetName, float damage, DamageType type,
            float range, float cooldown, float noiseRadius, int durabilityCost)
        {
            var module = CreateOrLoad<RangedModule>($"{ModuleFolder}/{assetName}.asset");
            var serialized = new SerializedObject(module);

            serialized.FindProperty("damage").floatValue = damage;
            serialized.FindProperty("damageType").intValue = (int)type;
            serialized.FindProperty("range").floatValue = range;
            serialized.FindProperty("attackCooldown").floatValue = cooldown;
            serialized.FindProperty("noiseRadius").floatValue = noiseRadius;
            serialized.FindProperty("durabilityCost").intValue = durabilityCost;

            // No stamina field to write. That is the point of RangedModule.
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            return module;
        }

        private static DurabilityModule Durability(string assetName, int maxUses,
            ItemDefinition breaksInto)
        {
            var module = CreateOrLoad<DurabilityModule>($"{ModuleFolder}/{assetName}.asset");
            var serialized = new SerializedObject(module);

            serialized.FindProperty("maxUses").intValue = maxUses;
            serialized.FindProperty("breaksInto").objectReferenceValue = breaksInto;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            return module;
        }

        private static void Attach(string itemAssetName, params ItemModule[] modules)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                $"{ItemFolder}/{itemAssetName}.asset");

            if (definition == null)
            {
                Debug.LogWarning($"[Combat] '{itemAssetName}' not found — modules not attached.");
                return;
            }

            var serialized = new SerializedObject(definition);
            var array = serialized.FindProperty("modules");

            array.arraySize = modules.Length;

            for (var i = 0; i < modules.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = modules[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        // ------------------------------------------------------------------- targets

        /// <summary>
        /// The one network prefab every breakable thing shares, mirroring <c>PF_WorldItem</c>.
        /// </summary>
        [MenuItem("Office/Content/Build Target Prefab", priority = 25)]
        public static void BuildTargetPrefab()
        {
            var root = new GameObject("PF_Target") { layer = PhysicsLayers.Prop };

            var networkObject = root.AddComponent<NetworkObject>();

            // Same reasoning as the item carrier: NGO ships position and rotation in the spawn
            // payload while SynchronizeTransform is on, and a target does not move.
            networkObject.SynchronizeTransform = true;

            var health = root.AddComponent<Health>();
            var target = root.AddComponent<DamageableTarget>();

            var serialized = new SerializedObject(health);

            // Off: a target is not a player, so zero health means gone rather than downed.
            serialized.FindProperty("canBeDowned").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Wire(target, ("health", health));

            EnsureFolder(Path.GetDirectoryName(TargetPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, TargetPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            NetworkPrefabRegistry.Register(
                AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath));

            Debug.Log($"[Combat] Target carrier written to {TargetPrefabPath}.");
        }

        public static GameObject LoadTargetPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);

        /// <summary>
        /// Two targets that differ only in their response table — which is the point.
        /// </summary>
        private static void BuildTargetDefinitions()
        {
            // Ordinary matter. Everything hurts it, nothing hurts it especially.
            BuildTarget("TGT_FilingCabinet", "CABINET", 60f, 6f,
                new Vector3(0.7f, 1.5f, 0.5f), new Color(0.42f, 0.45f, 0.48f),
                new DamageResponse[0]);

            // The digital class, the shape every Glitch-family enemy will reuse. Physical
            // weapons do nothing at all; light tears it apart. Authored here as two rows so
            // that the rule can be changed by a designer and proved by DamageResponseTests.
            BuildTarget("TGT_Anomaly", "ANOMALY", 40f, 8f,
                new Vector3(0.6f, 1.7f, 0.6f), new Color(0.25f, 0.75f, 0.85f),
                new[]
                {
                    new DamageResponse(DamageType.Blunt | DamageType.Cutting, 0f),
                    new DamageResponse(DamageType.Light, 2.5f)
                });
        }

        private static void BuildTarget(string assetName, string displayName, float maxHealth,
            float respawnSeconds, Vector3 size, Color colour, DamageResponse[] responses)
        {
            var view = BuildTargetView(assetName, size, colour);

            var definition = CreateOrLoad<TargetDefinition>($"{TargetFolder}/{assetName}.asset");
            var serialized = new SerializedObject(definition);

            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("viewPrefab").objectReferenceValue = view;
            serialized.FindProperty("maxHealth").floatValue = maxHealth;
            serialized.FindProperty("respawnSeconds").floatValue = respawnSeconds;

            var array = serialized.FindProperty("responses").FindPropertyRelative("responses");
            array.arraySize = responses.Length;

            for (var i = 0; i < responses.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Type").intValue = (int)responses[i].Type;
                element.FindPropertyRelative("Multiplier").floatValue = responses[i].Multiplier;
            }

            // 'id' is deliberately untouched: only the registry rebuild hands ids out.
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        // Pivot at the base, so a marker on the floor puts the target on the floor rather than
        // half through it.
        private static GameObject BuildTargetView(string assetName, Vector3 size, Color colour)
        {
            var path = $"{TargetViewFolder}/VIEW_{assetName}.prefab";

            var root = new GameObject($"VIEW_{assetName}") { layer = PhysicsLayers.Prop };

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Mesh";
            box.layer = PhysicsLayers.Prop;
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial =
                CreateOrLoadMaterial($"M_Target_{assetName}", colour);

            EnsureFolder(TargetViewFolder);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return saved;
        }

        // ------------------------------------------------------------------- effects

        /// <summary>
        /// Three effects, because the server reports three outcomes and a player who cannot
        /// tell them apart cannot learn the physical/digital rule.
        /// </summary>
        private static void BuildImpactEffects()
        {
            BuildImpactEffect("PF_FX_Impact_Hit", new Color(1f, 0.85f, 0.35f), 24, 3.2f, 0.09f);

            // Cold, sparse and slow: contact without harm. It must not read as a good hit.
            BuildImpactEffect("PF_FX_Impact_Absorbed", new Color(0.35f, 0.85f, 1f), 10, 1.1f, 0.16f);

            BuildImpactEffect("PF_FX_Impact_Miss", new Color(0.7f, 0.7f, 0.7f), 4, 0.9f, 0.2f);
        }

        private static void BuildImpactEffect(string assetName, Color colour, int burst,
            float speed, float size)
        {
            var path = $"{EffectFolder}/{assetName}.prefab";

            var root = new GameObject(assetName);

            var particles = root.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.35f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = colour;
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.05f;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = CreateOrLoadUnlitMaterial($"M_FX_{assetName}", colour);

            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;

            // Fully 3D, so a fight two rooms away is heard from two rooms away. GDD §14 leans
            // on audio for information the darkness withholds.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 22f;

            var effect = root.AddComponent<ImpactEffect>();
            Wire(effect, ("particles", particles), ("source", source));

            EnsureFolder(EffectFolder);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        public static ImpactEffect LoadImpactEffect(string assetName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EffectFolder}/{assetName}.prefab");
            return prefab != null ? prefab.GetComponent<ImpactEffect>() : null;
        }

        // ------------------------------------------------------------------- helpers

        private static void Wire(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach (var (field, value) in fields)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Combat] {target.GetType().Name} has no field '{field}'.");
                    continue;
                }

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static Material CreateOrLoadMaterial(string assetName, Color colour)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = colour
            };
            material.SetFloat("_Smoothness", 0.1f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Unlit, additive-ish: sparks should not be swallowed by an unpowered room, which is
        // where most of this game happens.
        private static Material CreateOrLoadUnlitMaterial(string assetName, Color colour)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
            {
                color = colour
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
