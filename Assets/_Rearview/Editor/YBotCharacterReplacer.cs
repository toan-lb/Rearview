using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Scriptables;
using MalbersAnimations.IK;
using MalbersAnimations.Weapons;
using MalbersAnimations.Utilities;
using MalbersAnimations.HAP;

namespace Rearview.Editor
{
    public static class YBotCharacterReplacer
    {
        private const string YBotFbxPath = "Assets/_Rearview/Mesh/Y_Bot.fbx";
        private const string BackupFolderPath = "Assets/_Rearview/Prefab/Backup";
        private const string BackupPrefabPath = "Assets/_Rearview/Prefab/Backup/Cowboy_Backup.prefab";
        private const string OriginalCowboyPrefabPath = "Assets/Malbers Animations/Horse AnimSet Pro/4 - Prefabs/Rider/Cowboy (Combat).prefab";
        private const string NewPrefabPath = "Assets/_Rearview/Prefab/Player_YBot.prefab";
        private const string CameraTargetHookPath = "Assets/Malbers Animations/Common/Scriptable Assets/Hooks/Camera Target.asset";

        [MenuItem("Tools/Rearview/Replace Character with Y_Bot (with Backup)")]
        public static void ReplaceWithYBot()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Replace Character with Y_Bot");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Ensure Y_Bot.fbx is imported as Humanoid
            ModelImporter importer = AssetImporter.GetAtPath(YBotFbxPath) as ModelImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy file model tại {YBotFbxPath}", "OK");
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
            }

