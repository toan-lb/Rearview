using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using MalbersAnimations;
using MalbersAnimations.Controller;

namespace Rearview.Editor
{
    public static class VehicleSceneSetup
    {
        private const string CamerasCM3PrefabPath = "Assets/Malbers Animations/Common/Cinemachine/Cameras CM3.prefab";

        [MenuItem("Tools/Rearview/Setup Vehicle & Character in Scene")]
        public static void SetupScene()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Setup Vehicle & Character");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Ensure EventSystem exists with InputSystemUIInputModule
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                eventSystem = esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
                Debug.Log("[VehicleSceneSetup] ✅ Đã tạo EventSystem với InputSystemUIInputModule.");
            }
            else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                // In case it had legacy StandaloneInputModule
                var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (legacyModule != null)
                {
                    Undo.DestroyObjectImmediate(legacyModule);
                }
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[VehicleSceneSetup] ✅ Đã cập nhật InputModule của EventSystem sang New Input System.");
            }

            // 2. Ensure Cameras CM3 exists in scene
            GameObject hapCameraRig = null;
            var cmBrain = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
            if (cmBrain != null)
            {
                Transform p = cmBrain.transform.parent;
                hapCameraRig = p ? p.gameObject : cmBrain.gameObject;
            }
            else
            {
                GameObject cmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CamerasCM3PrefabPath);
                if (cmPrefab != null)
                {
                    hapCameraRig = (GameObject)PrefabUtility.InstantiatePrefab(cmPrefab);
                    hapCameraRig.name = "Cameras CM3";
                    Undo.RegisterCreatedObjectUndo(hapCameraRig, "Instantiate Cameras CM3");
                    Debug.Log("[VehicleSceneSetup] ✅ Đã thêm Cameras CM3 (Cinemachine 3) vào Scene.");
                }
                else
                {
                    Debug.LogWarning($"[VehicleSceneSetup] ⚠️ Không tìm thấy prefab tại {CamerasCM3PrefabPath}");
                }
            }

            // 3. Find Car and RCC_Camera
            RCC_CarControllerV4 car = Object.FindFirstObjectByType<RCC_CarControllerV4>();
            RCC_Camera rccCam = Object.FindFirstObjectByType<RCC_Camera>();

            // 4. Find Character (Cowboy)
            GameObject character = null;
            var animal = Object.FindFirstObjectByType<MAnimal>();
            if (animal != null)
            {
                character = animal.gameObject;
            }

            // 5. Ensure VehicleCharacterManager exists
            VehicleCharacterManager manager = Object.FindFirstObjectByType<VehicleCharacterManager>();
            if (manager == null)
            {
                GameObject mgrGO = new GameObject("_VehicleCharacterManager");
                manager = mgrGO.AddComponent<VehicleCharacterManager>();
                Undo.RegisterCreatedObjectUndo(mgrGO, "Create VehicleCharacterManager");
                Debug.Log("[VehicleSceneSetup] ✅ Đã tạo GameObject _VehicleCharacterManager.");
            }

            // Assign all references
            Undo.RecordObject(manager, "Configure VehicleCharacterManager");
            manager.carController = car;
            manager.rccCamera = rccCam;
            manager.character = character;
            manager.hapCameraRig = hapCameraRig;
            manager.currentState = VehicleCharacterManager.ControlState.OnFoot;
            EditorUtility.SetDirty(manager);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            string msg = $"🎉 Setup hoàn tất!\n\n" +
                         $"• Xe: {(car ? car.name : "Chưa tìm thấy")}\n" +
                         $"• RCC Camera: {(rccCam ? rccCam.name : "Chưa tìm thấy")}\n" +
                         $"• Nhân vật: {(character ? character.name : "Chưa tìm thấy")}\n" +
                         $"• Camera HAP: {(hapCameraRig ? hapCameraRig.name : "Chưa tìm thấy")}\n" +
                         $"• EventSystem: {(eventSystem ? "Đã sẵn sàng" : "Chưa có")}\n\n" +
                         $"Trạng thái ban đầu: OnFoot (Nhân vật đi bộ, xe tắt máy chờ). Bấm [E] để vào/ra xe!";

            EditorUtility.DisplayDialog("Rearview - Setup Thành Công", msg, "OK");
            Debug.Log($"[VehicleSceneSetup] {msg}");
        }
    }
}
