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
            manager.EnsureUIExists();
            EditorUtility.SetDirty(manager);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green><b>[HMIDisplaySetup] THÀNH CÔNG!</b></color> Hệ thống HMI Display (Canvas + Camera + RenderTexture + Material) đã được thiết lập hoàn chỉnh trong scene!");
        }
    }
}
