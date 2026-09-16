---
name: blender-unity-vehicle-pipeline
description: >-
  Standard specifications, checklist, and export guidelines for vehicle 3D models in Blender
  prior to exporting to Unity Realistic Car Controller (RCC). Use this skill whenever creating,
  modifying, rigging, reviewing, or exporting vehicle models between Blender and Unity.
---

# Blender to Unity RCC Vehicle Pipeline Standards

This skill defines the mandatory technical specifications, coordinate conventions, and export procedures for any vehicle 3D model in Blender to ensure seamless, error-free integration with **Unity** and the **Realistic Car Controller (RCC)** asset.

---

## 1. The 5 Golden Rules for Vehicle Models

### Rule 1: Root Object Transform & Hierarchy
- The vehicle MUST have a single root object (Empty or Top GameObject, e.g. `The_Last_Drive_Car`).
- **Position:** `(0.0, 0.0, 0.0)` — centered between front/rear axles and left/right track.
- **Rotation:** `(0.0, 0.0, 0.0)` — **CRITICAL:** Root must have ZERO rotation offset.
- **Scale:** `(1.0, 1.0, 1.0)`.
- All body panels, lights, glass, screens, interior, and wheels must be direct or indirect children of this root.

### Rule 2: Axis & Coordinate Conventions (Unity Native)
In the vehicle's local space (and in Unity):
| Direction | Unity Axis | Coordinate Value | Notes |
| :--- | :--- | :--- | :--- |
| **Forward** | `+Z` | `Z > 0` | Front bumper, headlights |
| **Backward**| `-Z` | `Z < 0` | Trunk, rear bumper, taillights |
| **Up** | `+Y` | `Y > 0` | Roof, headrests |
| **Down** | `-Y` | `Y < 0` | Floor, wheels, road contact |
| **Left** | `-X` | `X < 0` | Driver side (Left-Hand Drive) |
| **Right** | `+X` | `X > 0` | Passenger side (Left-Hand Drive) |

> [!CAUTION]
> If headlights point to `-Z` instead of `+Z`:
> - RCC's auto-detection will swap front wheels with rear wheels!
> - Steering will steer the rear wheels instead of front wheels!
> - Pressing `W` (Drive) will drive the vehicle backwards trunk-first!

### Rule 3: 4 Separated Wheel Models
- The 4 wheels MUST be separate mesh objects named exactly:
  - **`Wheel_FL`** (Front Left): `X < 0`, `Z > 0`
  - **`Wheel_FR`** (Front Right): `X > 0`, `Z > 0`
  - **`Wheel_RL`** (Rear Left): `X < 0`, `Z < 0`
  - **`Wheel_RR`** (Rear Right): `X > 0`, `Z < 0`
- **Wheel Pivot (Origin):** MUST be centered precisely at the geometric center of the wheel cylinder (`local bbox center ≈ (0, 0, 0)`).
- **Scale:** MUST be `(1.0, 1.0, 1.0)`.

### Rule 4: Lightweight Collision Hull (`Car_Collider`)
- Unity PhysX strictly limits `Convex MeshCollider` to **<= 255 triangles**.
- High-poly body meshes (50k+ vertices) will crash or fail convex cooking in RCC Wizard.
- Always include a dedicated simplified mesh named **`Car_Collider`** (8–30 vertices, ~12–20 triangles) covering the chassis and cabin.
- Set `display_type = 'WIRE'` in Blender so it does not obstruct modeling view.

### Rule 5: Non-Destructive Shape Keys (BlendShapes)
- Aerodynamic styling variants (e.g., SUV to Coupe fastback morph) should be implemented using **Shape Keys** (`Basis` = 0.0, `Coupe_Style` = 1.0).
- When meshes have active shape keys, avoid running global destructive `transform_apply` on mesh data in Edit mode without transforming key block offsets.

---

## 2. Standard Blender FBX Export Settings

When exporting the vehicle model to FBX for Unity RCC, use the following exact parameters in `bpy.ops.export_scene.fbx`:

```python
bpy.ops.export_scene.fbx(
    filepath="/path/to/Assets/Mesh/Vehicle_Name.fbx",
    use_selection=True,
    axis_forward='Z',       # Native Unity forward
    axis_up='Y',            # Native Unity up
    bake_space_transform=False,
    apply_scale_options='FBX_SCALE_ALL'
)
```

> [!IMPORTANT]
> Do NOT use `axis_forward='-Z'` with `bake_space_transform=True` when the vehicle is already oriented to `Y-Up, Z-Forward`, as this will pitch the vehicle 90° upright onto its bumper in Unity!

---

## 3. Pre-Export Verification Checklist

Before exporting any vehicle model, execute the validation script in Blender:
- Script location: [validate_car_model.py](./scripts/validate_car_model.py)

```python
import sys
sys.path.append("/path/to/.agents/skills/blender-unity-vehicle-pipeline/scripts")
import validate_car_model
validate_car_model.validate_vehicle("Your_Vehicle_Root_Name")
```

The script verifies:
1. `Root.rotation == (0, 0, 0)` and `Root.scale == (1, 1, 1)`.
2. All 4 wheels exist with correct naming (`Wheel_FL`, `Wheel_FR`, `Wheel_RL`, `Wheel_RR`).
3. Signs of wheel coordinates match Unity standards (`FL`: $-X, +Z$; `FR`: $+X, +Z$; `RL`: $-X, -Z$; `RR`: $+X, -Z$).
4. Wheel pivots are centered at `(0, 0, 0)` in local space.
5. Headlights are at $+Z$ and tailgate is at $-Z$.
6. `Car_Collider` exists with triangle count $\le 255$.

---

## 4. Unity Post-Import Workflow (RCC Setup)

1. Drag the exported `.fbx` into the Scene.
2. Verify Transform: `Position: (0, 0, 0)`, `Rotation: (0, 0, 0)`, `Scale: (1, 1, 1)`.
3. Right click -> **Prefab > Unpack Completely**.
4. Open RCC Quick Setup Wizard:
   - **Fix pivot prompt:** Select **"NO"** (pivot is already centered at ground origin).
   - **Front wheels:** Select `Wheel_FL` and `Wheel_FR`.
   - **Rear wheels:** Select `Wheel_RL` and `Wheel_RR`.
   - **Body collider:** Select `Car_Collider` -> click "Add MeshCollider To Selected Body".
   - **Finish!**
5. Save the configured GameObject as a **Prefab Variant** or **Original Prefab** in `Assets/Prefabs/`. Future mesh/material edits in Blender will update automatically without re-running the Wizard.
