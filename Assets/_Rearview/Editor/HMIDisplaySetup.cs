using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Rearview.Editor
{
    public class HMIDisplaySetup : UnityEditor.Editor
    {
        private const string RT_PATH = "Assets/_Rearview/RenderTexture/P2P_Screen_RT.renderTexture";
        private const string MAT_PATH = "Assets/_Rearview/Material/Car/plasticGlossy.001.mat";

        [MenuItem("Tools/Rearview/⚡ Setup HMI Screen In Current Scene")]
        public static void SetupHMIInScene()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RT_PATH);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);

            if (rt == null)
            {
                Debug.LogError($"[HMIDisplaySetup] Không tìm thấy RenderTexture tại: {RT_PATH}");
                return;
            }

            if (mat == null)
            {
                Debug.LogError($"[HMIDisplaySetup] Không tìm thấy Material tại: {MAT_PATH}");
                return;
            }

            // Ensure material has RT assigned and emission enabled
            mat.SetTexture("_BaseMap", rt);
            mat.SetTexture("_EmissionMap", rt);
            mat.SetTexture("_MainTex", rt);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_EmissionColor", Color.white);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);

            // Find or create HMI Manager in scene
            var manager = Object.FindFirstObjectByType<HMIDisplayManager>();
            if (manager == null)
            {
                GameObject mgrObj = new GameObject("HMI_Display_Manager");
                manager = mgrObj.AddComponent<HMIDisplayManager>();
                Undo.RegisterCreatedObjectUndo(mgrObj, "Create HMI Display Manager");
            }

            manager.targetRenderTexture = rt;
            manager.screenMaterial = mat;
            // Force rebuild ensures the new AAOS App Launcher Grid & Unobstructed Cluster layout is generated
            manager.EnsureUIExists(forceRebuild: true);
            manager.SelectApp(HMIDisplayManager.AAOSApp.Launcher);
            manager.EnsureHVACScreenOff();
            manager.EnsureScreenMaterialAssigned();
            EditorUtility.SetDirty(manager);

            // Per RCC vehicle pipeline standards (rcc-vehicle-control-pipeline/SKILL.md):
            // Curve_Screen is strictly a visual display mesh and MUST NOT have any colliders attached
            // to avoid polluting the vehicle's dynamic Rigidbody compound shape in PhysX.
            var vehicle = Object.FindFirstObjectByType<RCC_CarControllerV4>();
            if (vehicle != null)
            {
                MeshRenderer[] renderers = vehicle.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in renderers)
                {
                    if (mr.gameObject.name.Equals("Curve_Screen", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Collider[] colliders = mr.GetComponents<Collider>();
                        foreach (var col in colliders)
                        {
                            Undo.DestroyObjectImmediate(col);
                        }

                        // Ensure slot 0 has the screen material assigned
                        Material[] mats = mr.sharedMaterials;
                        bool assigned = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] != null && mats[i].name.Contains("plasticGlossy.001"))
                            {
                                mats[i] = mat;
                                assigned = true;
                            }
                        }
                        if (!assigned && mats.Length > 0)
                        {
                            mats[0] = mat;
                        }
                        mr.sharedMaterials = mats;
                        EditorUtility.SetDirty(mr.gameObject);
                        break;
                    }
                }
            }

            // Immediately render camera to update RenderTexture for Scene/Game view
            if (manager.hmiCamera != null)
            {
                manager.hmiCamera.Render();
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green><b>[HMIDisplaySetup] THÀNH CÔNG!</b></color> Hệ thống AAOS HMI Display (Unobstructed Cluster + App Grid Launcher + HVAC Screen Off) đã được gắn trực tiếp lên Mesh và Render hoàn chỉnh!");
        }

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying)
                {
                    SetupHMIInScene();
                }
            };
        }
    }
}
