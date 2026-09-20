---
name: hap-character-interaction-pipeline
description: >-
  Standard architecture and implementation guidelines for humanoid character control,
  Mixamo humanoid rigging, IK look-at, and world interaction using Malbers Animations
  Horse AnimSet Pro (HAP) & Animal Controller in The Last Waypoint. Use this skill
  whenever configuring characters, interaction triggers, camera rigs, or diagnosing TPS issues.
---

# HAP Character & Interaction Pipeline Standards

This skill defines the architectural standards, component setups, and interaction patterns for third-person (TPS) humanoid characters using **Malbers Animations Horse AnimSet Pro (HAP)** and **Animal Controller** in *The Last Waypoint*.

---

## 1. HAP Architecture in the Project

The third-person character system handles on-foot exploration, item inspection, talking to NPCs, and entering/exiting the vehicle.

```mermaid
flowchart TD
    subgraph PlayerRoot["Player_YBot (Root GameObject)"]
        MAnimal["MAnimal (Physics, Movement, Ground Check)"]
        MRider["MRider (Equip points, Mount/Interact)"]
        MInteractor["MInteractor (Sphere/Raycast Trigger)"]
        IKManager["IKManager (IKGenericLookAt Processors)"]
        Animator["Animator (Mixamo Humanoid Avatar)"]
        PlayerCore["Player Core (Hooks & Camera Targets)"]
    end

    subgraph CameraSystem["Cameras CM3 Rig"]
        CinemachineBrain["CinemachineBrain"]
        FollowTarget["ThirdPersonFollowTarget"]
    end

    subgraph WorldInteractable["World Objects / Vehicle"]
        IInteractable["IInteractable / MInteract ([E] Trigger)"]
        VehicleMgr["VehicleCharacterManager (Enter/Exit)"]
    end

    PlayerCore -->|Camera Target Hook (TransformVar)| FollowTarget
    MInteractor -->|Inspect / Trigger [E]| IInteractable
    IInteractable --> VehicleMgr
```

