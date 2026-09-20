using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using MalbersAnimations;
using MalbersAnimations.Events;
using MalbersAnimations.Controller;

namespace Rearview.Editor
{
    public static class VehicleSceneSetup
    {
        private const string CamerasCM3PrefabPath = "Assets/Malbers Animations/Common/Cinemachine/Cameras CM3.prefab";
        private const string InteractUIPrefabPath = "Assets/Malbers Animations/Common/Prefabs/UI/Interact UI.prefab";
        private const string InteractUIAssetPath = "Assets/Malbers Animations/Common/Assets/Events/Extras/Interact UI.asset";

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

                // Ensure Cinemachine brain camera has MainCamera tag
                var brainCam = cmBrain.GetComponent<Camera>();
                if (brainCam != null && !brainCam.CompareTag("MainCamera"))
                {
                    brainCam.tag = "MainCamera";
                }
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

            // 4. Ensure Door_Driver child transform on Car
            Transform doorPoint = null;
            if (car != null)
            {
                doorPoint = car.transform.Find("Door_Driver");
                if (doorPoint == null)
                {
                    GameObject doorGO = new GameObject("Door_Driver");
                    doorGO.transform.SetParent(car.transform, false);
                    doorGO.transform.localPosition = new Vector3(-1.15f, 0.9f, 0.2f);
                    doorGO.transform.localRotation = Quaternion.identity;
                    doorPoint = doorGO.transform;
                    Undo.RegisterCreatedObjectUndo(doorGO, "Create Door_Driver");
                    Debug.Log("[VehicleSceneSetup] ✅ Đã tạo điểm neo Door_Driver trên xe.");
                }
            }

            // 5. Ensure UI_Canvas exists (Screen Space Overlay) for HAP UI
            Canvas uiCanvas = null;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.name == "UI_Canvas" || c.name == "Main UI")
                {
                    uiCanvas = c;
                    break;
                }
            }

            if (uiCanvas == null)
            {
                GameObject canvasGO = new GameObject("UI_Canvas");
                uiCanvas = canvasGO.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.pixelPerfect = true;

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasGO.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create UI_Canvas");
                Debug.Log("[VehicleSceneSetup] ✅ Đã tạo UI_Canvas (Screen Space Overlay).");
            }

            // 6. Ensure Interact UI prefab is instantiated under UI_Canvas
            Transform interactUIInstance = uiCanvas.transform.Find("Interact UI");
            if (interactUIInstance == null)
            {
                GameObject interactUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractUIPrefabPath);
                if (interactUIPrefab != null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(interactUIPrefab, uiCanvas.transform);
                    instance.name = "Interact UI";
                    interactUIInstance = instance.transform;
                    Undo.RegisterCreatedObjectUndo(instance, "Instantiate Interact UI");
                    Debug.Log("[VehicleSceneSetup] ✅ Đã thêm Interact UI prefab vào UI_Canvas.");
                }
                else
                {
                    Debug.LogWarning($"[VehicleSceneSetup] ⚠️ Không tìm thấy Interact UI prefab tại {InteractUIPrefabPath}");
                }
            }

            // 7. Load Interact UI MEvent asset
            MEvent interactUIEvent = AssetDatabase.LoadAssetAtPath<MEvent>(InteractUIAssetPath);
            if (interactUIEvent == null)
            {
                Debug.LogWarning($"[VehicleSceneSetup] ⚠️ Không tìm thấy Interact UI asset tại {InteractUIAssetPath}");
            }

            // 8. Find Character (MAnimal)
            GameObject character = null;
            var animal = Object.FindFirstObjectByType<MAnimal>();
            if (animal != null)
            {
                character = animal.gameObject;
            }

            // 9. Ensure VehicleCharacterManager exists
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
            manager.doorPoint = doorPoint;
            manager.interactUIEvent = interactUIEvent;
            manager.currentState = VehicleCharacterManager.ControlState.OnFoot;
            manager.enterPromptText = "Lên Xe";
            manager.exitPromptText = "Xuống Xe";
            manager.interactCooldown = 0.5f;
            manager.interactionDistance = 3.5f;
            manager.showPrompt = true;
            EditorUtility.SetDirty(manager);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            string msg = $"🎉 Setup hoàn tất!\n\n" +
                         $"• Xe: {(car ? car.name : "Chưa tìm thấy")}\n" +
                         $"• Điểm cửa Door_Driver: {(doorPoint ? "Đã gán" : "Chưa có")}\n" +
                         $"• RCC Camera: {(rccCam ? rccCam.name : "Chưa tìm thấy")}\n" +
                         $"• Nhân vật: {(character ? character.name : "Chưa tìm thấy")}\n" +
                         $"• Camera HAP: {(hapCameraRig ? hapCameraRig.name : "Chưa tìm thấy")}\n" +
                         $"• HAP Interact UI: {(interactUIInstance ? "Đã cài đặt trên UI_Canvas" : "Chưa có")}\n" +
                         $"• MEvent: {(interactUIEvent ? "Đã gán Interact UI.asset" : "Chưa gán")}\n" +
                         $"• EventSystem: {(eventSystem ? "Đã sẵn sàng" : "Chưa có")}\n\n" +
                         $"Trạng thái ban đầu: OnFoot (Nhân vật đi bộ, xe tắt máy chờ). Đến gần xe sẽ hiện UI của HAP, bấm [E] để lên xe!";

            EditorUtility.DisplayDialog("Rearview - Setup Thành Công", msg, "OK");
            Debug.Log($"[VehicleSceneSetup] {msg}");
        }
    }
}
