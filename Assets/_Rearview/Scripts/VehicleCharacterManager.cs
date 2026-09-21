using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MalbersAnimations;
using MalbersAnimations.Utilities;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Rearview
{
    /// <summary>
    /// Manages control handover and camera switching between Malbers HAP character (on-foot)
    /// and Realistic Car Controller (RCC vehicle).
    /// Implements IInteractable so Malbers' MInteractor can trigger it directly via [E].
    /// Integrates natively with Malbers HAP Interact UI system (Interact UI.prefab & MEvent).
    /// </summary>
    [AddComponentMenu("Rearview/Vehicle Character Manager")]
    [DefaultExecutionOrder(-10)]
    public class VehicleCharacterManager : MonoBehaviour, IInteractable
    {
        public enum ControlState
        {
            OnFoot,
            InVehicle
        }

        [Header("--- Control State ---")]
        [Tooltip("Initial and current state: OnFoot (walking) or InVehicle (driving).")]
        public ControlState currentState = ControlState.OnFoot;

        [Header("--- Vehicle References (RCC) ---")]
        [Tooltip("The player car controller. Auto-found if null.")]
        public RCC_CarControllerV4 carController;

        [Tooltip("The RCC Camera controller in the scene. Auto-found if null.")]
        public RCC_Camera rccCamera;

        [Header("--- Character References (HAP) ---")]
        [Tooltip("The HAP character GameObject (e.g. Player_YBot). Auto-found if null.")]
        public GameObject character;

        [Tooltip("The Malbers Cinemachine Camera Rig (e.g. Cameras CM3). Auto-found if null.")]
        public GameObject hapCameraRig;

        [Header("--- Interaction Settings ---")]
        [Tooltip("Optional transform near the driver door for entering/exiting and UI anchor. If null, auto-created or calculated on the left of the car.")]
        public Transform doorPoint;

        [Tooltip("Maximum distance to interact with the vehicle when on foot.")]
        [Range(1f, 10f)]
        public float interactionDistance = 3.5f;

        [Tooltip("HAP MInteract component (optional). If attached to the car, it will hook into this manager.")]
        public MInteract carInteractable;

        [Header("--- HAP Interaction UI ---")]
        [Tooltip("Malbers MEvent asset for raising Interact UI (Assets/Malbers Animations/Common/Assets/Events/Extras/Interact UI.asset).")]
        public MalbersAnimations.Events.MEvent interactUIEvent;

        [Tooltip("Show simple on-screen prompt when in interaction range on foot.")]
        public bool showPrompt = true;

        [Tooltip("Prompt text when near the vehicle on foot.")]
        public string enterPromptText = "Lên Xe";

        [Header("--- In-Vehicle Prompt Settings ---")]
        [Tooltip("If true, shows exit prompt briefly upon entering vehicle. If false, completely hides prompt while driving (clean HUD).")]
        public bool showExitPrompt = false;

        [Tooltip("How long (in seconds) to show the exit prompt before auto-hiding (if showExitPrompt is true).")]
        public float exitPromptDuration = 3f;

        [Tooltip("Prompt text when driving the vehicle.")]
        public string exitPromptText = "Xuống Xe";

        [Tooltip("Cooldown in seconds between entering and exiting vehicle to prevent accidental double-triggering.")]
        public float interactCooldown = 0.5f;

        [Header("--- Enter Vehicle Animation ---")]
        [Tooltip("Animation clip of character entering the car (e.g. Assets/_Rearview/Animations/Y_Bot@Entering_Car.fbx).")]
        public AnimationClip enterCarClip;

        [Tooltip("Playback speed multiplier for enter/exit animation (1.0 = normal ~5.5s, 1.875 = ~2.9s).")]
        [Range(0.5f, 3f)]
        public float enterAnimSpeed = 1.875f;

        [Tooltip("Duration in seconds to smoothly align character to the driver door before playing animation.")]
        [Range(0.1f, 1f)]
        public float alignToDoorDuration = 0.35f;

        [Tooltip("Animation curve for pre-aligning character to the driver door.")]
        public AnimationCurve alignCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("--- Exit Transition Settings ---")]
        [Tooltip("Duration in seconds to smoothly blend from exit animation back to HAP idle locomotion (0.45s gives a silky smooth transition from feet-together to idle stance).")]
        [Range(0.1f, 1.5f)]
        public float exitTransitionDuration = 0.45f;

        [Header("--- Driver Seat & Animation Alignment ---")]
        [Tooltip("Transform of the driver seat cushion (FrontSeat_Left in Left-Hand Drive). Auto-found if null.")]
        public Transform driverSeat;

        [Tooltip("Transform of the steering wheel. Auto-found if null.")]
        public Transform steeringWheel;

        [Tooltip("Offset from driverSeat to character start position (in car local space: X = lateral left/right, Y = vertical, Z = longitudinal forward/backward). Calibrated so character localPosition.z in car is exactly 0.04.")]
        public Vector3 startOffsetFromSeat = new Vector3(-1.86f, 0f, -0.00265f);

        [Tooltip("Yaw rotation offset in degrees from car heading at start of animation (90 = facing directly into driver door).")]
        public float startYawOffset = 90f;

        [Header("--- Driver Door Animation Sync ---")]
        [Tooltip("Transform of the driver door mesh (Byton_Optimazile_LookDev_SK_FD_Left). Auto-found if null.")]
        public Transform driverDoor;

        [Tooltip("Maximum opening angle in degrees around the door's local Y axis.")]
        [Range(10f, 90f)]
        public float doorMaxOpenAngle = 55f;

        [Tooltip("Curve defining how the door opens and closes synchronized with the character's animation (0 = fully closed, 1 = max open angle).")]
        public AnimationCurve doorOpenCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),              // Frame 0: Closed
            new Keyframe(0.18f, 0f, 0f, 2.0f),         // Frame 30: Hand touches handle, starts cracking open
            new Keyframe(0.38f, 1f, 1.5f, 0f),         // Frame 63: Swung wide open
            new Keyframe(0.64f, 1f, 0f, 0f),           // Frame 105: Stays open while character enters & sits
            new Keyframe(0.88f, 0f, -3.5f, 0f),        // Frame 145: Pulled completely shut
            new Keyframe(1.0f, 0f, 0f, 0f)             // Frame 165: Firmly closed
        );

        [Tooltip("Local axis around which the door rotates. Default is Vector3.up (Y axis).")]
        public Vector3 doorRotationAxis = Vector3.up;

        private Quaternion initialDoorLocalRotation = Quaternion.identity;

        [Header("--- Vehicle Exit Safety ---")]
        [Tooltip("Maximum vehicle speed (km/h) to allow exiting safely.")]
        public float maxExitSpeed = 3f;

        [Tooltip("Warning text when trying to exit while car is moving.")]
        public string carMovingWarningText = "Dừng xe để xuống!";

        private float lastInteractTime = -10f;
        private Coroutine exitPromptCoroutine;
        private Coroutine enterSequenceCoroutine;
        private Coroutine exitSequenceCoroutine;
        private PlayableGraph activePlayableGraph;
        private AnimationClipPlayable clipPlayable;
        private Collider[] characterColliders;
        private bool isTransitioning = false;

        // Proximity tracking
        private bool isPlayerInRange = false;
        private bool isUIActive = false;

        #region IInteractable Implementation (for Malbers MInteractor)
        public GameObject Owner => gameObject;
        public int Index => 0;
        public bool Active { get => currentState == ControlState.OnFoot; set { } }
        public bool SingleInteraction => false;
        public bool Auto { get; set; } = false;
        public bool Focused { get; set; }

        public void Focus(IInteractor focuser)
        {
            Focused = true;
            isPlayerInRange = true;
            if (currentState == ControlState.OnFoot)
            {
                ShowInteractUI(enterPromptText);
            }
        }

        public void UnFocus(IInteractor focuser)
        {
            Focused = false;
            isPlayerInRange = false;
            HideInteractUI();
        }

        public bool Interact(IInteractor interactor)
        {
            if (currentState == ControlState.OnFoot && CanInteract())
            {
                EnterVehicle();
                return true;
            }
            return false;
        }

        public bool Interact(int interactorID, GameObject interactor)
        {
            if (currentState == ControlState.OnFoot && CanInteract())
            {
                EnterVehicle();
                return true;
            }
            return false;
        }

        public void Interact()
        {
            if (currentState == ControlState.OnFoot && CanInteract())
            {
                EnterVehicle();
            }
        }

        public void Restart()
        {
            Focused = false;
            isPlayerInRange = false;
            HideInteractUI();
        }
        #endregion

        private bool CanInteract()
        {
            return !isTransitioning && (Time.time - lastInteractTime >= interactCooldown);
        }

        private void OnDisable()
        {
            if (activePlayableGraph.IsValid())
            {
                activePlayableGraph.Destroy();
            }

            if (driverDoor != null && initialDoorLocalRotation != Quaternion.identity)
            {
                driverDoor.localRotation = initialDoorLocalRotation;
            }

            // Safety cleanup if disabled while in vehicle
            if (character != null)
            {
                SetCharacterCollidersEnabled(true);
                if (carController != null && character.transform.parent == carController.transform)
                {
                    character.transform.SetParent(null, true);
                }
                var animal = character.GetComponent<MalbersAnimations.Controller.MAnimal>();
                if (animal != null && animal.RB != null)
                {
                    animal.RB.isKinematic = false;
                }
            }
        }

        /// <summary>
        /// Enables or disables all colliders on the character and its children to strictly prevent
        /// PhysX compound collider conflicts with RCC vehicle while seated.
        /// </summary>
        private void SetCharacterCollidersEnabled(bool isEnabled)
        {
            if (characterColliders == null && character != null)
            {
                characterColliders = character.GetComponentsInChildren<Collider>(true);
            }

            if (characterColliders != null)
            {
                foreach (var col in characterColliders)
                {
                    if (col != null)
                    {
                        col.enabled = isEnabled;
                    }
                }
            }
        }

        /// <summary>
        /// Robust ground detection that strictly excludes vehicle, character, triggers,
        /// and non-environment layers to prevent characters jumping into the air.
        /// </summary>
        private bool TryGetGroundHeight(Vector3 searchPos, out float groundHeight, float referenceGroundY = float.NaN)
        {
            groundHeight = searchPos.y;

            // Dynamic layer mask excluding all known non-ground layers
            int excludeMask = 0;
            string[] excludeLayers = new string[] {
                "Ignore Raycast", "TransparentFX", "UI", "Water",
                "RCC_Vehicle", "RCC_WheelCollider", "RCC_DetachablePart", "RCC_Prop",
                "Animal", "BodyPart", "Enemy", "Player"
            };

            foreach (var name in excludeLayers)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer != -1)
                {
                    excludeMask |= (1 << layer);
                }
            }

            int groundMask = ~excludeMask;

            // Cast from 2.0m above search position downwards
            Vector3 rayStart = new Vector3(searchPos.x, searchPos.y + 2.0f, searchPos.z);
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 6.0f, groundMask, QueryTriggerInteraction.Ignore);

            float bestY = float.MinValue;
            bool foundValid = false;

            // If referenceGroundY is provided, ground cannot be > 0.4m higher than reference
            float maxY = !float.IsNaN(referenceGroundY) ? referenceGroundY + 0.4f : searchPos.y + 0.4f;

            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;

                // Exclude any collider that belongs to the car or character
                if (carController != null && (hit.collider.transform.IsChildOf(carController.transform) || hit.collider.transform == carController.transform))
                    continue;

                if (character != null && (hit.collider.transform.IsChildOf(character.transform) || hit.collider.transform == character.transform))
                    continue;

                // Ground surface cannot be higher than maxY (e.g. above player's waist / car roof)
                if (hit.point.y > maxY)
                    continue;

                // Take the highest valid ground surface below maxY
                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    foundValid = true;
                }
            }

            if (foundValid)
            {
                groundHeight = bestY;
                return true;
            }

            // Fallback 1: Reference ground height
            if (!float.IsNaN(referenceGroundY))
            {
                groundHeight = referenceGroundY;
                return true;
            }

            // Fallback 2: Car wheels ground level
            if (carController != null)
            {
                groundHeight = carController.transform.position.y - 0.8f;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Smoothly switches active camera to the Malbers Cinemachine 3 HAP camera rig and disables RCC camera.
        /// </summary>
        private void SwitchToHAPCamera()
        {
            if (rccCamera)
            {
                rccCamera.isRendering = false;
                if (rccCamera.actualCamera)
                {
                    rccCamera.actualCamera.gameObject.SetActive(false);
                    var listener = rccCamera.actualCamera.GetComponent<AudioListener>();
                    if (listener) listener.enabled = false;
                }
            }

            if (hapCameraRig)
            {
                hapCameraRig.SetActive(true);
                var hapCam = hapCameraRig.GetComponentInChildren<Camera>(false);
                if (hapCam != null)
                {
                    hapCam.tag = "MainCamera";
                    var animal = character ? character.GetComponent<MalbersAnimations.Controller.MAnimal>() : null;
                    if (animal != null)
                    {
                        animal.m_MainCamera.UseConstant = true;
                        animal.m_MainCamera.Value = hapCam.transform;
                    }
                }
            }
        }

        private void Awake()
        {
            AutoFindReferences();
            EnsureInteractUIExists();

            // Hook into MInteract event if present on car
            if (!carInteractable && carController)
                carInteractable = carController.GetComponentInChildren<MInteract>();

            if (carInteractable)
            {
                carInteractable.OnInteractWithGO.AddListener(OnHAPInteractEvent);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                AutoFindReferences();
            }
        }
#endif

        private void Start()
        {
            // Apply initial state
            if (currentState == ControlState.OnFoot)
            {
                ApplyOnFootState(true);
            }
            else
            {
                ApplyInVehicleState(true);
            }
        }

        private void Update()
        {
            if (currentState == ControlState.OnFoot)
            {
                CheckOnFootProximity();
            }
            else if (currentState == ControlState.InVehicle)
            {
                CheckInVehicleInput();
            }
        }

        private void AutoFindReferences()
        {
            // 1. Car Controller
            if (!carController)
                carController = FindFirstObjectByType<RCC_CarControllerV4>();

            // 2. RCC Camera
            if (!rccCamera)
                rccCamera = FindFirstObjectByType<RCC_Camera>();

            // 3. Character (MAnimal)
            if (!character)
            {
                var animal = FindFirstObjectByType<MalbersAnimations.Controller.MAnimal>();
                if (animal) character = animal.gameObject;
            }

            // 4. HAP Camera Rig (Cameras CM3 or CinemachineBrain)
            if (!hapCameraRig)
            {
                var brain = FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
                if (brain)
                {
                    Transform parent = brain.transform.parent;
                    hapCameraRig = parent ? parent.gameObject : brain.gameObject;
                }
            }

            // 5. Door point
            if (!doorPoint && carController)
            {
                doorPoint = carController.transform.Find("Door_Driver");
                if (!doorPoint)
                {
                    GameObject doorGO = new GameObject("Door_Driver");
                    doorGO.transform.SetParent(carController.transform, false);
                    doorGO.transform.localPosition = new Vector3(-1.15f, 0.9f, 0.2f);
                    doorGO.transform.localRotation = Quaternion.identity;
                    doorPoint = doorGO.transform;
                }
            }

            // 6. Driver Seat & Steering Wheel
            // In this car model, the driver seat is FrontSeat_Left (behind Steering_Wheel on the left side).
            // FrontSeat_Right is the passenger seat on the right side.
            if (carController)
            {
                if (driverSeat == null || driverSeat.name == "FrontSeat_Right")
                {
                    foreach (var t in carController.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "FrontSeat_Left")
                        {
                            driverSeat = t;
                            break;
                        }
                    }

                    if (!driverSeat)
                    {
                        driverSeat = carController.transform.Find("The_Last_Drive_Car/FrontSeat_Left");
                    }
                }
            }

            if (!steeringWheel && carController)
            {
                foreach (var t in carController.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Steering_Wheel")
                    {
                        steeringWheel = t;
                        break;
                    }
                }

                if (!steeringWheel)
                {
                    steeringWheel = carController.transform.Find("The_Last_Drive_Car/Steering_Wheel");
                }
            }

            // 7. Driver Door (Byton_Optimazile_LookDev_SK_FD_Left)
            if (!driverDoor && carController)
            {
                foreach (var t in carController.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Byton_Optimazile_LookDev_SK_FD_Left")
                    {
                        driverDoor = t;
                        break;
                    }
                }

                if (!driverDoor)
                {
                    driverDoor = carController.transform.Find("The_Last_Drive_Car/Byton_Optimazile_LookDev_SK_FD_Left");
                }
            }

            if (driverDoor != null)
            {
                initialDoorLocalRotation = driverDoor.localRotation;
            }

            // 8. HAP MEvent for Interact UI
            if (!interactUIEvent)
            {
#if UNITY_EDITOR
                interactUIEvent = UnityEditor.AssetDatabase.LoadAssetAtPath<MalbersAnimations.Events.MEvent>(
                    "Assets/Malbers Animations/Common/Assets/Events/Extras/Interact UI.asset");
#endif
                if (!interactUIEvent)
                    interactUIEvent = MTools.GetInstance<MalbersAnimations.Events.MEvent>("Interact UI");
            }

            // 9. Enter Car Animation Clip
#if UNITY_EDITOR
            if (!enterCarClip)
            {
                var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Rearview/Animations/Y_Bot@Entering_Car.fbx");
                foreach (var a in subAssets)
                {
                    if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        enterCarClip = clip;
                        break;
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Context menu action to quickly auto-wire all references and calibrated offsets in the Inspector.
        /// </summary>
        [ContextMenu("Auto Setup References & Offsets")]
        public void AutoSetupReferencesAndOffsets()
        {
            driverSeat = null; // force re-detection of FrontSeat_Left
            driverDoor = null; // force re-detection of Byton_Optimazile_LookDev_SK_FD_Left
            AutoFindReferences();
            startOffsetFromSeat = new Vector3(-1.86f, 0f, -0.00265f);
            startYawOffset = 90f;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[VehicleCharacterManager] ✅ Đã cấu hình xong: Ghế lái = {(driverSeat ? driverSeat.name : "null")}, Vô lăng = {(steeringWheel ? steeringWheel.name : "null")}, Cửa lái = {(driverDoor ? driverDoor.name : "null")}, Offset = {startOffsetFromSeat}");
        }

        /// <summary>
        /// Automatically ensures that UI_Canvas and HAP Interact UI prefab exist in the scene.
        /// </summary>
        public void EnsureInteractUIExists()
        {
            // 1. Check if Interact UI / UIFollowTransform already exists
            var existingUI = FindFirstObjectByType<MalbersAnimations.UI.UIFollowTransform>();
            if (existingUI != null) return;

            // 2. Find or create UI_Canvas (Screen Space Overlay)
            Canvas canvas = null;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name != "HMI_Canvas")
                {
                    canvas = c;
                    break;
                }
            }

            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("UI_Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = true;

                var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Debug.Log("[VehicleCharacterManager] ✅ Đã tự động tạo UI_Canvas.");
            }

            // 3. Instantiate Interact UI prefab under canvas
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Malbers Animations/Common/Prefabs/UI/Interact UI.prefab");
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, canvas.transform);
                instance.name = "Interact UI";
                Debug.Log("[VehicleCharacterManager] ✅ Đã tự động tạo HAP Interact UI prefab trên UI_Canvas.");
            }
            else
            {
                Debug.LogWarning("[VehicleCharacterManager] ⚠️ Không tìm thấy prefab: Assets/Malbers Animations/Common/Prefabs/UI/Interact UI.prefab");
            }
