# validate_car_model.py
# Run this script in Blender (via Python console or MCP execute_blender_code)
# to validate whether a car model meets all Unity & RCC standards before exporting.

import bpy

def validate_vehicle(root_name="The_Last_Drive_Car"):
    print("=" * 60)
    print(f"VALIDATING VEHICLE MODEL: '{root_name}'")
    print("=" * 60)
    
    root = bpy.data.objects.get(root_name)
    if not root:
        print(f"[FAIL] Root object '{root_name}' not found in Blender scene!")
        return False

    passed = True
    
    # 1. Root Transforms
    loc = root.location
    rot = root.rotation_euler
    scale = root.scale
    
    if abs(rot.x) > 0.001 or abs(rot.y) > 0.001 or abs(rot.z) > 0.001:
        print(f"[FAIL] Root rotation must be (0, 0, 0). Current: ({rot.x:.3f}, {rot.y:.3f}, {rot.z:.3f})")
        passed = False
    else:
        print("[PASS] Root rotation is (0, 0, 0).")
        
    if abs(scale.x - 1.0) > 0.001 or abs(scale.y - 1.0) > 0.001 or abs(scale.z - 1.0) > 0.001:
        print(f"[FAIL] Root scale must be (1, 1, 1). Current: ({scale.x:.3f}, {scale.y:.3f}, {scale.z:.3f})")
        passed = False
    else:
        print("[PASS] Root scale is (1, 1, 1).")

    # 2. Check 4 Wheels
    required_wheels = ["Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR"]
    for w_name in required_wheels:
        w = bpy.data.objects.get(w_name)
        if not w:
            print(f"[FAIL] Missing wheel object '{w_name}'!")
            passed = False
            continue
        
        w_loc = w.location
        # FL: X < 0, Z > 0
        # FR: X > 0, Z > 0
        # RL: X < 0, Z < 0
        # RR: X > 0, Z < 0
        err = []
        if "FL" in w_name:
            if w_loc.x >= 0: err.append("X should be negative (Left)")
            if w_loc.z <= 0: err.append("Z should be positive (Front)")
        elif "FR" in w_name:
            if w_loc.x <= 0: err.append("X should be positive (Right)")
            if w_loc.z <= 0: err.append("Z should be positive (Front)")
        elif "RL" in w_name:
            if w_loc.x >= 0: err.append("X should be negative (Left)")
            if w_loc.z >= 0: err.append("Z should be negative (Rear)")
        elif "RR" in w_name:
            if w_loc.x <= 0: err.append("X should be positive (Right)")
            if w_loc.z >= 0: err.append("Z should be negative (Rear)")
            
        if err:
            print(f"[FAIL] {w_name} at loc=({w_loc.x:.3f}, {w_loc.y:.3f}, {w_loc.z:.3f}): {', '.join(err)}")
            passed = False
        else:
            print(f"[PASS] {w_name} position is correct ({w_loc.x:.3f}, {w_loc.y:.3f}, {w_loc.z:.3f}).")

        # Check wheel pivot centering
        bbox = w.bound_box
        center_local = [sum(corner[i] for corner in bbox) / 8.0 for i in range(3)]
        if any(abs(c) > 0.02 for c in center_local):
            print(f"[FAIL] {w_name} pivot is off-center in local space: {center_local}")
            passed = False
        else:
            print(f"[PASS] {w_name} pivot is centered.")

    # 3. Check Headlights vs Tailgate Orientation
    headlight = None
    backdoor = None
    for child in root.children:
        c_low = child.name.lower()
        if "headlight" in c_low or "front" in c_low:
            headlight = child
        if "backdoor" in c_low or "trunk" in c_low or "tail" in c_low:
            backdoor = child

    if headlight and backdoor:
        if headlight.location.z <= backdoor.location.z:
            print(f"[FAIL] Vehicle orientation reversed! Headlight Z ({headlight.location.z:.2f}) <= Backdoor Z ({backdoor.location.z:.2f}). Front must have higher Z.")
            passed = False
        else:
            print(f"[PASS] Vehicle forward direction is correct (+Z = Front, -Z = Rear).")

    # 4. Check Collider Mesh
    collider = bpy.data.objects.get("Car_Collider")
    if not collider:
        print("[WARN] 'Car_Collider' object not found. RCC Wizard will require manually adding BoxCollider in Unity.")
    else:
        vert_count = len(collider.data.vertices)
        if vert_count > 100:
            print(f"[WARN] 'Car_Collider' has {vert_count} vertices. PhysX convex colliders must have <= 255 triangles.")
        else:
            print(f"[PASS] 'Car_Collider' present with lightweight geometry ({vert_count} verts).")

    print("-" * 60)
    if passed:
        print("[RESULT] ALL CHECKS PASSED! The model is 100% ready for export to Unity RCC.")
    else:
        print("[RESULT] VERIFICATION FAILED. Please fix the errors above before exporting.")
    print("=" * 60)
    return passed

if __name__ == "__main__":
    validate_vehicle()
