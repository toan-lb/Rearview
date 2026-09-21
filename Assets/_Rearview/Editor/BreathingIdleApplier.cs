using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rearview.Editor
{
    public static class BreathingIdleApplier
    {
        private const string IdleFbxPath = "Assets/_Rearview/Animations/Y_Bot@Breathing_Idle.fbx";
        private const string YBotFbxPath = "Assets/_Rearview/Mesh/Y_Bot.fbx";
        private const string RiderControllerPath = "Assets/Malbers Animations/Horse AnimSet Pro/2 - Animations/AC Human v5 Rider.controller";
        private const string HumanControllerPath = "Assets/Malbers Animations/Common/Human Anims/AC Human v5.controller";

        [MenuItem("Tools/Rearview/Apply Breathing Idle to HAP")]
        public static void ApplyBreathingIdle()
        {
            if (Execute())
            {
                EditorUtility.DisplayDialog("Thành công", 
                    "Đã áp dụng animation Y_Bot@Breathing_Idle vào hệ thống HAP (Locomotion BlendTree & Idle State) thành công!", 
                    "OK");
            }
        }

        public static void ApplyBreathingIdleBatch()
        {
            bool success = Execute();
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static bool Execute()
        {
            Debug.Log("[BreathingIdleApplier] 🚀 Bắt đầu cấu hình Breathing Idle cho HAP...");

            // 1. Check FBX existence
            if (!File.Exists(IdleFbxPath))
            {
                Debug.LogError($"[BreathingIdleApplier] ❌ Không tìm thấy file tại: {IdleFbxPath}");
                return false;
            }

            if (!File.Exists(YBotFbxPath))
            {
                Debug.LogError($"[BreathingIdleApplier] ❌ Không tìm thấy file model tại: {YBotFbxPath}");
                return false;
            }

            // 2. Find Y_Bot Avatar
            Avatar yBotAvatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(YBotFbxPath))
            {
                if (asset is Avatar av)
                {
                    yBotAvatar = av;
                    break;
                }
            }

            if (yBotAvatar == null)
            {
                Debug.LogError("[BreathingIdleApplier] ❌ Không tìm thấy Humanoid Avatar trong Y_Bot.fbx!");
                return false;
            }

            // 3. Configure ModelImporter for Y_Bot@Breathing_Idle.fbx
            ModelImporter importer = AssetImporter.GetAtPath(IdleFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[BreathingIdleApplier] ❌ Không thể lấy ModelImporter cho: {IdleFbxPath}");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = yBotAvatar;

            // Configure Clip Animations
            ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;
            string takeName = "mixamo.com";
            float firstFrame = 0f;
            float lastFrame = 298f;

            if (defaultClips != null && defaultClips.Length > 0)
            {
                if (!string.IsNullOrEmpty(defaultClips[0].takeName))
                    takeName = defaultClips[0].takeName;
                firstFrame = defaultClips[0].firstFrame;
                lastFrame = defaultClips[0].lastFrame;
            }

            ModelImporterClipAnimation clip = new ModelImporterClipAnimation
            {
                name = "Breathing_Idle",
                takeName = takeName,
                firstFrame = firstFrame,
                lastFrame = lastFrame,
                loopTime = true,
                loopPose = true,
                lockRootRotation = true,
                lockRootHeightY = true,
                lockRootPositionXZ = true,
                keepOriginalOrientation = true,
                keepOriginalPositionY = false,
                keepOriginalPositionXZ = false,
                heightFromFeet = true,
                maskType = ClipAnimationMaskType.CreateFromThisModel
            };

            // Add LookAt curve (Constant 1.0) for Malbers IKGenericLookAt compatibility
            var lookAtCurve = new ClipAnimationInfoCurve
            {
                name = "LookAt",
                curve = AnimationCurve.Constant(0f, 1f, 1f)
            };
            clip.curves = new ClipAnimationInfoCurve[] { lookAtCurve };

            importer.clipAnimations = new ModelImporterClipAnimation[] { clip };
            importer.SaveAndReimport();
            AssetDatabase.Refresh();

            // 4. Load the generated AnimationClip
            AnimationClip breathingIdleClip = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(IdleFbxPath))
            {
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    breathingIdleClip = c;
                    break;
                }
            }

            if (breathingIdleClip == null)
            {
                Debug.LogError("[BreathingIdleApplier] ❌ Không tìm thấy AnimationClip sau khi reimport!");
                return false;
            }

            Debug.Log($"[BreathingIdleApplier] ✅ Đã tạo AnimationClip: '{breathingIdleClip.name}' (Length: {breathingIdleClip.length:F2}s, Humanoid: {breathingIdleClip.isHumanMotion})");

            // 5. Update Animator Controllers
            bool riderUpdated = UpdateController(RiderControllerPath, breathingIdleClip);
            bool humanUpdated = UpdateController(HumanControllerPath, breathingIdleClip);

            AssetDatabase.SaveAssets();
            Debug.Log($"[BreathingIdleApplier] 🎉 Hoàn tất! Rider Controller updated: {riderUpdated}, Human Controller updated: {humanUpdated}");
            return riderUpdated;
        }

        private static bool UpdateController(string controllerPath, AnimationClip newIdleClip)
        {
            if (!File.Exists(controllerPath))
            {
                Debug.LogWarning($"[BreathingIdleApplier] ⚠️ Không tìm thấy controller tại: {controllerPath}");
                return false;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogError($"[BreathingIdleApplier] ❌ Không thể load AnimatorController: {controllerPath}");
                return false;
            }

            int replacedCount = 0;

            foreach (var layer in controller.layers)
            {
                replacedCount += ProcessStateMachine(layer.stateMachine, newIdleClip);
            }

            if (replacedCount > 0)
            {
                EditorUtility.SetDirty(controller);
                Debug.Log($"[BreathingIdleApplier] ✅ Đã thay thế {replacedCount} chỗ trong '{Path.GetFileName(controllerPath)}'");
                return true;
            }

            Debug.LogWarning($"[BreathingIdleApplier] ⚠️ Không tìm thấy vị trí nào cần thay thế trong '{Path.GetFileName(controllerPath)}'");
            return false;
        }

        private static int ProcessStateMachine(AnimatorStateMachine sm, AnimationClip newIdleClip)
        {
            if (sm == null) return 0;
            int count = 0;

            // Check states in this state machine
            foreach (var childState in sm.states)
            {
                var state = childState.state;
                if (state == null) continue;

                // 1. Direct State: "Idle" with tag "Idle" or motion named "H_Idle2" / "Idle v2"
                if (state.name == "Idle" && (state.tag == "Idle" || IsOldIdleMotion(state.motion)))
                {
                    state.motion = newIdleClip;
                    count++;
                    Debug.Log($"[BreathingIdleApplier] -> Thay thế state: '{state.name}' (Tag: {state.tag})");
                }
                else if (state.motion is BlendTree bt)
                {
                    count += ProcessBlendTree(bt, newIdleClip);
                }
            }

            // Recursively process sub-state machines
            foreach (var childSm in sm.stateMachines)
            {
                count += ProcessStateMachine(childSm.stateMachine, newIdleClip);
            }

            return count;
        }

        private static int ProcessBlendTree(BlendTree bt, AnimationClip newIdleClip)
        {
            if (bt == null) return 0;
            int count = 0;

            // Check if this is a Locomotion BlendTree
            bool isLocomotionBT = bt.name.Contains("Locomotion");

            var children = bt.children;
            bool modified = false;

            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];

                if (child.motion is BlendTree subBt)
                {
                    count += ProcessBlendTree(subBt, newIdleClip);
                }
                else if (isLocomotionBT && (child.position == Vector2.zero || IsOldIdleMotion(child.motion)))
                {
                    // Locomotion at (0, 0) is Idle
                    child.motion = newIdleClip;
                    children[i] = child;
                    modified = true;
                    count++;
                    Debug.Log($"[BreathingIdleApplier] -> Thay thế BlendTree: '{bt.name}' child [{i}] tại pos {child.position}");
                }
                else if (IsOldIdleMotion(child.motion))
                {
                    child.motion = newIdleClip;
                    children[i] = child;
                    modified = true;
                    count++;
                    Debug.Log($"[BreathingIdleApplier] -> Thay thế BlendTree: '{bt.name}' child [{i}] (Old Idle Motion)");
                }
            }

            if (modified)
            {
                bt.children = children;
                EditorUtility.SetDirty(bt);
            }

            return count;
        }

        private static bool IsOldIdleMotion(Motion motion)
        {
            if (motion == null) return false;
            string motionName = motion.name.ToLower();
            return motionName == "h_idle2" || motionName == "idle v2" || motionName.Contains("s_idle");
        }
    }
}