#endif
        }

        private void CheckOnFootProximity()
        {
            if (!character || !carController) return;

            Vector3 target = doorPoint ? doorPoint.position : carController.transform.position;
            float dist = Vector3.Distance(character.transform.position, target);
            bool inRange = (dist <= interactionDistance);

            if (inRange != isPlayerInRange)
            {
                isPlayerInRange = inRange;
                if (isPlayerInRange)
                {
                    ShowInteractUI(enterPromptText);
                }
                else
                {
                    HideInteractUI();
                }
            }

            if (isPlayerInRange && CanInteract() && WasInteractPressed())
            {
                EnterVehicle();
            }
        }

        private void CheckInVehicleInput()
        {
            if (CanInteract() && WasInteractPressed())
            {
                ExitVehicle();
            }
        }

        /// <summary>
        /// Shows HAP native Interact UI using default prompt for current state.
        /// </summary>
        public void ShowInteractUI()
        {
            ShowInteractUI(currentState == ControlState.OnFoot ? enterPromptText : exitPromptText);
        }

        /// <summary>
        /// Shows HAP native Interact UI using MEvent.
        /// </summary>
        public void ShowInteractUI(string promptText)
        {
            if (!showPrompt) return;

            if (interactUIEvent == null)
            {
#if UNITY_EDITOR
                interactUIEvent = UnityEditor.AssetDatabase.LoadAssetAtPath<MalbersAnimations.Events.MEvent>(
                    "Assets/Malbers Animations/Common/Assets/Events/Extras/Interact UI.asset");
#endif
                if (interactUIEvent == null)
                    interactUIEvent = MTools.GetInstance<MalbersAnimations.Events.MEvent>("Interact UI");
            }

            if (interactUIEvent == null)
            {
                Debug.LogWarning("[VehicleCharacterManager] ⚠️ interactUIEvent chưa được gán!");
                return;
            }

            EnsureInteractUIExists();

            Transform target = doorPoint ? doorPoint : (carController ? carController.transform : null);
            if (target != null)
            {
                interactUIEvent.Invoke(target);
            }

            interactUIEvent.Invoke(promptText);
            interactUIEvent.Invoke(1);     // 1 shows [E] icon
            interactUIEvent.Invoke(true);  // shows container & enables UIFollowTransform
            isUIActive = true;
            Debug.Log($"[VehicleCharacterManager] 🎯 ShowInteractUI: '{promptText}' tại {target?.name}");
        }

        /// <summary>
        /// Hides HAP native Interact UI using MEvent.
        /// </summary>
        public void HideInteractUI()
        {
            if (interactUIEvent != null)
            {
                interactUIEvent.Invoke(false);
            }
            isUIActive = false;
        }

        /// <summary>
        /// Reads interact key (E) compatible with both Unity New Input System and Legacy.
        /// </summary>
        private bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E))
                return true;