Key reference implementations:
- [VehicleCharacterManager.cs](file:///Users/toanlb/toanlb_game/Rearview/Assets/_Rearview/Scripts/VehicleCharacterManager.cs)
- [YBotCharacterReplacer.cs](file:///Users/toanlb/toanlb_game/Rearview/Assets/_Rearview/Editor/YBotCharacterReplacer.cs)
- [DiagnoseCharacters.cs](file:///Users/toanlb/toanlb_game/Rearview/Assets/_Rearview/Editor/DiagnoseCharacters.cs)

---

## 2. Character Rigging & The Mixamo Humanoid Standard

### 2.1 Model Import Settings
- All player and NPC humanoid models (e.g. `Y_Bot.fbx`) MUST be configured as:
  - `ModelImporter.animationType = ModelImporterAnimationType.Human`
  - `ModelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel`

### 2.2 Bone Hierarchy
Mixamo humanoid skeletons follow standard naming conventions:
- Root Hip: `mixamorig:Hips`
- Spine / Chest: `mixamorig:Spine`, `mixamorig:Spine1`, `mixamorig:Spine2`
- Neck & Head: `mixamorig:Neck`, `mixamorig:Head`
- Arms & Hands: `mixamorig:LeftHand`, `mixamorig:RightHand`

### 2.3 The IK Generic LookAt Zero-Offset Rule
> [!CAUTION]
> **CRITICAL BONE ORIENTATION DIFFERENCE:**
> The original Malbers Cowboy model was rigged in 3ds Max, which uses an IK offset of `(0, -90, -90)` in `IKGenericLookAt`.
> Applying `(0, -90, -90)` to a **Mixamo** humanoid avatar causes the head/neck to twist violently backward or snap 90 degrees downward!
> 
> **MANDATORY FIX FOR ALL MIXAMO CHARACTERS:**
> ```csharp
> foreach (var p in ikSet.IKProcesors)
> {
>     if (p is IKGenericLookAt genericLookAt)
>     {
>         genericLookAt.Offset = Vector3.zero; // MUST BE (0, 0, 0)
>     }
> }
> ```

---

## 3. Essential Malbers Components Configuration

### 3.1 `MAnimal` Component Setup
```csharp
animal.Anim = animator;
animal.MainCollider = GetComponent<CapsuleCollider>();
animal.RB = GetComponent<Rigidbody>();
animal.height = 1.5f;
animal.m_pivotMultiplier = 1.5f;
animal.Has_Pivot_Chest = true;
animal.Has_Pivot_Hip = false;

// Ensure Pivot_Chest is aligned properly with the chest height
if (animal.Pivot_Chest != null)
{
    animal.Pivot_Chest.position = new Vector3(0f, 1.5f, 0f);
    animal.Pivot_Chest.name = "Chest";
}
```

### 3.2 `Player Core` & Cinemachine CM3 Camera Target
The camera rig (`Cameras CM3`) tracks the character through a ScriptableObject hook:
- Hook Asset: `Assets/Malbers Animations/Common/Scriptable Assets/Hooks/Camera Target.asset` (`TransformVar`)
- `Player Core/CM Main Target` has an `AnimalTracker` and `TransformHook` pointing to this asset:
  ```csharp
  var hook = cmMainTarget.GetComponent<TransformHook>();
  hook.Reference = cmMainTarget;
  hook.Hook = AssetDatabase.LoadAssetAtPath<TransformVar>("Assets/Malbers Animations/Common/Scriptable Assets/Hooks/Camera Target.asset");
  hook.Hook.Value = cmMainTarget;
  ```
- `Cameras CM3` has `ThirdPersonFollowTarget` set to:
  - `Target.Variable = cameraTargetVar`
  - `Target.UseConstant = false`

---

## 4. Interaction System & `IInteractable` Pattern

To make any object or vehicle interactable with the player's `MInteractor`:

### 4.1 Implementing `IInteractable`
```csharp
using MalbersAnimations;

public class WorldInteractableItem : MonoBehaviour, IInteractable
{
    public GameObject Owner => gameObject;
    public int Index => 0;
    public bool Active { get => isEnabled; set => isEnabled = value; }
    public bool SingleInteraction => false;
    public bool Auto => false;
    public bool Focused { get; set; }

    public void Focus(IInteractor focuser)
    {
        Focused = true;
        // Show prompt / outline highlight
    }

    public void UnFocus(IInteractor focuser)
    {
        Focused = false;
        // Hide prompt / outline highlight
    }

    public bool Interact(IInteractor interactor)
    {
        ExecuteInteraction();
        return true;
    }

    public bool Interact(int interactorID, GameObject interactor)
    {
        ExecuteInteraction();
        return true;
    }

    public void Interact() => ExecuteInteraction();
    public void Restart() => Focused = false;

    private void ExecuteInteraction()
    {
        // Dialogue trigger, item pickup, or vehicle entry logic
    }
}
```

### 4.2 Dual Input Support (New & Legacy Input System)
Always support both input paths to prevent input lockups:
```csharp
private bool WasInteractPressed()
{
#if ENABLE_INPUT_SYSTEM
    if (UnityEngine.InputSystem.Keyboard.current != null && 
        UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
    if (Input.GetKeyDown(KeyCode.E))
        return true;
#endif
    return false;
}
```

---

## 5. Verification & Diagnosis Checklist

When creating or modifying character prefabs or interaction points:
1. Run diagnostic menu: `Tools > Rearview > Diagnose Characters`.
2. Inspect `Animator`: `isHuman == true`, `avatar.isValid == true`.
3. Verify `IKManager`: All `IKGenericLookAt` processors have `Offset == (0, 0, 0)`.
4. Ground Check: Ensure `MAnimal.height == 1.5f` and `CapsuleCollider.center.y ≈ 0.9f` to prevent the character from floating or falling through terrain.
5. Camera Rig: Verify `Cameras CM3` follow target is assigned to `Camera Target` hook.
6. Interaction: Walk up to target within `3.5m`, check that prompt appears and pressing `[E]` triggers interaction.
