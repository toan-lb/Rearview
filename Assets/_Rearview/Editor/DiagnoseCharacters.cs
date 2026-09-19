using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using MalbersAnimations.Controller;

namespace Rearview.Editor
{
    public static class DiagnoseCharacters
    {
        [MenuItem("Tools/Rearview/Diagnose Characters")]
        public static void Diagnose()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== CHARACTER DIAGNOSIS ===");

            // 1. Find Cowboy (Backup) (including inactive)
            GameObject cowboy = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "Cowboy (Backup)" && go.scene.isLoaded)
                {
                    cowboy = go;
                    break;
                }
            }
            if (cowboy != null)
            {
                sb.AppendLine("\n--- COWBOY (BACKUP) ---");
                DiagnoseObject(cowboy, sb);
            }
            else
            {
                sb.AppendLine("\nCowboy (Backup) not found in scene!");
            }

            // 2. Find Player_YBot (including inactive)
            GameObject ybot = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "Player_YBot" && go.scene.isLoaded)
                {
                    ybot = go;
                    break;
                }
            }
            if (ybot != null)
            {
                sb.AppendLine("\n--- PLAYER_YBOT ---");
                DiagnoseObject(ybot, sb);
            }
            else
            {
                sb.AppendLine("\nPlayer_YBot not found in scene!");
            }

            // 3. Find Cameras CM3
            GameObject camObj = GameObject.Find("Cameras CM3");
            if (camObj != null)
            {
                sb.AppendLine("\n--- CAMERAS CM3 ---");
                var follow = camObj.GetComponentInChildren<MalbersAnimations.ThirdPersonFollowTarget>();
                if (follow != null)
                {
                    string varName = follow.Target != null && follow.Target.Variable != null ? follow.Target.Variable.name : "null";
                    string valName = "null";
                    try { valName = follow.Target != null && follow.Target.Value != null ? follow.Target.Value.name : "null"; } catch {}
                    sb.AppendLine($"ThirdPersonFollowTarget found: Target.Variable={varName}, Target.Value={valName}");
                }
                else
                {
                    sb.AppendLine("No ThirdPersonFollowTarget component found in Cameras CM3 children.");
                }
            }

            string outPath = "/Users/toanlb/.gemini/antigravity-cli/brain/fea23057-22c5-4a77-8333-88eca8352319/scratch/diagnosis.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[DiagnoseCharacters] Diagnosis written to {outPath}");
        }

        private static void DiagnoseObject(GameObject go, StringBuilder sb)
        {
            sb.AppendLine($"Name: {go.name}, Tag: {go.tag}, Layer: {go.layer} ({LayerMask.LayerToName(go.layer)})");
            sb.AppendLine($"Position: {go.transform.position}, Rotation: {go.transform.eulerAngles}, LocalScale: {go.transform.localScale}");

            sb.AppendLine("Root Components:");
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null)
                {
                    sb.AppendLine($"  - [{c.GetType().Name}] (Enabled: {(c is Behaviour b ? b.enabled.ToString() : "N/A")})");
                    // Check serialized references to child transforms/bones
                    var so = new SerializedObject(c);
                    var sp = so.GetIterator();
                    while (sp.NextVisible(true))
                    {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference && sp.objectReferenceValue != null)
                        {
                            if (sp.objectReferenceValue is Transform tf && tf.IsChildOf(go.transform))
                            {
                                sb.AppendLine($"      {sp.propertyPath} -> Transform: {tf.name} (path: {AnimationUtility.CalculateTransformPath(tf, go.transform)})");
                            }
                            else if (sp.objectReferenceValue is GameObject g && g.transform.IsChildOf(go.transform))
                            {
                                sb.AppendLine($"      {sp.propertyPath} -> GameObject: {g.name} (path: {AnimationUtility.CalculateTransformPath(g.transform, go.transform)})");
                            }
                            else if (sp.objectReferenceValue is Component comp && comp.transform.IsChildOf(go.transform))
                            {
                                sb.AppendLine($"      {sp.propertyPath} -> Component {comp.GetType().Name} on {comp.name} (path: {AnimationUtility.CalculateTransformPath(comp.transform, go.transform)})");
                            }
                        }
                    }
                }
            }

            // Animator
            var anim = go.GetComponent<Animator>();
            if (anim != null)
            {
                sb.AppendLine($"Animator: avatar={anim.avatar?.name}, isHuman={anim.isHuman}, isValid={anim.avatar?.isValid}, controller={anim.runtimeAnimatorController?.name}, applyRootMotion={anim.applyRootMotion}");
                if (anim.avatar != null && anim.isHuman)
                {
                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    var chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                    var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                    var head = anim.GetBoneTransform(HumanBodyBones.Head);
                    sb.AppendLine($"  Hips bone: {(hips ? hips.name + " at pos " + hips.position + " (local " + hips.localPosition + ")" : "null")}");
                    sb.AppendLine($"  Chest bone: {(chest ? chest.name + " at pos " + chest.position + " (local " + chest.localPosition + ")" : "null")}");
                    sb.AppendLine($"  Neck bone: {(neck ? $"{neck.name} rot={neck.eulerAngles} fwd={neck.forward} up={neck.up} right={neck.right}" : "null")}");
                    sb.AppendLine($"  Head bone: {(head ? $"{head.name} rot={head.eulerAngles} fwd={head.forward} up={head.up} right={head.right}" : "null")}");
                }
            }

            // IKManager details
            var ik = go.GetComponent<MalbersAnimations.IK.IKManager>();
            if (ik != null && ik.sets != null)
            {
                sb.AppendLine("IKManager Sets:");
                for (int i = 0; i < ik.sets.Count; i++)
                {
                    var s = ik.sets[i];
                    sb.AppendLine($"  Set [{i}] '{s.name?.Value}': active={s.active}, weight={s.FinalWeight}");
                    if (s.IKProcesors != null)
                    {
                        foreach (var p in s.IKProcesors)
                        {
                            if (p is MalbersAnimations.IK.IKGenericLookAt lookAt)
                            {
                                sb.AppendLine($"    Processor: {lookAt.name} (IKGenericLookAt), Active={lookAt.Active}, Weight={lookAt.Weight}, TargetIndex={lookAt.TargetIndex}, Offset={lookAt.Offset}");
                            }
                            else if (p != null)
                            {
                                sb.AppendLine($"    Processor: {p.name} ({p.GetType().Name}), Active={p.Active}, Weight={p.Weight}");
                            }
                        }
                    }
                }
            }

            // Collider
            var col = go.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                sb.AppendLine($"CapsuleCollider: center={col.center}, radius={col.radius}, height={col.height}, bounds={col.bounds}");
            }

            // MAnimal
            var animal = go.GetComponent<MAnimal>();
            if (animal != null)
            {
                sb.AppendLine($"MAnimal: height={animal.height}, Height={animal.Height}, ScaleFactor={animal.ScaleFactor}");
                sb.AppendLine($"  m_pivotMultiplier={animal.m_pivotMultiplier}, Pivot_Multiplier={animal.Pivot_Multiplier}");
                sb.AppendLine($"  Has_Pivot_Chest={animal.Has_Pivot_Chest}, Has_Pivot_Hip={animal.Has_Pivot_Hip}");
                sb.AppendLine($"  Pivot_Chest: pos={animal.Pivot_Chest?.position}, name={animal.Pivot_Chest?.name}");
                sb.AppendLine($"  Main_Pivot_Point={animal.Main_Pivot_Point}");
                sb.AppendLine($"  GroundLayer={animal.GroundLayer.value} (Default in layer? {(animal.GroundLayer.value & 1) != 0})");
            }

            // Children hierarchy
            sb.AppendLine("Children:");
            PrintChildren(go.transform, "  ", sb);
        }

        private static void PrintChildren(Transform t, string indent, StringBuilder sb)
        {
            foreach (Transform child in t)
            {
                sb.AppendLine($"{indent}- {child.name} (pos={child.localPosition}, rot={child.localEulerAngles}, scale={child.localScale}, layer={child.gameObject.layer})");
                var comps = child.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c != null && !(c is Transform))
                    {
                        sb.AppendLine($"{indent}    [{c.GetType().Name}]");
                        if (c is SkinnedMeshRenderer smr)
                        {
                            sb.AppendLine($"{indent}      SMR bounds={smr.bounds}, localBounds={smr.localBounds}, rootBone={smr.rootBone?.name}");
                            if (smr.sharedMesh != null)
                                sb.AppendLine($"{indent}      Mesh.bounds={smr.sharedMesh.bounds}");
                        }
                    }
                }
                if (child.childCount > 0)
                {
                    PrintChildren(child, indent + "  ", sb);
                }
            }
        }
    }
}