#endif
            return false;
        }

        /// <summary>
        /// Listener for MInteract UnityEvent.
        /// </summary>
        public void OnHAPInteractEvent(GameObject interactor)
        {
            if (currentState == ControlState.OnFoot && CanInteract())
            {
                EnterVehicle();
            }
        }

        /// <summary>
        /// Transfer control to the RCC vehicle with smooth alignment and animation.
        /// </summary>
        [ContextMenu("Enter Vehicle")]
        public void EnterVehicle()
        {
            if (!carController || !CanInteract() || isTransitioning) return;

            lastInteractTime = Time.time;
            HideInteractUI();

            if (exitPromptCoroutine != null)
            {
                StopCoroutine(exitPromptCoroutine);
                exitPromptCoroutine = null;
            }

            if (enterSequenceCoroutine != null)
                StopCoroutine(enterSequenceCoroutine);

            enterSequenceCoroutine = StartCoroutine(EnterVehicleSequence());
        }

        private IEnumerator EnterVehicleSequence()
        {
            isTransitioning = true;

            var animal = character != null ? character.GetComponent<MalbersAnimations.Controller.MAnimal>() : null;
            var animator = character != null ? character.GetComponent<Animator>() : null;

            // 1. Lock character input & movement so physics don't fight alignment
            if (animal != null)
            {
                animal.LockInput = true;
                animal.LockMovement = true;
                if (animal.RB != null)
                    animal.RB.isKinematic = true;
                // Temporarily disable MAnimal to prevent OnAnimatorMove from swallowing root motion / fighting bone alignment
                animal.enabled = false;
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            // Immediately disable character colliders to prevent self-collision and raycast hits during entering
            SetCharacterCollidersEnabled(false);

            // Record character's current ground Y before alignment as reliable reference
            float currentCharacterGroundY = character != null ? character.transform.position.y : (carController ? carController.transform.position.y - 0.8f : 0f);

            // Ensure character is cleanly unparented in world space before alignment
            if (character != null && character.transform.parent != null)
            {
                character.transform.SetParent(null, true);
            }

            // 2. Pre-align character to startPos (feet firmly on ground, facing into driver door)
            // Mathematical anchor: startPos is calculated from driverSeat (FrontSeat_Left behind Steering_Wheel) so that at the end of Entering_Car
            // the hips land exactly in FrontSeat_Left and hands on Steering_Wheel.
            if (character && carController)
            {
                Vector3 seatPos = driverSeat ? driverSeat.position : carController.transform.TransformPoint(new Vector3(-0.364f, -0.611f, 0.043f));
                Vector3 targetPos = seatPos + carController.transform.right * startOffsetFromSeat.x + carController.transform.forward * startOffsetFromSeat.z;

                if (TryGetGroundHeight(targetPos, out float groundY, currentCharacterGroundY))
                {
                    targetPos.y = groundY;
                }
                else
                {
                    targetPos.y = currentCharacterGroundY;
                }

                Quaternion targetRot = Quaternion.Euler(0f, carController.transform.eulerAngles.y + startYawOffset, 0f);

                yield return MTools.AlignTransform(character.transform, targetPos, targetRot, alignToDoorDuration, alignCurve);
            }

            // 3. Play Entering_Car animation via PlayableGraph
            if (animator != null && enterCarClip != null)
            {
                if (activePlayableGraph.IsValid())
                    activePlayableGraph.Destroy();

                activePlayableGraph = PlayableGraph.Create("EnterCarPlayable");
                activePlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

                clipPlayable = AnimationClipPlayable.Create(activePlayableGraph, enterCarClip);
                clipPlayable.SetSpeed(0f); // manual normalized time control

                var output = AnimationPlayableOutput.Create(activePlayableGraph, "Animation", animator);
                output.SetSourcePlayable(clipPlayable);

                activePlayableGraph.Play();

                float totalDuration = enterCarClip.length / enterAnimSpeed;
                float elapsed = 0f;
                bool cameraSwitched = false;

                // Cache initial door rotation before opening
                if (driverDoor != null)
                {
                    initialDoorLocalRotation = driverDoor.localRotation;
                }

                while (elapsed < totalDuration)
                {
                    elapsed += Time.deltaTime;
                    float normalizedTime = Mathf.Clamp01(elapsed / totalDuration);

                    if (clipPlayable.IsValid())
                    {
                        clipPlayable.SetTime(normalizedTime * enterCarClip.length);
                    }

                    // Animate driver door opening/closing based on calibrated curve
                    if (driverDoor != null && doorOpenCurve != null)
                    {
                        float curveVal = doorOpenCurve.Evaluate(normalizedTime);
                        float currentAngle = curveVal * doorMaxOpenAngle;
                        driverDoor.localRotation = initialDoorLocalRotation * Quaternion.AngleAxis(currentAngle, doorRotationAxis);
                    }

                    // Near the end of animation (~80%, when character is inside the cabin), switch camera to car
                    if (!cameraSwitched && elapsed >= totalDuration * 0.8f)
                    {
                        cameraSwitched = true;
                        SwitchToRCCCamera();
                    }

                    yield return null;
                }

                // Ensure door is firmly closed at the end of animation
                if (driverDoor != null)
                {
                    driverDoor.localRotation = initialDoorLocalRotation;
                }

                // Hold final frame (seated driving pose with hands on steering wheel)
                if (clipPlayable.IsValid())
                {
                    clipPlayable.SetTime(enterCarClip.length);
                }
            }
            else
            {
                // Fallback if no animation clip is present
                yield return new WaitForSeconds(0.3f);
                SwitchToRCCCamera();
            }

            // 4. Finalize in-vehicle state
            currentState = ControlState.InVehicle;
            ApplyInVehicleState(false);

            isTransitioning = false;
            enterSequenceCoroutine = null;

            if (showExitPrompt)
            {
                exitPromptCoroutine = StartCoroutine(ShowExitPromptTemporarily(exitPromptDuration));
            }
        }

        private IEnumerator ShowExitPromptTemporarily(float duration)
        {
            ShowInteractUI(exitPromptText);
            yield return new WaitForSeconds(duration);
            if (currentState == ControlState.InVehicle)
            {
                HideInteractUI();
            }
            exitPromptCoroutine = null;
        }

        /// <summary>
        /// Transfer control back to the HAP character with safety checks and clean handover.
        /// </summary>
        [ContextMenu("Exit Vehicle")]
        public void ExitVehicle()
        {
            if (!carController || !character || !CanInteract() || isTransitioning) return;

            // Safety check: Vehicle must be almost stopped to exit safely
            if (carController.speed > maxExitSpeed)
            {
                ShowInteractUI(carMovingWarningText);
                StartCoroutine(HidePromptAfterDelay(2f));
                return;
            }

            if (exitPromptCoroutine != null)
            {
                StopCoroutine(exitPromptCoroutine);
                exitPromptCoroutine = null;
            }

            lastInteractTime = Time.time;
            HideInteractUI();

            if (exitSequenceCoroutine != null)
                StopCoroutine(exitSequenceCoroutine);

            exitSequenceCoroutine = StartCoroutine(ExitVehicleSequence());
        }

        private IEnumerator HidePromptAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (currentState == ControlState.InVehicle)
            {
                HideInteractUI();
            }
        }

        private IEnumerator ExitVehicleSequence()
        {
            isTransitioning = true;

            // 1. Immediately cut engine, disallow control, and apply handbrake
            if (carController)
            {
                carController.SetCanControl(false);
                carController.handbrakeInput = 1f;
                carController.KillEngine();
            }

            var animator = character != null ? character.GetComponent<Animator>() : null;

            // 2. Play reverse animation from Frame 165 down to Frame 0
            if (animator != null && enterCarClip != null)
            {
                if (activePlayableGraph.IsValid())
                    activePlayableGraph.Destroy();

                activePlayableGraph = PlayableGraph.Create("ExitCarPlayable");
                activePlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

                var mixer = AnimationMixerPlayable.Create(activePlayableGraph, 2);
                clipPlayable = AnimationClipPlayable.Create(activePlayableGraph, enterCarClip);
                clipPlayable.SetSpeed(0f);

                bool hasController = animator.runtimeAnimatorController != null;
                AnimatorControllerPlayable controllerPlayable = default;
                if (hasController)
                {
                    controllerPlayable = AnimatorControllerPlayable.Create(activePlayableGraph, animator.runtimeAnimatorController);
                }

                activePlayableGraph.Connect(clipPlayable, 0, mixer, 0);
                if (hasController)
                {
                    activePlayableGraph.Connect(controllerPlayable, 0, mixer, 1);
                }

                mixer.SetInputWeight(0, 1f);
                mixer.SetInputWeight(1, 0f);

                var output = AnimationPlayableOutput.Create(activePlayableGraph, "Animation", animator);
                output.SetSourcePlayable(mixer);

                activePlayableGraph.Play();

                float totalDuration = enterCarClip.length / enterAnimSpeed;
                float elapsed = 0f;
                bool cameraSwitched = false;

                if (driverDoor != null)
                {
                    initialDoorLocalRotation = driverDoor.localRotation;
                }

                while (elapsed < totalDuration)
                {
                    elapsed += Time.deltaTime;
                    // Normalized time runs backwards: 1.0 (seated) down to 0.0 (standing outside)
                    float normalizedTime = 1f - Mathf.Clamp01(elapsed / totalDuration);

                    if (clipPlayable.IsValid())
                    {
                        clipPlayable.SetTime(normalizedTime * enterCarClip.length);
                    }

                    // Animate driver door opening from inside, staying open, and shutting
                    if (driverDoor != null && doorOpenCurve != null)
                    {
                        float curveVal = doorOpenCurve.Evaluate(normalizedTime);
                        float currentAngle = curveVal * doorMaxOpenAngle;
                        driverDoor.localRotation = initialDoorLocalRotation * Quaternion.AngleAxis(currentAngle, doorRotationAxis);
                    }

                    // Switch camera to HAP early in the exit sequence (~15%, when door begins cracking open)
                    if (!cameraSwitched && elapsed >= totalDuration * 0.15f)
                    {
                        cameraSwitched = true;
                        SwitchToHAPCamera();
                    }

                    yield return null;
                }

                // Ensure door is firmly shut at the end of exit
                if (driverDoor != null)
                {
                    driverDoor.localRotation = initialDoorLocalRotation;
                }

                // Hold final standing pose at frame 0
                if (clipPlayable.IsValid())
                {
                    clipPlayable.SetTime(0f);
                }

                // 3. Unparent character from vehicle and firmly ground outside door
                if (character)
                {
                    character.transform.SetParent(null, true);

                    Vector3 exitPos = character.transform.position;
                    float carGroundY = carController ? carController.transform.position.y - 0.8f : exitPos.y;
                    if (TryGetGroundHeight(exitPos, out float groundY, carGroundY))
                    {
                        exitPos.y = groundY;
                        character.transform.position = exitPos;
                    }
                }

                // 4. Smoothly blend from exit standing pose (legs together) into HAP Idle pose (legs spread)
                if (hasController && exitTransitionDuration > 0f)
                {
                    float blendElapsed = 0f;
                    while (blendElapsed < exitTransitionDuration)
                    {
                        blendElapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(blendElapsed / exitTransitionDuration);
                        float smoothT = Mathf.SmoothStep(0f, 1f, t);
                        mixer.SetInputWeight(0, 1f - smoothT);
                        mixer.SetInputWeight(1, smoothT);
                        yield return null;
                    }
                    mixer.SetInputWeight(0, 0f);
                    mixer.SetInputWeight(1, 1f);
                }

                // Clean up PlayableGraph so AnimatorController resumes on-foot locomotion
                if (activePlayableGraph.IsValid())
                {
                    activePlayableGraph.Destroy();
                }
            }
            else
            {
                // Fallback if no animation clip is present
                yield return new WaitForSeconds(0.3f);
                SwitchToHAPCamera();

                if (character)
                {
                    character.transform.SetParent(null, true);
                    Vector3 exitPos = character.transform.position;
                    float carGroundY = carController ? carController.transform.position.y - 0.8f : exitPos.y;
                    if (TryGetGroundHeight(exitPos, out float groundY, carGroundY))
                    {
                        exitPos.y = groundY;
                        character.transform.position = exitPos;
                    }
                }
            }

            // 5. Restore full on-foot physics & control
            if (character)
            {
                // Re-enable colliders
                SetCharacterCollidersEnabled(true);

                // Restore MAnimal & Rigidbody dynamics
                var animal = character.GetComponent<MalbersAnimations.Controller.MAnimal>();
                if (animal != null)
                {
                    if (animal.RB != null)
                    {
                        animal.RB.isKinematic = false;
                        animal.RB.detectCollisions = true;
                    }
                    animal.enabled = true;
                    animal.LockInput = false;
                    animal.LockMovement = false;
                }

                if (animator != null)
                {
                    animator.applyRootMotion = false;
                }
            }

            // Ensure HAP camera is active
            SwitchToHAPCamera();

            currentState = ControlState.OnFoot;
            isTransitioning = false;
            exitSequenceCoroutine = null;
        }

        private void SwitchToRCCCamera()
        {
            if (hapCameraRig)
                hapCameraRig.SetActive(false);

            if (rccCamera)
            {
                rccCamera.isRendering = true;
                if (rccCamera.actualCamera)
                {
                    rccCamera.actualCamera.gameObject.SetActive(true);
                    rccCamera.actualCamera.tag = "MainCamera";
                    var listener = rccCamera.actualCamera.GetComponent<AudioListener>();
                    if (listener) listener.enabled = true;
                }
                rccCamera.SetTarget(carController);
            }
        }

        private void ApplyOnFootState(bool isInit)
        {
            // 1. Unparent character and position safely to the left of the car if needed
            if (character)
            {
                if (carController != null && character.transform.parent == carController.transform)
                {
                    character.transform.SetParent(null, true);
                }

                if (!isInit && carController)
                {
                    float doorZ = doorPoint ? carController.transform.InverseTransformPoint(doorPoint.position).z : 0.2f;
                    Vector3 exitPos = carController.transform.TransformPoint(new Vector3(-2.0f, 0f, doorZ));
                    float carGroundY = carController.transform.position.y - 0.8f;

                    if (TryGetGroundHeight(exitPos, out float groundY, carGroundY))
                    {
                        exitPos.y = groundY;
                    }

                    character.transform.position = exitPos;
                    character.transform.rotation = Quaternion.Euler(0f, carController.transform.eulerAngles.y, 0f);
                }

                // 2. Enable Character & restore physics
                character.SetActive(true);
                SetCharacterCollidersEnabled(true);

                var animal = character.GetComponent<MalbersAnimations.Controller.MAnimal>();
                if (animal != null)
                {
                    if (animal.RB != null)
                    {
                        animal.RB.isKinematic = false;
                        animal.RB.detectCollisions = true;
                    }
                    animal.enabled = true;
                    animal.LockInput = false;
                    animal.LockMovement = false;
                }

                var animator = character.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                }
            }

            // Clean up any remaining PlayableGraph
            if (activePlayableGraph.IsValid())
            {
                activePlayableGraph.Destroy();
            }

            // 3. Enable HAP Camera Rig & disable RCC Camera
            SwitchToHAPCamera();

            // 4. Disable Car control, engage handbrake, shut off engine
            if (carController)
            {
                carController.SetCanControl(false);
                carController.handbrakeInput = 1f;
                carController.KillEngine();
            }
        }

        private void ApplyInVehicleState(bool isInit)
        {
            // 1. Keep Character ENABLED, but parent to car and disable colliders/physics
            if (character && carController)
            {
                character.SetActive(true);

                // Disable all colliders on character to protect RCC compound collider
                SetCharacterCollidersEnabled(false);

                // Disable MAnimal & set Rigidbody to kinematic
                var animal = character.GetComponent<MalbersAnimations.Controller.MAnimal>();
                if (animal != null)
                {
                    animal.LockInput = true;
                    animal.LockMovement = true;
                    if (animal.RB != null)
                    {
                        animal.RB.isKinematic = true;
                        animal.RB.detectCollisions = false;
                    }
                    animal.enabled = false;
                }

                var animator = character.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                }

                // If initializing directly into vehicle state:
                if (isInit)
                {
                    Vector3 seatPos = driverSeat ? driverSeat.position : carController.transform.TransformPoint(new Vector3(-0.364f, -0.611f, 0.043f));
                    Vector3 targetPos = seatPos + carController.transform.right * startOffsetFromSeat.x + carController.transform.forward * startOffsetFromSeat.z;
                    float carGroundY = carController.transform.position.y - 0.8f;
                    if (TryGetGroundHeight(targetPos, out float groundY, carGroundY))
                    {
                        targetPos.y = groundY;
                    }
                    character.transform.position = targetPos;
                    character.transform.rotation = Quaternion.Euler(0f, carController.transform.eulerAngles.y + startYawOffset, 0f);

                    if (animator != null && enterCarClip != null)
                    {
                        if (activePlayableGraph.IsValid()) activePlayableGraph.Destroy();
                        activePlayableGraph = PlayableGraph.Create("EnterCarPlayable");
                        activePlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

                        clipPlayable = AnimationClipPlayable.Create(activePlayableGraph, enterCarClip);
                        clipPlayable.SetSpeed(0f);
                        clipPlayable.SetTime(enterCarClip.length);

                        var output = AnimationPlayableOutput.Create(activePlayableGraph, "Animation", animator);
                        output.SetSourcePlayable(clipPlayable);

                        activePlayableGraph.Play();
                    }
                }

                // Parent character to car so it follows vehicle motion, tilting, and suspension
                character.transform.SetParent(carController.transform, true);
            }

            // 2. Enable RCC Camera
            SwitchToRCCCamera();

            // 3. Enable Car control, release handbrake, start engine
            if (carController)
            {
                carController.SetCanControl(true);
                carController.handbrakeInput = 0f;
                carController.StartEngine();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!carController) return;

            // 1. Interaction distance wire sphere around doorPoint
            Vector3 center = doorPoint ? doorPoint.position : carController.transform.position;
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.3f);
            Gizmos.DrawWireSphere(center, interactionDistance);

            // 2. Door_Driver anchor
            Vector3 defaultDoor = carController.transform.TransformPoint(new Vector3(-1.15f, 0.9f, 0.2f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(doorPoint ? doorPoint.position : defaultDoor, 0.15f);

            // 3. Driver Seat Cushion (Green) - FrontSeat_Left behind Steering_Wheel
            Vector3 seatPos = driverSeat ? driverSeat.position : carController.transform.TransformPoint(new Vector3(-0.364f, -0.611f, 0.043f));
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(seatPos, 0.2f);

            // 4. Steering Wheel (Cyan)
            Vector3 wheelPos = steeringWheel ? steeringWheel.position : carController.transform.TransformPoint(new Vector3(-0.358f, 0.129f, 0.396f));
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(wheelPos, 0.18f);

            // 5. Pre-alignment Character Start Position (Magenta/Cyan)
            Vector3 startPos = seatPos + carController.transform.right * startOffsetFromSeat.x + carController.transform.forward * startOffsetFromSeat.z;
            float carGroundY = carController.transform.position.y - 0.8f;
            if (TryGetGroundHeight(startPos, out float groundY, carGroundY))
            {
                startPos.y = groundY;
            }
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(startPos, 0.2f);

            // 6. Character start facing direction (Yellow arrow pointing towards driver door)
            Vector3 startFwd = Quaternion.Euler(0f, carController.transform.eulerAngles.y + startYawOffset, 0f) * Vector3.forward;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(startPos + Vector3.up * 0.5f, startFwd * 1.0f);

            // 7. Trajectory line from startPos to seatPos
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            Gizmos.DrawLine(startPos + Vector3.up * 0.5f, seatPos + Vector3.up * 0.5f);
        }
    }
}
