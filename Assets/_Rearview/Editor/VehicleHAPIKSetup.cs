using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MalbersAnimations.IK;
using MalbersAnimations.Scriptables;

namespace Rearview.Editor
{
    [InitializeOnLoad]
    public static class VehicleHAPIKSetup
    {
        static VehicleHAPIKSetup()
        {
            EditorApplication.delayCall += () =>
            {
                SetupHAPSteeringIK();
            };
        }

        [MenuItem("Rearview/Setup HAP Steering IK")]
        public static void SetupHAPSteeringIK()
        {
            // 1. Find character in scene
            GameObject character = GameObject.Find("Player_YBot");
            if (!character)
            {
                var animal = Object.FindFirstObjectByType<MalbersAnimations.Controller.MAnimal>();
                if (animal) character = animal.gameObject;
            }

            if (!character)
            {
                return;
            }

            // Ensure VehicleSteeringIKHook is attached to character
            if (!character.GetComponent<VehicleSteeringIKHook>())
            {
                Undo.AddComponent<VehicleSteeringIKHook>(character);
            }

            var ikManager = character.GetComponent<IKManager>();
            if (!ikManager)
            {
                return;
            }

            Undo.RecordObject(ikManager, "Setup HAP Steering IK");

            // 2. Ensure Steering IKSet exists
            var existingSet = ikManager.FindSet("Steering");
            if (existingSet == null)
            {
                existingSet = new IKSet()
                {
                    name = new StringReference("Steering") { UseConstant = true },
                    active = false,
                    Weight = 1f,
                    EnableTime = 0.05f,
                    DisableTime = 0.1f,
                    LerpWeight = 0f,
                    Targets = new TransformReference[2]
                    {
                        new TransformReference() { UseConstant = true },
                        new TransformReference() { UseConstant = true }
                    },
                    IKProcesors = new List<IKProcessor>()
                    {
                        new HumanIKGoal()
                        {
                            name = "Left Hand Steering Goal",
                            Active = true,
                            Weight = 1f,
                            goal = AvatarIKGoal.LeftHand,
                            TargetIndex = 0,
                            position = true,
                            rotation = true,
                            OffsetP = Vector3.zero,
                            OffsetR = Vector3.zero
                        },
                        new HumanIKGoal()
                        {
                            name = "Right Hand Steering Goal",
                            Active = true,
                            Weight = 1f,
                            goal = AvatarIKGoal.RightHand,
                            TargetIndex = 1,
                            position = true,
                            rotation = true,
                            OffsetP = Vector3.zero,
                            OffsetR = Vector3.zero
                        }
                    }
                };

                ikManager.sets.Add(existingSet);
                Debug.Log("[VehicleHAPIKSetup] ✅ Đã thêm IKSet 'Steering' vào IKManager của Player_YBot.");
            }
            if (existingSet != null)
            {
                // Ensure elbow hints are disabled if present to prevent pulling elbows/wrists
                ikManager.Processor_SetEnable("Steering", "Left Elbow Hint", false);
                ikManager.Processor_SetEnable("Steering", "Right Elbow Hint", false);
                Debug.Log("[VehicleHAPIKSetup] ℹ️ IKSet 'Steering' đã tồn tại sẵn (Elbow Hints disabled).");
            }

            EditorUtility.SetDirty(ikManager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(character.scene);

            // 3. Also apply to prefab if Player_YBot is a prefab instance
            var prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(character);
            if (prefabRoot != null)
            {
                var prefabPath = AssetDatabase.GetAssetPath(prefabRoot);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefabAsset)
                    {
                        if (!prefabAsset.GetComponent<VehicleSteeringIKHook>())
                        {
                            prefabAsset.AddComponent<VehicleSteeringIKHook>();
                            EditorUtility.SetDirty(prefabAsset);
                        }

                        var prefabIK = prefabAsset.GetComponent<IKManager>();
                        if (prefabIK)
                        {
                            prefabIK.Processor_SetEnable("Steering", "Left Elbow Hint", false);
                            prefabIK.Processor_SetEnable("Steering", "Right Elbow Hint", false);
                            EditorUtility.SetDirty(prefabIK);
                        }
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            Debug.Log("[VehicleHAPIKSetup] 🎉 Hoàn tất thiết lập HAP Steering IK!");
        }
    }
}
