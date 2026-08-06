using System.Collections.Generic;
using Office.Gameplay;
using Office.Network;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Office.Editor
{
    /// <summary>
    /// Builds the animated character player prefabs: fills the man/woman animator
    /// controllers, creates PF_Player_Man / PF_Player_Woman variants with the
    /// rigged models, and wires the PlayerSpawner and network prefab list.
    /// </summary>
    internal static class CharacterPlayerBuilder
    {
        private const string AnimFolder = "Assets/Project/Art/Animations/Charachters";
        private const string ModelFolder = "Assets/Project/Art/Models/Charachters";

        private const string BasePlayerPath = "Assets/Project/Prefab/Player/PF_Player.prefab";
        private const string ManVariantPath = "Assets/Project/Prefab/Player/PF_Player_Man.prefab";
        private const string WomanVariantPath = "Assets/Project/Prefab/Player/PF_Player_Woman.prefab";
        private const string SessionPrefabPath = "Assets/Project/Prefab/Systems/PF_Session.prefab";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        // Walk speed / sprint speed from CFG_PlayerMovement (3.2 / 5.6): the blend
        // position where the walk cycle sits on the normalized velocity axes.
        private const float WalkRatio = 0.571f;

        [MenuItem("Office/Setup/Build Character Players", priority = 46)]
        public static void Build()
        {
            BuildController($"{AnimFolder}/Charachter_Man.controller");
            BuildController($"{AnimFolder}/Charachter_Woman.controller");

            var man = BuildVariant(
                $"{ModelFolder}/Man.fbx",
                $"{AnimFolder}/Charachter_Man.controller",
                ManVariantPath);

            var woman = BuildVariant(
                $"{ModelFolder}/Woman.fbx",
                $"{AnimFolder}/Charachter_Woman.controller",
                WomanVariantPath);

            WireSpawner(man, woman);
            RegisterNetworkPrefabs(man, woman);

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Character players built: controllers, variants, spawner, network prefabs.");
        }

        // ----------------------------------------------------------------- animator

        private static void BuildController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller == null)
            {
                Debug.LogError($"[Setup] Animator controller not found at {path}.");
                return;
            }

            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Float);

            var parameters = controller.parameters;
            for (var i = 0; i < parameters.Length; i++)
                if (parameters[i].name == "Grounded")
                    parameters[i].defaultBool = true;
            controller.parameters = parameters;

            var stateMachine = controller.layers[0].stateMachine;

            foreach (var child in stateMachine.states)
                stateMachine.RemoveState(child.state);

            // Re-runs leave the previous blend tree behind as an orphaned sub-asset.
            foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (subAsset is BlendTree stale)
                    Object.DestroyImmediate(stale, true);

            var locomotion = BuildLocomotionState(controller, stateMachine);
            var jump = BuildJumpState(stateMachine);

            var toJump = locomotion.AddTransition(jump);
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;

            var toLocomotion = jump.AddTransition(locomotion);
            toLocomotion.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            toLocomotion.hasExitTime = false;
            toLocomotion.duration = 0.15f;

            stateMachine.defaultState = locomotion;
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorState BuildLocomotionState(
            AnimatorController controller, AnimatorStateMachine stateMachine)
        {
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                hideFlags = HideFlags.HideInHierarchy
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.AddChild(Clip("Idle"), Vector2.zero);
            tree.AddChild(Clip("Walking"), new Vector2(0f, WalkRatio));
            tree.AddChild(Clip("Running"), new Vector2(0f, 1f));
            tree.AddChild(Clip("Left_Walking"), new Vector2(-WalkRatio, 0f));
            tree.AddChild(Clip("Right_Walking"), new Vector2(WalkRatio, 0f));
            tree.AddChild(Clip("Left_Strafe"), new Vector2(-1f, 0f));
            tree.AddChild(Clip("Right_Run"), new Vector2(1f, 0f));
            tree.AddChild(Clip("Walking"), new Vector2(0f, -WalkRatio));

            // The last child is the backpedal: the walk cycle played in reverse.
            var children = tree.children;
            children[children.Length - 1].timeScale = -1f;
            tree.children = children;

            var state = stateMachine.AddState("Locomotion");
            state.motion = tree;
            return state;
        }

        private static AnimatorState BuildJumpState(AnimatorStateMachine stateMachine)
        {
            var state = stateMachine.AddState("Jump");
            state.motion = Clip("Jump");
            return state;
        }

        private static AnimationClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{name}.anim");
            if (clip == null) Debug.LogError($"[Setup] Animation clip '{name}' not found.");
            return clip;
        }

        // ----------------------------------------------------------------- prefabs

        private static GameObject BuildVariant(string fbxPath, string controllerPath, string variantPath)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePlayerPath);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (basePrefab == null || fbx == null || controller == null)
            {
                Debug.LogError($"[Setup] Missing asset for variant {variantPath}.");
                return null;
            }

            // The shared clips are humanoid muscle clips, so they only retarget onto a
            // model imported with Rig > Animation Type = Humanoid.
            var avatar = LoadAvatar(fbxPath);

            if (avatar == null || !avatar.isHuman || !avatar.isValid)
            {
                Debug.LogError($"[Setup] {fbxPath} has no valid Humanoid avatar — set Rig > Animation Type to Humanoid.");
                return null;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);

            try
            {
                SetActive(root, "Body", false);
                SetActive(root, "FacingMarker", false);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                model.name = "Model";
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();

                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var networkAnimator = root.GetComponent<OwnerNetworkAnimator>();
                if (networkAnimator == null) networkAnimator = root.AddComponent<OwnerNetworkAnimator>();
                networkAnimator.Animator = animator;
                networkAnimator.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;

                var driver = root.GetComponent<PlayerAnimationDriver>();
                if (driver == null) driver = root.AddComponent<PlayerAnimationDriver>();

                var driverData = new SerializedObject(driver);
                driverData.FindProperty("movement").objectReferenceValue =
                    root.GetComponent<PlayerMovement>();
                driverData.FindProperty("animator").objectReferenceValue = animator;
                driverData.ApplyModifiedPropertiesWithoutUndo();

                var rig = root.GetComponent<PlayerRig>();
                var rigData = new SerializedObject(rig);
                var bodyRenderers = rigData.FindProperty("bodyRenderers");
                var renderers = model.GetComponentsInChildren<Renderer>(true);

                bodyRenderers.arraySize = renderers.Length;
                for (var i = 0; i < renderers.Length; i++)
                    bodyRenderers.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                rigData.ApplyModifiedPropertiesWithoutUndo();

                var variant = PrefabUtility.SaveAsPrefabAsset(root, variantPath);
                Debug.Log($"[Setup] Built player variant {variantPath}.");
                return variant;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The avatar is a sub-asset of the FBX, so it needs the full asset list.
        private static Avatar LoadAvatar(string fbxPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (asset is Avatar avatar)
                    return avatar;

            return null;
        }

        private static void SetActive(GameObject root, string childName, bool active)
        {
            var child = root.transform.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        // ----------------------------------------------------------------- wiring

        private static void WireSpawner(GameObject man, GameObject woman)
        {
            if (man == null || woman == null) return;

            using var scope = new PrefabUtility.EditPrefabContentsScope(SessionPrefabPath);
            var spawner = scope.prefabContentsRoot.GetComponentInChildren<PlayerSpawner>(true);

            if (spawner == null)
            {
                Debug.LogError("[Setup] PF_Session has no PlayerSpawner.");
                return;
            }

            var data = new SerializedObject(spawner);
            data.FindProperty("manPrefab").objectReferenceValue = man;
            data.FindProperty("womanPrefab").objectReferenceValue = woman;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterNetworkPrefabs(params GameObject[] prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);

            if (list == null)
            {
                Debug.LogError($"[Setup] Network prefabs list not found at {NetworkPrefabsPath}.");
                return;
            }

            var known = new HashSet<GameObject>();
            foreach (var entry in list.PrefabList)
                if (entry.Prefab != null)
                    known.Add(entry.Prefab);

            foreach (var prefab in prefabs)
            {
                if (prefab == null || known.Contains(prefab)) continue;
                list.Add(new NetworkPrefab { Prefab = prefab });
            }

            EditorUtility.SetDirty(list);
        }
    }
}
