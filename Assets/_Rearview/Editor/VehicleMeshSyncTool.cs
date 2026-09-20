using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rearview.Editor
{
    /// <summary>
    /// Vehicle Mesh & Prefab Synchronizer Tool.
    /// Safely synchronizes broken meshes and newly added meshes from an updated FBX model
    /// into unpacked Unity vehicle prefabs, auto-assigns extracted materials,
    /// preserves all RCC & gameplay references, and strictly enforces the Zero-Child-Collider rule.
    /// </summary>
    public class VehicleMeshSyncTool : EditorWindow
    {
        private GameObject sourceModel;
        private GameObject targetPrefab;
        private DefaultAsset materialFolder;

        public const string DefaultModelPath = "Assets/_Rearview/Mesh/The_Last_Drive_Car.fbx";
        public const string DefaultPrefabPath = "Assets/_Rearview/Prefab/Car.prefab";
        public const string DefaultRccPrefabPath = "Assets/RealisticCarControllerV4/Prefabs/Vehicles/The_Last_Drive_Car.prefab";
        public const string DefaultMatFolderPath = "Assets/_Rearview/Material/Car";

        private const int LayerRccVehicle = 8;
        private const int LayerP2PScreen = 31;

        private Vector2 scrollPos;

        [MenuItem("Tools/Rearview/🚗 Sync Vehicle Model Meshes to Prefabs...")]
        public static void ShowWindow()
        {
            var window = GetWindow<VehicleMeshSyncTool>("Vehicle Mesh Sync");
            window.minSize = new Vector2(500, 480);
            window.InitDefaults();
        }

        [MenuItem("Tools/Rearview/🛡️ Clean Visual Mesh Colliders in Prefabs")]
        public static void QuickCleanColliders()
        {
            int cleanedTotal = 0;
            if (File.Exists(DefaultPrefabPath))
                cleanedTotal += CleanVisualColliders(DefaultPrefabPath);

            if (File.Exists(DefaultRccPrefabPath))
                cleanedTotal += CleanVisualColliders(DefaultRccPrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Clean Visual Colliders",
                $"Đã quét và xóa {cleanedTotal} collider thừa trên các mesh hiển thị của Prefab xe.\n" +
                "Vật lý RCC compound collider đã được bảo vệ an toàn!",
                "OK");
        }

        [MenuItem("Tools/Rearview/🔍 Verify RCC Vehicle Integrity")]
        public static void QuickVerifyAll()
        {
            string report1 = File.Exists(DefaultPrefabPath) ? VerifyVehicleIntegrity(DefaultPrefabPath) : "Không tìm thấy Car.prefab";
            string report2 = File.Exists(DefaultRccPrefabPath) ? VerifyVehicleIntegrity(DefaultRccPrefabPath) : "Không tìm thấy The_Last_Drive_Car.prefab";

            string fullReport = $"=== KIỂM TRA TOÀN VẸN RCC ===\n\n" +
                                $"[1] Car.prefab:\n{report1}\n\n" +
                                $"[2] The_Last_Drive_Car.prefab:\n{report2}";

            Debug.Log($"[VehicleMeshSyncTool]\n{fullReport}");
            EditorUtility.DisplayDialog("RCC Vehicle Integrity Report", fullReport, "OK");
        }

        private void OnEnable()
        {
            InitDefaults();
        }

        private void InitDefaults()
        {
            if (sourceModel == null)
                sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);

            if (targetPrefab == null)
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);

            if (materialFolder == null)
                materialFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultMatFolderPath);
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🚗 Vehicle Model & Prefab Mesh Synchronizer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Công cụ chuẩn hóa cập nhật Mesh xe (Break mesh, Thêm mesh) từ Blender vào Unpacked Prefab:\n" +
                "• Tự động thêm các sub-mesh mới hoặc sub-mesh vừa tách (break) từ FBX vào Prefab.\n" +
                "• Cập nhật MeshFilter.sharedMesh cho các mesh cũ đã bị thay đổi hình học.\n" +
                "• Tự động gán Materials từ thư mục Material theo đúng tên material gốc.\n" +
                "• BẢO TOÀN 100% các GameObject và component RCC (Wheel Colliders, Vô lăng, COM, Camera, Script ra/vào xe, HMI Screen).\n" +
                "• TRIỆT ĐỂ áp dụng Zero-Child-Collider: Không để bất kỳ collider nào trên mesh hiển thị làm hỏng PhysX của RCC.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("1. Thiết Lập Đường Dẫn", EditorStyles.boldLabel);
            sourceModel = (GameObject)EditorGUILayout.ObjectField("Source Model (FBX)", sourceModel, typeof(GameObject), false);
            targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target Prefab", targetPrefab, typeof(GameObject), false);
            materialFolder = (DefaultAsset)EditorGUILayout.ObjectField("Material Folder", materialFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("2. Đồng Bộ Mesh Vào Prefab", EditorStyles.boldLabel);

            if (GUILayout.Button("⚡ Đồng Bộ Cho Prefab Đang Chọn", GUILayout.Height(36)))
            {
                if (targetPrefab != null && sourceModel != null)
                {
                    string pPath = AssetDatabase.GetAssetPath(targetPrefab);
                    string mPath = AssetDatabase.GetAssetPath(sourceModel);
                    string matPath = materialFolder ? AssetDatabase.GetAssetPath(materialFolder) : DefaultMatFolderPath;
                    ExecuteSync(pPath, mPath, matPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn đầy đủ Source Model (FBX) và Target Prefab!", "OK");
                }
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("🚗 Đồng Bộ Cả 2 Prefab (Car.prefab & The_Last_Drive_Car.prefab)", GUILayout.Height(32)))
            {
                string mPath = sourceModel ? AssetDatabase.GetAssetPath(sourceModel) : DefaultModelPath;
                string matPath = materialFolder ? AssetDatabase.GetAssetPath(materialFolder) : DefaultMatFolderPath;

                int successCount = 0;
                if (File.Exists(DefaultPrefabPath))
                {
                    if (ExecuteSync(DefaultPrefabPath, mPath, matPath)) successCount++;
                }

                if (File.Exists(DefaultRccPrefabPath))
                {
                    if (ExecuteSync(DefaultRccPrefabPath, mPath, matPath)) successCount++;
                }

                EditorUtility.DisplayDialog("Kết Quả Đồng Bộ", $"Đã hoàn tất đồng bộ {successCount} Prefab!", "OK");
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("3. Bảo Vệ Vật Lý RCC & Kiểm Tra Toàn Vẹn", EditorStyles.boldLabel);

            if (GUILayout.Button("🛡️ Quét & Xóa Toàn Bộ Collider Thừa Trên Visual Mesh", GUILayout.Height(30)))
            {
                QuickCleanColliders();
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("🔍 Kiểm Tra Kết Nối RCC & Gameplay References", GUILayout.Height(30)))
            {
                QuickVerifyAll();
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Executes mesh synchronization from FBX into the given prefab.
        /// </summary>
        public static bool ExecuteSync(string prefabPath, string modelPath, string matFolder)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"[VehicleMeshSyncTool] Không tìm thấy Model tại: {modelPath}");
                return false;
            }

            if (!File.Exists(prefabPath))
            {
                Debug.LogError($"[VehicleMeshSyncTool] Không tìm thấy Prefab tại: {prefabPath}");
                return false;
            }

            // Backup prefab
            try
            {
                File.Copy(prefabPath, prefabPath + ".bak", true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VehicleMeshSyncTool] Không thể tạo file backup: {ex.Message}");
            }

            // Load materials
            var materialMap = LoadMaterialMap(matFolder);
            Debug.Log($"[VehicleMeshSyncTool] Đã tải {materialMap.Count} materials từ '{matFolder}'");

            // Extract all mesh filters and renderers from model
            var modelMeshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            var modelDataDict = new Dictionary<string, ModelMeshData>();

            foreach (var mf in modelMeshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                string goName = mf.gameObject.name;

                var mr = mf.GetComponent<MeshRenderer>();
                var matNames = new List<string>();
                if (mr != null)
                {
                    foreach (var m in mr.sharedMaterials)
                        matNames.Add(m != null ? m.name : "None");
                }

                modelDataDict[goName] = new ModelMeshData
                {
                    name = goName,
                    mesh = mf.sharedMesh,
                    localPos = mf.transform.localPosition,
                    localRot = mf.transform.localRotation,
                    localScale = mf.transform.localScale,
                    materialNames = matNames
                };
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            int updatedCount = 0;
            int addedCount = 0;
            int removedColliderCount = 0;

            try
            {
                // Locate the visual container inside prefab
                Transform visualContainer = FindVisualContainer(prefabRoot.transform);
                if (visualContainer == null)
                {
                    Debug.LogError($"[VehicleMeshSyncTool] Không xác định được visual container trong Prefab '{prefabPath}'!");
                    return false;
                }

                // Map existing GameObjects under visual container
                var existingVisualGOs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
                CollectVisualGameObjects(visualContainer, existingVisualGOs);

                // 1. Process all sub-meshes from model
                foreach (var kvp in modelDataDict)
                {
                    string meshName = kvp.Key;
                    ModelMeshData mData = kvp.Value;

                    if (existingVisualGOs.TryGetValue(meshName, out GameObject existingGO))
                    {
                        // GameObject already exists: update MeshFilter
                        var mf = existingGO.GetComponent<MeshFilter>();
                        if (mf == null) mf = existingGO.AddComponent<MeshFilter>();

                        if (mf.sharedMesh != mData.mesh)
                        {
                            mf.sharedMesh = mData.mesh;
                            EditorUtility.SetDirty(mf);
                            updatedCount++;
                        }

                        // Ensure MeshRenderer exists
                        var mr = existingGO.GetComponent<MeshRenderer>();
                        if (mr == null) mr = existingGO.AddComponent<MeshRenderer>();

                        // Check & update materials if empty or missing
                        AssignMaterials(mr, mData.materialNames, materialMap);

                        // Ensure correct Layer
                        int targetLayer = meshName.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) >= 0
                            ? LayerP2PScreen
                            : LayerRccVehicle;
                        if (existingGO.layer != targetLayer)
                        {
                            existingGO.layer = targetLayer;
                            EditorUtility.SetDirty(existingGO);
                        }

                        // Clean any accidental collider on this visual mesh
                        removedColliderCount += RemoveColliders(existingGO);
                    }
                    else
                    {
                        // Brand new or broken-off mesh: create GameObject under visual container
                        GameObject newGO = new GameObject(meshName);
                        newGO.transform.SetParent(visualContainer, false);
                        newGO.transform.localPosition = mData.localPos;
                        newGO.transform.localRotation = mData.localRot;
                        newGO.transform.localScale = mData.localScale;

                        var mf = newGO.AddComponent<MeshFilter>();
                        mf.sharedMesh = mData.mesh;

                        var mr = newGO.AddComponent<MeshRenderer>();
                        AssignMaterials(mr, mData.materialNames, materialMap);

                        int targetLayer = meshName.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) >= 0
                            ? LayerP2PScreen
                            : LayerRccVehicle;
                        newGO.layer = targetLayer;

                        existingVisualGOs[meshName] = newGO;
                        addedCount++;
                        Debug.Log($"<color=cyan>[VehicleMeshSyncTool] + Đã thêm sub-mesh mới:</color> '{meshName}' vào '{visualContainer.name}'");
                    }
                }

                // 2. Warn about any GameObjects in Prefab whose mesh no longer exists in model
                foreach (var kvp in existingVisualGOs)
                {
                    string goName = kvp.Key;
                    GameObject go = kvp.Value;
                    if (go == null) continue;

                    // Skip known non-mesh containers or utility objects
                    if (goName == "COM" || goName == "Door_Driver" || goName == "Wheel Colliders" ||
                        goName.StartsWith("RCC_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null && !modelDataDict.ContainsKey(goName))
                    {
                        Debug.LogWarning($"[VehicleMeshSyncTool] ⚠️ Sub-mesh '{goName}' trong Prefab không còn tồn tại trong model FBX. Có thể mesh đã bị đổi tên, chia tách hoặc xóa.");
                    }
                }

                // 3. Ensure visual meshes have ZERO colliders
                removedColliderCount += CleanVisualCollidersInHierarchy(visualContainer);

                // Save modified prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log($"<color=green><b>[VehicleMeshSyncTool] THÀNH CÔNG!</b></color> Prefab '{Path.GetFileName(prefabPath)}': " +
                          $"Cập nhật {updatedCount} meshes, Thêm mới {addedCount} meshes, Xóa {removedColliderCount} colliders thừa.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VehicleMeshSyncTool] Lỗi trong quá trình đồng bộ: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Finds the visual container transform. If child 'The_Last_Drive_Car' exists, returns it; otherwise returns root.
        /// </summary>
        public static Transform FindVisualContainer(Transform root)
        {
            Transform child = root.Find("The_Last_Drive_Car");
            if (child != null) return child;

            // If root itself is named The_Last_Drive_Car
            if (root.name.Equals("The_Last_Drive_Car", StringComparison.OrdinalIgnoreCase))
                return root;

            return root;
        }

        private static void CollectVisualGameObjects(Transform current, Dictionary<string, GameObject> dict)
        {
            if (current == null) return;
            if (!dict.ContainsKey(current.gameObject.name))
                dict[current.gameObject.name] = current.gameObject;

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                // Don't recurse into Wheel Colliders physics hierarchy
                if (child.name.Equals("Wheel Colliders", StringComparison.OrdinalIgnoreCase))
                    continue;

                CollectVisualGameObjects(child, dict);
            }
        }

        private static void AssignMaterials(MeshRenderer mr, List<string> originalMatNames, Dictionary<string, Material> materialMap)
        {
            if (mr == null || originalMatNames == null || originalMatNames.Count == 0) return;

            Material[] currentMats = mr.sharedMaterials;
            Material[] newMats = new Material[originalMatNames.Count];
            bool changed = false;

            for (int i = 0; i < originalMatNames.Count; i++)
            {
                string matName = originalMatNames[i];
                if (materialMap.TryGetValue(matName, out Material targetMat))
                {
                    newMats[i] = targetMat;
                    changed = true;
                }
                else
                {
                    newMats[i] = (currentMats != null && i < currentMats.Length) ? currentMats[i] : null;
                }
            }

            if (changed || currentMats == null || currentMats.Length != newMats.Length)
            {
                mr.sharedMaterials = newMats;
                EditorUtility.SetDirty(mr);
            }
        }

        private static Dictionary<string, Material> LoadMaterialMap(string folderPath)
        {
            var map = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && !map.ContainsKey(mat.name))
                {
                    map[mat.name] = mat;
                }
            }
            return map;
        }

        private static int RemoveColliders(GameObject go)
        {
            int count = 0;
            var colliders = go.GetComponents<Collider>();
            foreach (var col in colliders)
            {
                // Never remove WheelCollider
                if (col is WheelCollider) continue;
                Undo.DestroyObjectImmediate(col);
                count++;
            }
            return count;
        }

        private static int CleanVisualCollidersInHierarchy(Transform container)
        {
            int count = 0;
            // Scan all children of visual container
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                count += RemoveColliders(child.gameObject);
                count += CleanVisualCollidersInHierarchy(child);
            }
            return count;
        }

        /// <summary>
        /// Cleans all visual colliders in a prefab asset.
        /// </summary>
        public static int CleanVisualColliders(string prefabPath)
        {
            if (!File.Exists(prefabPath)) return 0;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            int cleaned = 0;

            try
            {
                Transform visualContainer = FindVisualContainer(prefabRoot.transform);
                if (visualContainer != null)
                {
                    cleaned = CleanVisualCollidersInHierarchy(visualContainer);
                    if (cleaned > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        Debug.Log($"[VehicleMeshSyncTool] Đã xóa {cleaned} collider thừa trong '{Path.GetFileName(prefabPath)}'.");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return cleaned;
        }

        /// <summary>
        /// Audits RCC and gameplay references in the given prefab.
        /// </summary>
        public static string VerifyVehicleIntegrity(string prefabPath)
        {
            if (!File.Exists(prefabPath)) return "❌ Không tìm thấy file prefab!";

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            var report = new System.Text.StringBuilder();

            try
            {
                var rcc = prefabRoot.GetComponent<RCC_CarControllerV4>();
                if (rcc == null)
                {
                    report.AppendLine("❌ Thiếu RCC_CarControllerV4 trên Root!");
                }
                else
                {
                    report.AppendLine("✅ RCC_CarControllerV4: Có mặt trên Root");

                    // Wheels
                    bool fl = rcc.FrontLeftWheelTransform != null;
                    bool fr = rcc.FrontRightWheelTransform != null;
                    bool rl = rcc.RearLeftWheelTransform != null;
                    bool rr = rcc.RearRightWheelTransform != null;

                    if (fl && fr && rl && rr)
                        report.AppendLine("✅ RCC Wheel Transforms: Đủ 4 bánh (FL, FR, RL, RR)");
                    else
                        report.AppendLine($"⚠️ RCC Wheel Transforms thiếu: FL:{(fl ? "OK" : "MẤT")}, FR:{(fr ? "OK" : "MẤT")}, RL:{(rl ? "OK" : "MẤT")}, RR:{(rr ? "OK" : "MẤT")}");

                    // Steering Wheel
                    if (rcc.SteeringWheel != null)
                        report.AppendLine($"✅ Steering Wheel: Đã gán '{rcc.SteeringWheel.name}' (Xoay: {rcc.steeringWheelRotateAround})");
                    else
                        report.AppendLine("⚠️ Steering Wheel: Chưa gán transform vô lăng!");

                    // COM
                    if (rcc.COM != null)
                        report.AppendLine($"✅ Center of Mass (COM): Đã gán '{rcc.COM.name}'");
                    else
                        report.AppendLine("⚠️ Center of Mass (COM): Chưa gán!");

                    // Hood Camera
                    var hoodCam = rcc.GetComponentInChildren<RCC_HoodCamera>(true);
                    if (hoodCam != null)
                        report.AppendLine($"✅ Hood Camera: Đã gán '{hoodCam.name}'");
                    else
                        report.AppendLine("ℹ️ Hood Camera: Không có");
                }

                // Check Wheel Colliders hierarchy
                Transform wcParent = prefabRoot.transform.Find("Wheel Colliders");
                if (wcParent != null)
                {
                    var wcs = wcParent.GetComponentsInChildren<RCC_WheelCollider>(true);
                    report.AppendLine($"✅ RCC_WheelCollider: Tìm thấy {wcs.Length} component dưới 'Wheel Colliders'");
                    foreach (var wc in wcs)
                    {
                        string modelInfo = wc.wheelModel ? wc.wheelModel.name : "CHƯA GÁN";
                        report.AppendLine($"   • {wc.gameObject.name} -> wheelModel: {modelInfo}");
                    }
                }
                else
                {
                    report.AppendLine("⚠️ Không tìm thấy GameObject 'Wheel Colliders'!");
                }

                // Check Chassis Collider
                Transform visualContainer = FindVisualContainer(prefabRoot.transform);
                var rootCols = prefabRoot.GetComponents<Collider>();
                var containerCols = visualContainer != null ? visualContainer.GetComponents<Collider>() : new Collider[0];
                report.AppendLine($"✅ Chassis Collider: {rootCols.Length} trên Root, {containerCols.Length} trên {visualContainer?.name}");

                // Check child colliders (Zero-Child-Collider rule)
                int illegalColliders = 0;
                if (visualContainer != null)
                {
                    for (int i = 0; i < visualContainer.childCount; i++)
                    {
                        illegalColliders += CountCollidersRecursive(visualContainer.GetChild(i));
                    }
                }

                if (illegalColliders == 0)
                    report.AppendLine("✅ Zero-Child-Collider: Tuyệt đối an toàn (0 collider thừa trên visual meshes)");
                else
                    report.AppendLine($"❌ Zero-Child-Collider VI PHẠM: Phát hiện {illegalColliders} collider thừa trên visual meshes! Hãy bấm 'Quét & Xóa Collider Thừa'.");

                // Check Door_Driver for VehicleCharacterManager
                Transform door = prefabRoot.transform.Find("Door_Driver");
                if (door == null && visualContainer != null) door = visualContainer.Find("Door_Driver");
                if (door != null)
                    report.AppendLine($"✅ Door_Driver: Có mặt tại {door.localPosition}");
                else
                    report.AppendLine("ℹ️ Door_Driver: Chưa có điểm neo cửa lái (VehicleSceneSetup sẽ tự tạo nếu cần)");

                // Check Curve_Screen for HMI
                Transform curveScreen = visualContainer != null ? visualContainer.Find("Curve_Screen") : null;
                if (curveScreen != null)
                {
                    var mr = curveScreen.GetComponent<MeshRenderer>();
                    string matName = mr && mr.sharedMaterial ? mr.sharedMaterial.name : "Chưa gán";
                    report.AppendLine($"✅ Curve_Screen: Có mặt (Layer: {curveScreen.gameObject.layer}, Mat: {matName})");
                }
                else
                {
                    report.AppendLine("ℹ️ Curve_Screen: Không tìm thấy dưới visual container");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return report.ToString();
        }

        private static int CountCollidersRecursive(Transform t)
        {
            int count = 0;
            var cols = t.GetComponents<Collider>();
            foreach (var col in cols)
            {
                if (!(col is WheelCollider)) count++;
            }
            for (int i = 0; i < t.childCount; i++)
            {
                count += CountCollidersRecursive(t.GetChild(i));
            }
            return count;
        }

        private class ModelMeshData
        {
            public string name;
            public Mesh mesh;
            public Vector3 localPos;
            public Quaternion localRot;
            public Vector3 localScale;
            public List<string> materialNames;
        }
    }
}