            // Find Y_Bot Avatar
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
                EditorUtility.DisplayDialog("Lỗi", "Không thể tìm thấy Humanoid Avatar trong Y_Bot.fbx. Hãy kiểm tra lại tab Rig của file FBX.", "OK");
                return;
            }

            // Position & rotation to place the new character
            Vector3 targetPos = new Vector3(-450.94244f, 24.8f, 58.2f);
            Quaternion targetRot = Quaternion.identity;

            // 2. Find the REAL Base Cowboy character (must have MRider and Player Core)
            GameObject cowboySource = null;
            var allSceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var staleObjectsToDelete = new System.Collections.Generic.List<GameObject>();

            foreach (var go in allSceneObjects)
            {
                if (!go.scene.isLoaded) continue;

                if (go.name == "Player_YBot")
                {
                    targetPos = go.transform.position;
                    targetRot = go.transform.rotation;
                    staleObjectsToDelete.Add(go);
                }
                else if (go.name.Contains("Cowboy"))
                {
                    bool isRealCowboy = go.GetComponent<MRider>() != null && go.transform.Find("Player Core") != null;
                    if (isRealCowboy && cowboySource == null)
                    {
                        cowboySource = go;
                    }
                    else
                    {
                        // Stale duplicate or broken backup
                        staleObjectsToDelete.Add(go);
                    }
                }
            }

            // Remove stale objects
            foreach (var stale in staleObjectsToDelete)
            {
                Undo.DestroyObjectImmediate(stale);
            }

            // If no real cowboy was in the scene, instantiate from OriginalCowboyPrefabPath
            if (cowboySource == null)
            {
                GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalCowboyPrefabPath);
                if (originalPrefab != null)
                {
                    cowboySource = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab);
                    cowboySource.name = "Cowboy (Backup)";
                    cowboySource.transform.position = targetPos;
                    cowboySource.transform.rotation = targetRot;
                    Undo.RegisterCreatedObjectUndo(cowboySource, "Instantiate Original Cowboy");
                    Debug.Log("[YBotCharacterReplacer] ℹ️ Đã load Cowboy gốc từ Prefab vì scene không còn bản hợp lệ.");
                }
            }

            if (cowboySource == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy Cowboy gốc trong scene hoặc prefab!", "OK");
                return;
            }

            // 3. Ensure Backup Prefab exists and save the REAL cowboy as backup
            if (!Directory.Exists(BackupFolderPath))
            {
                Directory.CreateDirectory(BackupFolderPath);
                AssetDatabase.Refresh();
            }

            PrefabUtility.SaveAsPrefabAsset(cowboySource, BackupPrefabPath);
            Debug.Log($"[YBotCharacterReplacer] 💾 Đã backup Cowboy gốc thành công tại: {BackupPrefabPath}");

            // Ensure cowboySource is named Cowboy (Backup) and disabled in scene
            Undo.RecordObject(cowboySource, "Ensure Cowboy Backup Disabled");
            cowboySource.name = "Cowboy (Backup)";
            cowboySource.SetActive(false);

            // 4. Instantiate new Player_YBot from Cowboy base (to retain ALL Malbers components & hierarchy)
            GameObject yBotInstance = Object.Instantiate(cowboySource, targetPos, targetRot);

            // Unpack completely so we can modify hierarchy freely
            if (PrefabUtility.IsPartOfAnyPrefab(yBotInstance))
            {
                PrefabUtility.UnpackPrefabInstance(yBotInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            yBotInstance.name = "Player_YBot";
            yBotInstance.SetActive(true);
            Undo.RegisterCreatedObjectUndo(yBotInstance, "Create Player_YBot");

            // 5. Remove old Cowboy Mesh and Bones
            Transform oldMesh = yBotInstance.transform.Find("Mesh");
            if (oldMesh != null)
            {
                Object.DestroyImmediate(oldMesh.gameObject);
            }

            Transform oldRCG = yBotInstance.transform.Find("R_CG");
            if (oldRCG != null)
            {
                Object.DestroyImmediate(oldRCG.gameObject);
            }

            // 6. Instantiate Y_Bot FBX children (Alpha_Joints, Alpha_Surface, mixamorig:Hips)
            GameObject yBotModel = AssetDatabase.LoadAssetAtPath<GameObject>(YBotFbxPath);
            GameObject tempFbx = Object.Instantiate(yBotModel);

            Transform alphaJoints = tempFbx.transform.Find("Alpha_Joints");
            Transform alphaSurface = tempFbx.transform.Find("Alpha_Surface");
            Transform hips = tempFbx.transform.Find("mixamorig:Hips");

            if (alphaJoints != null)
            {
                alphaJoints.SetParent(yBotInstance.transform, false);
                alphaJoints.gameObject.layer = 20; // Animal layer
            }
            if (alphaSurface != null)
            {
                alphaSurface.SetParent(yBotInstance.transform, false);
                alphaSurface.gameObject.layer = 20; // Animal layer
            }
            if (hips != null)
            {
                hips.SetParent(yBotInstance.transform, false);
                SetLayerRecursively(hips.gameObject, 0); // Default layer for bones
            }

            Object.DestroyImmediate(tempFbx);

            // 7. Configure Animator
            Animator animator = yBotInstance.GetComponent<Animator>();
            if (animator == null) animator = yBotInstance.AddComponent<Animator>();
            animator.avatar = yBotAvatar;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 8. Re-link bone references in Malbers components
            Transform leftHand = FindChildRecursive(yBotInstance.transform, "mixamorig:LeftHand");
            Transform rightHand = FindChildRecursive(yBotInstance.transform, "mixamorig:RightHand");
            Transform neck = FindChildRecursive(yBotInstance.transform, "mixamorig:Neck");
            Transform head = FindChildRecursive(yBotInstance.transform, "mixamorig:Head");

            // MWeaponManager
            var wm = yBotInstance.GetComponent<MWeaponManager>();
            if (wm != null)
            {
                if (leftHand != null) wm.LeftHandEquipPoint = leftHand;
                if (rightHand != null) wm.RightHandEquipPoint = rightHand;
                wm.Anim = animator;
            }

            // IKManager
            var ik = yBotInstance.GetComponent<IKManager>();
            if (ik != null)
            {
                ik.animator = animator;
                if (ik.sets != null)
                {
                    foreach (var s in ik.sets)
                    {
                        if (s.Targets != null && s.Targets.Length >= 2)
                        {
                            string sName = s.name != null ? s.name.Value : "";
                            if (sName == "Look At" || sName == "Look At Camera")
                            {
                                if (neck != null) s.Targets[0].ConstantValue = neck;
                                if (head != null) s.Targets[1].ConstantValue = head;
                            }
                        }

                        // Reset bone offset for Y_Bot on all IKGenericLookAt processors
                        // (Cowboy used Offset (0, -90, -90) due to 3ds Max bone orientation, which severely deformed Mixamo bones)
                        if (s.IKProcesors != null)
                        {
                            foreach (var p in s.IKProcesors)
                            {
                                if (p is IKGenericLookAt genericLookAt)
                                {
                                    genericLookAt.Offset = Vector3.zero;
                                }
                            }
                        }
                    }
                }
            }

            // Aim
            var aim = yBotInstance.GetComponent<Aim>();
            if (aim != null)
            {
                aim.m_Animator = animator;
            }

            // MRider
            var rider = yBotInstance.GetComponent<MRider>();
            if (rider != null)
            {
                if (leftHand != null) rider.LeftHand = leftHand;
                if (rightHand != null) rider.RightHand = rightHand;
            }

            // MAnimal
            var animal = yBotInstance.GetComponent<MAnimal>();
            if (animal != null)
            {
                animal.Anim = animator;
                animal.MainCollider = yBotInstance.GetComponent<CapsuleCollider>();
                animal.RB = yBotInstance.GetComponent<Rigidbody>();
                animal.Aimer = aim;
                Transform rotator = yBotInstance.transform.Find("Rotator");
                if (rotator != null) animal.Rotator = rotator;

                animal.height = 1.5f;
                animal.m_pivotMultiplier = 1.5f;
                animal.Has_Pivot_Chest = true;
                animal.Has_Pivot_Hip = false;
                if (animal.Pivot_Chest != null)
                {
                    animal.Pivot_Chest.position = new Vector3(0f, 1.5f, 0f);
                    animal.Pivot_Chest.name = "Chest";
                }
            }

            // TransformHooks in Player Core
            Transform transformHooks = yBotInstance.transform.Find("Player Core/Trasform Hooks");
            if (transformHooks != null && head != null)
            {
                var hooks = transformHooks.GetComponents<TransformHook>();
                foreach (var h in hooks)
                {
                    if (h.Hook != null && h.Hook.name.Contains("Head"))
                    {
                        h.Reference = head;
                    }
                }
            }

            // CM Main Target in Player Core
            Transform cmMainTarget = yBotInstance.transform.Find("Player Core/CM Main Target");
            if (cmMainTarget != null)
            {
                cmMainTarget.localPosition = new Vector3(0f, 1.5f, 0f);
                var tracker = cmMainTarget.GetComponent<AnimalTracker>();
                if (tracker != null)
                {
                    tracker.animal = animal;
                }
                var hook = cmMainTarget.GetComponent<TransformHook>();
                if (hook != null)
                {
                    hook.Reference = cmMainTarget;
                    if (hook.Hook == null)
                    {
                        hook.Hook = AssetDatabase.LoadAssetAtPath<TransformVar>(CameraTargetHookPath);
                    }
                    // Immediately assign runtime value in editor so camera updates
                    if (hook.Hook != null)
                    {
                        hook.Hook.Value = cmMainTarget;
                    }
                }
            }

            // 9. Configure Cameras CM3
            TransformVar cameraTargetVar = AssetDatabase.LoadAssetAtPath<TransformVar>(CameraTargetHookPath);
            GameObject camObj = GameObject.Find("Cameras CM3");
            if (camObj != null)
            {
                var follow = camObj.GetComponentInChildren<ThirdPersonFollowTarget>();
                if (follow != null && cameraTargetVar != null)
                {
                    Undo.RecordObject(follow, "Set Camera Target Variable");
                    follow.Target.Variable = cameraTargetVar;
                    follow.Target.UseConstant = false;
                    EditorUtility.SetDirty(follow);
                    Debug.Log("[YBotCharacterReplacer] 📷 Đã liên kết Cameras CM3 với Camera Target Hook thành công!");
                }
            }

            // 10. Update VehicleCharacterManager reference
            VehicleCharacterManager manager = Object.FindFirstObjectByType<VehicleCharacterManager>();
            if (manager != null)
            {
                Undo.RecordObject(manager, "Update VehicleCharacterManager Character");
                manager.character = yBotInstance;
                EditorUtility.SetDirty(manager);
                Debug.Log("[YBotCharacterReplacer] 🚗 Đã cập nhật VehicleCharacterManager.character = Player_YBot");
            }

            // 11. Save as new Prefab
            PrefabUtility.SaveAsPrefabAssetAndConnect(yBotInstance, NewPrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[YBotCharacterReplacer] ✅ Đã lưu Prefab nhân vật mới tại: {NewPrefabPath}");

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            DiagnoseCharacters.Diagnose();

            string msg = $"🎉 Thiết lập nhân vật Y_Bot hoàn tất chuẩn theo bản backup!\n\n" +
                         $"• Giữ nguyên toàn bộ hệ thống Malbers (Player Core, Internal Components, Rotator, Inputs, Stats)\n" +
                         $"• Đã thay thế Model & Avatar sang Y_Bot (Alpha_Joints, Alpha_Surface, mixamorig:Hips)\n" +
                         $"• Đã re-link EquipPoints, IK, Aim, Aimer, và Head hooks sang bones của Y_Bot\n" +
                         $"• Đã kích hoạt CM Main Target & TransformHook -> Cameras CM3 theo dõi mượt mà\n" +
                         $"• MAnimal & CapsuleCollider giữ đúng chuẩn ground alignment, không bị tụt chân\n" +
                         $"• VehicleCharacterManager đã cập nhật trỏ sang Player_YBot mới";

            Debug.Log($"[YBotCharacterReplacer] {msg}");
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
