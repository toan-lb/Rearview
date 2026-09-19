using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rearview.Editor
{
    public class CarMaterialAssigner : EditorWindow
    {
        private GameObject targetPrefab;
        private GameObject sourceModel;
        private DefaultAsset materialFolder;

        private const string DefaultPrefabPath = "Assets/_Rearview/Prefab/Car.prefab";
        private const string DefaultModelPath = "Assets/_Rearview/Mesh/The_Last_Drive_Car.fbx";
        private const string DefaultMatFolderPath = "Assets/_Rearview/Material/Car";

        [MenuItem("Tools/Rearview/Reassign Car Materials From Model")]
        public static void ShowWindow()
        {
            var window = GetWindow<CarMaterialAssigner>("Car Material Assigner");
            window.minSize = new Vector2(450, 320);
            window.InitDefaults();
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
            EditorGUILayout.LabelField("🚗 Car Material Auto-Assigner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Duyệt tất cả Renderer trong Prefab xe, tìm danh sách Material gốc từ Model FBX (xuất từ file .blend), " +
                "sau đó tự động gán các Material tương ứng trong thư mục Material đã extract.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target Prefab", targetPrefab, typeof(GameObject), false);
            sourceModel = (GameObject)EditorGUILayout.ObjectField("Source Model (FBX)", sourceModel, typeof(GameObject), false);
            materialFolder = (DefaultAsset)EditorGUILayout.ObjectField("Material Folder", materialFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space(15);
            if (GUILayout.Button("⚡ Gán Lại Toàn Bộ Materials Cho Prefab", GUILayout.Height(36)))
            {
                ExecuteReassignment();
            }
        }

        public static void ExecuteReassignment(string prefabPath = DefaultPrefabPath, string modelPath = DefaultModelPath, string matFolder = DefaultMatFolderPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (prefab == null)
            {
                Debug.LogError($"[CarMaterialAssigner] Không tìm thấy Prefab tại: {prefabPath}");
                return;
            }

            if (model == null)
            {
                Debug.LogError($"[CarMaterialAssigner] Không tìm thấy Model tại: {modelPath}");
                return;
            }

            var materialMap = new Dictionary<string, Material>();
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { matFolder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && !materialMap.ContainsKey(mat.name))
                {
                    materialMap[mat.name] = mat;
                }
            }
            Debug.Log($"[CarMaterialAssigner] Đã tìm thấy {materialMap.Count} materials trong '{matFolder}'");

            var modelRenderers = model.GetComponentsInChildren<Renderer>(true);
            var sourceMatDict = new Dictionary<string, List<string>>();

            foreach (var r in modelRenderers)
            {
                var matNames = new List<string>();
                foreach (var m in r.sharedMaterials)
                {
                    matNames.Add(m != null ? m.name : "None");
                }
                sourceMatDict[r.gameObject.name] = matNames;
            }

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                var prefabRenderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                int updatedCount = 0;
                int totalAssignedSlots = 0;

                foreach (var r in prefabRenderers)
                {
                    string goName = r.gameObject.name;
                    if (!sourceMatDict.TryGetValue(goName, out var originalMatNames))
                        continue;

                    var newMaterials = new Material[originalMatNames.Count];
                    bool changed = false;

                    for (int i = 0; i < originalMatNames.Count; i++)
                    {
                        string matName = originalMatNames[i];
                        if (materialMap.TryGetValue(matName, out var targetMat))
                        {
                            newMaterials[i] = targetMat;
                            totalAssignedSlots++;
                            changed = true;
                        }
                        else
                        {
                            Debug.LogWarning($"[CarMaterialAssigner] Object '{goName}' slot {i}: Không tìm thấy material '{matName}'");
                            newMaterials[i] = (i < r.sharedMaterials.Length) ? r.sharedMaterials[i] : null;
                        }
                    }

                    if (changed)
                    {
                        Undo.RecordObject(r, "Reassign Extracted Materials");
                        r.sharedMaterials = newMaterials;
                        EditorUtility.SetDirty(r);
                        updatedCount++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                Debug.Log($"<color=green><b>[CarMaterialAssigner] THÀNH CÔNG!</b></color> Đã cập nhật {updatedCount} Renderers ({totalAssignedSlots} slots) cho Prefab '{prefab.name}'!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
