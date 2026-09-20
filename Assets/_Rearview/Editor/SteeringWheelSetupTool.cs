using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rearview.Editor
{
    public class SteeringWheelSetupTool : EditorWindow
    {
        private GameObject targetPrefab;
        private GameObject sourceModel;
        private DefaultAsset materialFolder;

        private const string DefaultPrefabPath = "Assets/_Rearview/Prefab/Car.prefab";
        private const string DefaultRccPrefabPath = "Assets/RealisticCarControllerV4/Prefabs/Vehicles/The_Last_Drive_Car.prefab";
        private const string DefaultModelPath = "Assets/_Rearview/Mesh/The_Last_Drive_Car.fbx";
        private const string DefaultMatFolderPath = "Assets/_Rearview/Material/Car";

        private static readonly string[] NewPartNames = new[]
        {
            "Steering_Wheel",
            "Steering_Column",
            "Stalk_Gear",
            "Stalk_Wiper"
        };

        [MenuItem("Tools/Rearview/Setup Steering Wheel & Stalks (Window)...")]
        public static void ShowWindow()
        {
            var window = GetWindow<SteeringWheelSetupTool>("Steering Wheel Setup");
            window.minSize = new Vector2(480, 360);
            window.InitDefaults();
        }

        [MenuItem("Tools/Rearview/⚡ Quick Setup Steering Wheel in All Prefabs")]
        public static void QuickExecuteAll()
        {
            if (File.Exists(DefaultPrefabPath))
                ExecuteSetup(DefaultPrefabPath, DefaultModelPath, DefaultMatFolderPath);

            if (File.Exists(DefaultRccPrefabPath))
                ExecuteSetup(DefaultRccPrefabPath, DefaultModelPath, DefaultMatFolderPath);

            AssetDatabase.Refresh();
        }

        private void OnEnable()
        {
            InitDefaults();
        }

        private void InitDefaults()
        {
            if (targetPrefab == null)
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);

            if (sourceModel == null)
                sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);

            if (materialFolder == null)
                materialFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultMatFolderPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎡 Steering Wheel & Stalks Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Công cụ tự động:\n" +
                "1. Xóa 'SteeringWheelCluster' cũ trong Prefab xe.\n" +
                "2. Thêm 4 đối tượng mới tách từ FBX: Steering_Wheel, Steering_Column, Stalk_Gear, Stalk_Wiper.\n" +
                "3. Giữ nguyên Pivot tâm vô lăng và góc nghiêng 20° chuẩn xác từ FBX.\n" +
                "4. Gán tự động các Materials tương ứng từ thư mục Material.\n" +
                "5. Cập nhật RCC_CarControllerV4: gán SteeringWheel, xoay quanh ZAxis, hệ số 11.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target Prefab", targetPrefab, typeof(GameObject), false);
            sourceModel = (GameObject)EditorGUILayout.ObjectField("Source Model (FBX)", sourceModel, typeof(GameObject), false);
            materialFolder = (DefaultAsset)EditorGUILayout.ObjectField("Material Folder", materialFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("⚡ Cập Nhật Cho Prefab Đang Chọn", GUILayout.Height(36)))
            {
                if (targetPrefab != null)
                {
                    string path = AssetDatabase.GetAssetPath(targetPrefab);
                    string modelPath = sourceModel ? AssetDatabase.GetAssetPath(sourceModel) : DefaultModelPath;
                    string matPath = materialFolder ? AssetDatabase.GetAssetPath(materialFolder) : DefaultMatFolderPath;
                    ExecuteSetup(path, modelPath, matPath);
                }
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("🚗 Cập Nhật Cho Cả 2 Prefab (Car.prefab & The_Last_Drive_Car.prefab)", GUILayout.Height(32)))
            {
                string modelPath = sourceModel ? AssetDatabase.GetAssetPath(sourceModel) : DefaultModelPath;
                string matPath = materialFolder ? AssetDatabase.GetAssetPath(materialFolder) : DefaultMatFolderPath;

                if (File.Exists(DefaultPrefabPath))
                    ExecuteSetup(DefaultPrefabPath, modelPath, matPath);

                if (File.Exists(DefaultRccPrefabPath))
                    ExecuteSetup(DefaultRccPrefabPath, modelPath, matPath);
            }
        }

        public static void ExecuteSetup(string prefabPath, string modelPath, string matFolder)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"[SteeringWheelSetupTool] ❌ Không tìm thấy Model FBX tại: {modelPath}");
                return;
            }

            // Load materials from folder
            var materialMap = new Dictionary<string, Material>();
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { matFolder });
            foreach (var guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && !materialMap.ContainsKey(mat.name))
                {
                    materialMap[mat.name] = mat;
                }
            }

            // Find source parts in FBX model
            var sourceParts = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                foreach (var partName in NewPartNames)
                {
                    if (t.name == partName)
                    {
                        sourceParts[partName] = t;
                        break;
                    }
                }
            }

            if (sourceParts.Count < NewPartNames.Length)
            {
                Debug.LogError($"[SteeringWheelSetupTool] ❌ Chỉ tìm thấy {sourceParts.Count}/{NewPartNames.Length} parts trong FBX! Kiểm tra lại file FBX.");
                return;
            }

            // Open prefab for editing
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[SteeringWheelSetupTool] ❌ Không thể mở Prefab tại: {prefabPath}");
                return;
            }

            try
            {
                // Find the container where car meshes reside (The_Last_Drive_Car)
                Transform carMeshParent = null;
                var allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t.name == "The_Last_Drive_Car")
                    {
                        carMeshParent = t;
                        break;
                    }
                }

                // Fallback: check where SteeringWheelCluster or ControlPanel was parented
                if (carMeshParent == null)
                {
                    foreach (var t in allTransforms)
                    {
                        if (t.name == "SteeringWheelCluster" || t.name == "ControlPanel" || t.name == "Body_HeadLight_top")
                        {
                            carMeshParent = t.parent;
                            break;
                        }
                    }
                }

                if (carMeshParent == null)
                {
                    carMeshParent = prefabRoot.transform;
                }

                Debug.Log($"[SteeringWheelSetupTool] 📂 Gắn các mesh con vào node cha: '{carMeshParent.name}' (cùng cấp với các mesh khác của xe).");

                // 1. Remove old SteeringWheelCluster if present
                var oldCluster = carMeshParent.Find("SteeringWheelCluster");
                if (oldCluster == null)
                {
                    foreach (var t in allTransforms)
                    {
                        if (t.name == "SteeringWheelCluster")
                        {
                            oldCluster = t;
                            break;
                        }
                    }
                }

                if (oldCluster != null)
                {
                    Object.DestroyImmediate(oldCluster.gameObject);
                    Debug.Log($"[SteeringWheelSetupTool] 🗑️ Đã xóa 'SteeringWheelCluster' cũ khỏi '{carMeshParent.name}'.");
                }

                // 2. Add or update each new part under carMeshParent
                Transform steeringWheelTransform = null;
                foreach (var partName in NewPartNames)
                {
                    Transform srcTransform = sourceParts[partName];
                    var srcMF = srcTransform.GetComponent<MeshFilter>();
                    var srcMR = srcTransform.GetComponent<MeshRenderer>();

                    // Check if already exists in carMeshParent or prefab
                    Transform existing = carMeshParent.Find(partName);
                    GameObject partGO;
                    if (existing != null)
                    {
                        partGO = existing.gameObject;
                    }
                    else
                    {
                        partGO = new GameObject(partName);
                        partGO.transform.SetParent(carMeshParent, false);
                    }

                    // Copy local transform (preserves exact pivot and 20° tilt from FBX)
                    partGO.transform.localPosition = srcTransform.localPosition;
                    partGO.transform.localRotation = srcTransform.localRotation;
                    partGO.transform.localScale = srcTransform.localScale;

                    // Explicit override for Steering_Wheel as requested: local y = 0.4180036, rotation = (20, 0, 0)
                    if (partName == "Steering_Wheel")
                    {
                        partGO.transform.localPosition = new Vector3(srcTransform.localPosition.x, 0.4180036f, srcTransform.localPosition.z);
                        partGO.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                    }

                    partGO.layer = carMeshParent.gameObject.layer;

                    // MeshFilter
                    var mf = partGO.GetComponent<MeshFilter>();
                    if (mf == null) mf = partGO.AddComponent<MeshFilter>();
                    if (srcMF != null) mf.sharedMesh = srcMF.sharedMesh;

                    // MeshRenderer
                    var mr = partGO.GetComponent<MeshRenderer>();
                    if (mr == null) mr = partGO.AddComponent<MeshRenderer>();

                    if (srcMR != null)
                    {
                        Material[] newMats = new Material[srcMR.sharedMaterials.Length];
                        for (int i = 0; i < srcMR.sharedMaterials.Length; i++)
                        {
                            var origMat = srcMR.sharedMaterials[i];
                            if (origMat != null && materialMap.TryGetValue(origMat.name, out Material mappedMat))
                            {
                                newMats[i] = mappedMat;
                            }
                            else
                            {
                                newMats[i] = origMat;
                            }
                        }
                        mr.sharedMaterials = newMats;
                    }

                    if (partName == "Steering_Wheel")
                    {
                        steeringWheelTransform = partGO.transform;
                    }

                    Debug.Log($"[SteeringWheelSetupTool] ✅ Đã cấu hình '{partName}' dưới '{carMeshParent.name}' tại pos={partGO.transform.localPosition}, rot={partGO.transform.localRotation.eulerAngles}");
                }

                // 3. Configure RCC_CarControllerV4 (on prefab root or wherever it is attached)
                var carController = prefabRoot.GetComponentInChildren<RCC_CarControllerV4>();
                if (carController != null && steeringWheelTransform != null)
                {
                    carController.SteeringWheel = steeringWheelTransform;
                    carController.steeringWheelRotateAround = RCC_CarControllerV4.SteeringWheelRotateAround.ZAxis;
                    carController.steeringWheelAngleMultiplier = 11f;
                    Debug.Log($"[SteeringWheelSetupTool] 🚗 Đã gán SteeringWheel vào RCC_CarControllerV4 (RotateAround: ZAxis, Multiplier: 11).");
                }

                // Save prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log($"[SteeringWheelSetupTool] 💾 Đã lưu Prefab thành công: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
