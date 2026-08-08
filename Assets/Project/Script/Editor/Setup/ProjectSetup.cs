using System.IO;
using Office.Core;
using Office.Data;
using Office.Gameplay;
using Office.Network;
using Office.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Editor
{
    public static class ProjectSetup
    {
        private const string ConfigFolder = "Assets/Project/ScriptableObject/Config";
        private const string PlayerPrefabPath = "Assets/Project/Prefab/Player/PF_Player.prefab";
        private const string SessionPrefabPath = "Assets/Project/Prefab/Systems/PF_Session.prefab";
        private const string MaterialFolder = "Assets/Project/Art/Materials";

        private const string PlayerBodyMaterialPath =
            MaterialFolder + "/Charachters/MAT_Cylinder.mat";
        private const string ScenesFolder = "Assets/Project/Scenes";

        // Authored levels live here and nothing regenerates them. Build settings pick up
        // whatever is in this folder, so adding a level never means editing this file.
        private const string LevelsFolder = ScenesFolder + "/Level";

        private const string MovementConfigPath = ConfigFolder + "/CFG_PlayerMovement.asset";
        private const string LookConfigPath = ConfigFolder + "/CFG_PlayerLook.asset";
        private const string InteractionConfigPath = ConfigFolder + "/CFG_Interaction.asset";
        private const string CombatConfigPath = ConfigFolder + "/CFG_Combat.asset";

        // Right hand height, slightly forward. Authored by hand in the prefab first; moving
        // it means editing this line, not the asset, or the next rebuild reverts it.
        private static readonly Vector3 SocketLocalPosition = new(0.256f, 1.251f, 0.437f);

        private static string BootScenePath => $"{ScenesFolder}/{SceneNames.Boot}.unity";
        private static string SandboxScenePath => $"{ScenesFolder}/{SceneNames.Sandbox}.unity";
        private static string LobbyScenePath => $"{ScenesFolder}/{SceneNames.Lobby}.unity";
        private static string MainMenuScenePath => $"{ScenesFolder}/{SceneNames.MainMenu}.unity";

        [MenuItem("Office/Setup/Run All (physics, configs, prefab, scenes)", priority = 0)]
        public static void RunAll()
        {
            if (!EnsureNoUnsavedScene()) return;

            ConfigureCollisionMatrix();
            CreateConfigAssets();

            // Content first: the session prefab needs the item carrier, and the boot scene
            // needs the registry. Both are silent nulls if they are built afterwards.
            ItemContentBuilder.BuildAll();

            BuildPlayerPrefab();
            BuildSessionPrefab();
            BuildSandboxScene();
            BuildLobbyScene();
            BuildMainMenuScene();
            BuildBootScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Setup] Done. Open SCN_Boot and press Play, then Host in the F1 panel.");
        }

        [MenuItem("Office/Setup/Configure Collision Matrix", priority = 20)]
        public static void ConfigureCollisionMatrix()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");

            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[Setup] Could not open DynamicsManager.asset.");
                return;
            }

            var masks = new uint[32];
            for (var i = 0; i < masks.Length; i++) masks[i] = uint.MaxValue;

            DisableAll(masks, PhysicsLayers.ViewModel);

            DisableAll(masks, PhysicsLayers.VoiceEmitter);

            Disable(masks, PhysicsLayers.Projectile, PhysicsLayers.Projectile);

            Disable(masks, PhysicsLayers.Player, PhysicsLayers.Player);

            // A stapler on the floor must not shove a running player off course. Queries take
            // a layer mask and ignore this matrix entirely, so the interaction probe still
            // finds these colliders. Anything that should physically block — a closed door —
            // puts its blocking collider on LevelGeometry and keeps only its interaction
            // collider here.
            Disable(masks, PhysicsLayers.Player, PhysicsLayers.Interactable);

            var serialized = new SerializedObject(assets[0]);
            var matrix = serialized.FindProperty("m_LayerCollisionMatrix");

            if (matrix == null || !matrix.isArray)
            {
                Debug.LogError("[Setup] m_LayerCollisionMatrix not found — Unity changed the format.");
                return;
            }

            for (var i = 0; i < matrix.arraySize && i < masks.Length; i++)
                matrix.GetArrayElementAtIndex(i).uintValue = masks[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Debug.Log("[Setup] Collision matrix configured.");
        }

        private static void Disable(uint[] masks, int a, int b)
        {
            masks[a] &= ~(1u << b);
            masks[b] &= ~(1u << a);
        }

        private static void DisableAll(uint[] masks, int layer)
        {
            for (var other = 0; other < masks.Length; other++) Disable(masks, layer, other);
        }

        [MenuItem("Office/Setup/Create Config Assets", priority = 21)]
        public static void CreateConfigAssets()
        {
            var movement = CreateOrLoad<PlayerMovementConfig>(MovementConfigPath);

            var serialized = new SerializedObject(movement);
            serialized.FindProperty("canJump").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CreateOrLoad<PlayerLookConfig>(LookConfigPath);
            CreateOrLoad<InteractionConfig>(InteractionConfigPath);
            CreateOrLoad<CombatConfig>(CombatConfigPath);

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Config assets ready.");
        }

        [MenuItem("Office/Setup/Build Player Prefab", priority = 22)]
        public static void BuildPlayerPrefab()
        {
            var movementConfig = CreateOrLoad<PlayerMovementConfig>(MovementConfigPath);
            var lookConfig = CreateOrLoad<PlayerLookConfig>(LookConfigPath);
            var interactionConfig = CreateOrLoad<InteractionConfig>(InteractionConfigPath);
            var combatConfig = CreateOrLoad<CombatConfig>(CombatConfigPath);

            var root = new GameObject("PF_Player") { layer = PhysicsLayers.Player };

            var controller = root.AddComponent<CharacterController>();
            controller.slopeLimit = 46f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            controller.radius = 0.32f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<NetworkObject>();

            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.Interpolate = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            var body = BuildGreyboxBody(root.transform);

            // Where a carried item hangs. Authored by hand first, kept here so regenerating
            // the prefab no longer wipes it — a rig point is exactly the kind of thing that
            // must survive a rebuild. Under the body rather than the camera: the item has to
            // sit in one place for the holder and for everyone watching them.
            var socket = new GameObject("Socket") { layer = PhysicsLayers.Player };
            socket.transform.SetParent(root.transform, false);
            socket.transform.localPosition = SocketLocalPosition;

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            var cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(pivot.transform, false);

            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 120f;
            camera.fieldOfView = 68f;
            PostProcessBuilder.EnablePostProcessing(camera);

            var listener = cameraObject.AddComponent<AudioListener>();

            var input = root.AddComponent<PlayerInputReader>();
            input.enabled = false;

            var movement = root.AddComponent<PlayerMovement>();
            var look = root.AddComponent<PlayerLook>();
            var rig = root.AddComponent<PlayerRig>();
            root.AddComponent<PlayerSpawnAnchor>();

            var interactor = root.AddComponent<PlayerInteractor>();
            var inventory = root.AddComponent<PlayerInventory>();
            var heldItem = root.AddComponent<HeldItemView>();
            var health = root.AddComponent<Health>();
            var attacker = root.AddComponent<PlayerAttacker>();

            Wire(movement, ("config", movementConfig), ("input", input));

            // Health needs no wiring: its defaults are already the player's numbers, and it
            // keeps an empty response table on purpose — a player takes damage as authored,
            // and resistances are an enemy and prop concern.

            Wire(attacker,
                ("config", combatConfig),
                ("input", input),
                ("inventory", inventory),
                ("movement", movement),
                ("health", health),
                ("playerCamera", camera));

            Wire(interactor,
                ("config", interactionConfig),
                ("input", input),
                ("playerCamera", camera));

            Wire(inventory,
                ("config", interactionConfig),
                ("input", input));

            Wire(heldItem,
                ("inventory", inventory),
                ("socket", socket.transform));

            Wire(look,
                ("config", lookConfig),
                ("input", input),
                ("movement", movement),
                ("cameraPivot", pivot.transform),
                ("playerCamera", camera));

            Wire(rig,
                ("playerCamera", camera),
                ("audioListener", listener),
                ("inputReader", input),
                ("characterController", controller));

            WireArray(rig, "bodyRenderers", body);

            EnsureFolder(Path.GetDirectoryName(PlayerPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Setup] Player prefab written to {PlayerPrefabPath}.");
        }

        private static Renderer[] BuildGreyboxBody(Transform parent)
        {
            var accent = CreateOrLoadMaterial("M_Greybox_Accent", new Color(0.78f, 0.29f, 0.22f));

            // MAT_Cylinder is the look the player ships with today. Regenerating the prefab
            // must not silently replace it with a fresh grey material.
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerBodyMaterialPath)
                               ?? CreateOrLoadMaterial("M_Greybox_Player",
                                   new Color(0.62f, 0.66f, 0.72f));

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            capsule.layer = PhysicsLayers.Player;
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(parent, false);
            capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsule.transform.localScale = new Vector3(0.64f, 0.9f, 0.64f);
            capsule.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            nose.layer = PhysicsLayers.Player;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(parent, false);
            nose.transform.localPosition = new Vector3(0f, 1.58f, 0.3f);
            nose.transform.localScale = new Vector3(0.16f, 0.1f, 0.16f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = accent;

            return new Renderer[]
            {
                capsule.GetComponent<MeshRenderer>(), nose.GetComponent<MeshRenderer>()
            };
        }

        [MenuItem("Office/Setup/Build Sandbox Scene", priority = 40)]
        public static void BuildSandboxScene()
        {
            if (!EnsureNoUnsavedScene()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            GreyboxSandbox.Build();
            BuildSpawnPoints();
            BuildFallbackCamera(new Vector3(0f, 9f, -11f), new Vector3(32f, 0f, 0f));
            PostProcessBuilder.BuildVolume();
            HudBuilder.Build();
            PauseMenuBuilder.Build();

            SaveScene(scene, SandboxScenePath);
            Debug.Log($"[Setup] {SandboxScenePath} built.");
        }

        [MenuItem("Office/Setup/Import TextMeshPro Essentials", priority = 10)]
        public static void ImportTextMeshProEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.Log("[Setup] TextMeshPro essentials are already imported.");
                return;
            }

            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.Log("[Setup] TextMeshPro essentials imported.");
        }

        [MenuItem("Office/Setup/Build Lobby Scene", priority = 42)]
        public static void BuildLobbyScene()
        {
            if (!EnsureNoUnsavedScene()) return;

            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.LogError("[Setup] Run 'Office/Setup/Import TextMeshPro Essentials' first.");
                return;
            }

            LobbyUIBuilder.BuildRowPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            LobbyUIBuilder.Build(LobbyUIBuilder.LoadRowPrefab());

            SaveScene(scene, LobbyScenePath);
            Debug.Log($"[Setup] {LobbyScenePath} built.");
        }

        [MenuItem("Office/Setup/Build Main Menu Scene", priority = 44)]
        public static void BuildMainMenuScene()
        {
            if (!EnsureNoUnsavedScene()) return;

            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.LogError("[Setup] Run 'Office/Setup/Import TextMeshPro Essentials' first.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            MainMenuBuilder.Build();

            SaveScene(scene, MainMenuScenePath);
            Debug.Log($"[Setup] {MainMenuScenePath} built.");
        }

        [MenuItem("Office/Setup/Build Boot Scene", priority = 41)]
        public static void BuildBootScene()
        {
            if (!EnsureNoUnsavedScene()) return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogError("[Setup] Build the player prefab before the Boot scene.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var networkObject = new GameObject("NetworkManager");
            var networkManager = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();

            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;

            networkManager.NetworkConfig.PlayerPrefab = null;

            networkManager.NetworkConfig.EnableSceneManagement = false;

            // NetworkServiceInstaller supplies the callback and the payload at boot. Writing
            // it here too keeps the inspector honest about what the scene actually does.
            networkManager.NetworkConfig.ConnectionApproval = true;

            var bootstrapObject = new GameObject("[Bootstrap]");
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            var uiInstaller = bootstrapObject.AddComponent<UIEventSystemInstaller>();
            var networkInstaller = bootstrapObject.AddComponent<NetworkServiceInstaller>();

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("firstScene").stringValue = SceneNames.MainMenu;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var registry = ItemContentBuilder.LoadRegistry();

            if (registry == null)
                Debug.LogError("[Setup] REG_Definitions is missing. Run " +
                               "'Office/Content/Rebuild Definition Registry' first — without it " +
                               "no item or prop resolves at runtime.");

            Wire(bootstrap, ("definitions", registry));

            WireArray(bootstrap, "installers", uiInstaller, networkInstaller);

            var sessionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);

            if (sessionPrefab == null)
                Debug.LogError("[Setup] Build the session prefab before the Boot scene.");

            Wire(networkInstaller,
                ("networkManager", networkManager),
                ("sessionPrefab", sessionPrefab));

            // The item carrier is the first thing through the pool on purpose: it is already
            // the one prefab every item shares, so a run that drops and picks things up
            // exercises the pool constantly. Prewarm covers a typical floor's worth of loose
            // items without a spawn storm on the InRun edge.
            WirePooledPrefabs(networkInstaller,
                (ItemContentBuilder.LoadWorldItemPrefab(), 24));

            var uiObject = new GameObject("[DevUI]");
            uiObject.AddComponent<DevSessionPanel>();

            SaveScene(scene, BootScenePath);
            Debug.Log($"[Setup] {BootScenePath} built.");
        }

        [MenuItem("Office/Setup/Build Session Prefab", priority = 23)]
        public static void BuildSessionPrefab()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (playerPrefab == null)
            {
                Debug.LogError("[Setup] Build the player prefab before the session prefab.");
                return;
            }

            var root = new GameObject("PF_Session");
            root.AddComponent<NetworkObject>();
            root.AddComponent<SessionRoot>();

            var roster = root.AddComponent<LobbyRoster>();
            var director = root.AddComponent<SessionDirector>();
            var spawner = root.AddComponent<PlayerSpawner>();
            var sceneFlow = root.AddComponent<RunSceneFlow>();
            var itemSpawner = root.AddComponent<WorldItemSpawner>();

            Wire(director, ("roster", roster));

            // Every seat spawns the greybox capsule for now. The animated character
            // variants exist but are opt-in through Office/Setup/Player Prefab.
            Wire(spawner, ("director", director), ("manPrefab", playerPrefab));
            ClearFields(spawner, "womanPrefab");

            Wire(sceneFlow, ("director", director));

            var worldItemPrefab = ItemContentBuilder.LoadWorldItemPrefab();

            if (worldItemPrefab == null)
                Debug.LogError("[Setup] PF_WorldItem is missing. Run " +
                               "'Office/Content/Build World Item Prefab' first — without it " +
                               "nothing can be picked up or dropped.");

            Wire(itemSpawner, ("director", director), ("worldItemPrefab", worldItemPrefab));

            EnsureFolder(Path.GetDirectoryName(SessionPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            NetworkPrefabRegistry.Register(
                AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath), playerPrefab);

            Debug.Log($"[Setup] Session prefab written to {SessionPrefabPath}.");
        }

        private static void BuildLighting()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(48f, 34f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.color = new Color(0.92f, 0.94f, 1f);
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.26f, 0.3f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.18f, 0.2f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.11f);
        }

        private static void BuildSpawnPoints()
        {
            var root = new GameObject("SpawnPoints");
            var component = root.AddComponent<PlayerSpawnPoints>();

            var offsets = new[]
            {
                new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f),
                new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f)
            };

            var transforms = new Object[offsets.Length];

            for (var i = 0; i < offsets.Length; i++)
            {
                var point = new GameObject($"Spawn_{i + 1}");
                point.transform.SetParent(root.transform, false);
                point.transform.position = offsets[i];
                point.transform.rotation = Quaternion.LookRotation(-offsets[i].normalized, Vector3.up);
                transforms[i] = point.transform;
            }

            WireArray(component, "points", transforms);
        }

        private static void BuildFallbackCamera(Vector3 position, Vector3 eulerAngles)
        {
            var cameraObject = new GameObject("FallbackCamera") { tag = "MainCamera" };
            cameraObject.transform.SetPositionAndRotation(position, Quaternion.Euler(eulerAngles));

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.farClipPlane = 200f;
            PostProcessBuilder.EnablePostProcessing(camera);

            var listener = cameraObject.AddComponent<AudioListener>();
            var fallback = cameraObject.AddComponent<FallbackCamera>();

            Wire(fallback, ("fallbackListener", listener));
        }

        [MenuItem("Office/Setup/Configure Build Settings", priority = 60)]
        public static void ConfigureBuildSettings()
        {
            var paths = new[]
            {
                BootScenePath,
                MainMenuScenePath,
                LobbyScenePath,
                SandboxScenePath
            };

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            // This command replaces the whole list, so authored levels have to be re-discovered
            // rather than remembered — otherwise every Run All would silently drop them and the
            // build would ship with the greybox as the only level.
            var levels = 0;

            if (AssetDatabase.IsValidFolder(LevelsFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { LevelsFolder }))
                {
                    scenes.Add(new EditorBuildSettingsScene(AssetDatabase.GUIDToAssetPath(guid), true));
                    levels++;
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Setup] Build settings: {scenes.Count} scenes ({levels} authored), " +
                      "SCN_Boot at index 0.");
        }

        private static bool EnsureNoUnsavedScene()
        {
            var active = SceneManager.GetActiveScene();

            if (!active.isDirty) return true;
            if (IsGeneratedScene(active.path)) return true;

            Debug.LogError($"[Setup] '{active.name}' has unsaved changes. Save or discard it " +
                           "first — this command replaces the open scene.");
            return false;
        }

        private static bool IsGeneratedScene(string path) =>
            path == BootScenePath || path == LobbyScenePath || path == SandboxScenePath ||
            path == MainMenuScenePath;

        private static void SaveScene(Scene scene, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            ClearReadOnly(path);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void ClearReadOnly(string path)
        {
            if (!File.Exists(path)) return;

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
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

        private static Material CreateOrLoadMaterial(string assetName, Color color)
        {
            var path = $"{MaterialFolder}/{assetName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(MaterialFolder);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", 0.08f);

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

        private static void Wire(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach (var (field, value) in fields)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Setup] '{target.GetType().Name}' has no field '{field}'. " +
                                   "The setup script and the component have drifted apart.");
                    continue;
                }

                if (value == null)
                    Debug.LogError($"[Setup] '{target.GetType().Name}.{field}' was given null.");

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Deliberate nulls. Wire treats a null value as a mistake, which it almost always
        // is — clearing a field has to say so out loud.
        private static void ClearFields(Object target, params string[] fields)
        {
            var serialized = new SerializedObject(target);

            foreach (var field in fields)
            {
                var property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError($"[Setup] '{target.GetType().Name}' has no field '{field}'.");
                    continue;
                }

                property.objectReferenceValue = null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // The pooled list is an array of a struct, so WireArray cannot reach it — that one
        // only knows how to assign object references.
        private static void WirePooledPrefabs(Object target,
            params (GameObject Prefab, int Prewarm)[] entries)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty("pooledPrefabs");

            if (property == null || !property.isArray)
            {
                Debug.LogError($"[Setup] '{target.GetType().Name}.pooledPrefabs' is not a " +
                               "serialised array.");
                return;
            }

            property.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);

                if (entries[i].Prefab == null)
                    Debug.LogError("[Setup] A pooled prefab entry was given null. Nothing will " +
                                   "be pooled for it.");

                element.FindPropertyRelative("Prefab").objectReferenceValue = entries[i].Prefab;
                element.FindPropertyRelative("Prewarm").intValue = entries[i].Prewarm;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);

            if (property == null || !property.isArray)
            {
                Debug.LogError($"[Setup] '{target.GetType().Name}.{field}' is not a serialised array.");
                return;
            }

            property.arraySize = values.Length;

            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
