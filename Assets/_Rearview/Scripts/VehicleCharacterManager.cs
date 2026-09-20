using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MalbersAnimations;
using MalbersAnimations.Utilities;

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
        private float lastInteractTime = -10f;
        private Coroutine exitPromptCoroutine;

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
            return (Time.time - lastInteractTime >= interactCooldown);
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

            // 6. HAP MEvent for Interact UI
            if (!interactUIEvent)
            {
#if UNITY_EDITOR
                interactUIEvent = UnityEditor.AssetDatabase.LoadAssetAtPath<MalbersAnimations.Events.MEvent>(
                    "Assets/Malbers Animations/Common/Assets/Events/Extras/Interact UI.asset");
#endif
                if (!interactUIEvent)
                    interactUIEvent = MTools.GetInstance<MalbersAnimations.Events.MEvent>("Interact UI");
            }
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
        /// Transfer control to the RCC vehicle.
        /// </summary>
        [ContextMenu("Enter Vehicle")]
        public void EnterVehicle()
        {
            if (!carController || !CanInteract()) return;

            lastInteractTime = Time.time;
            HideInteractUI();

            if (exitPromptCoroutine != null)
            {
                StopCoroutine(exitPromptCoroutine);
                exitPromptCoroutine = null;
            }

            currentState = ControlState.InVehicle;
            ApplyInVehicleState(false);

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
        /// Transfer control back to the HAP character.
        /// </summary>
        [ContextMenu("Exit Vehicle")]
        public void ExitVehicle()
        {
            if (!carController || !character || !CanInteract()) return;

            if (exitPromptCoroutine != null)
            {
                StopCoroutine(exitPromptCoroutine);
                exitPromptCoroutine = null;
            }

            lastInteractTime = Time.time;
            HideInteractUI();
            currentState = ControlState.OnFoot;
            ApplyOnFootState(false);
        }

        private void ApplyOnFootState(bool isInit)
        {
            // 1. Position character safely to the left of the car
            if (!isInit && carController && character)
            {
                // Offset 2.0m to the left of the car centerline, at driver door Z
                float doorZ = doorPoint ? carController.transform.InverseTransformPoint(doorPoint.position).z : 0.2f;
                Vector3 exitPos = carController.transform.TransformPoint(new Vector3(-2.0f, 0f, doorZ));

                // Raycast downward to place feet securely on the ground
                if (Physics.Raycast(exitPos + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 10f))
                {
                    exitPos.y = hit.point.y;
                }

                character.transform.position = exitPos;
                character.transform.rotation = Quaternion.Euler(0f, carController.transform.eulerAngles.y, 0f);
            }

            // 2. Enable Character
            if (character)
                character.SetActive(true);

            // 3. Enable HAP Camera Rig
            if (hapCameraRig)
                hapCameraRig.SetActive(true);

            // 4. Disable RCC Camera (and its AudioListener to avoid conflicts)
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

            // 5. Ensure MAnimal uses the correct Cinemachine camera direction (prevents confused movement)
            if (character && hapCameraRig)
            {
                var animal = character.GetComponent<MalbersAnimations.Controller.MAnimal>();
                var hapCam = hapCameraRig.GetComponentInChildren<Camera>(false);
                if (hapCam != null)
                {
                    hapCam.tag = "MainCamera";
                    if (animal != null)
                    {
                        animal.m_MainCamera.UseConstant = true;
                        animal.m_MainCamera.Value = hapCam.transform;
                    }
                }
            }

            // 6. Disable Car control, engage handbrake, shut off engine
            if (carController)
            {
                carController.SetCanControl(false);
                carController.handbrakeInput = 1f;
                carController.KillEngine();
            }
        }

        private void ApplyInVehicleState(bool isInit)
        {
            // 1. Disable Character (cleanly disables physics, input, animation)
            if (character)
                character.SetActive(false);

            // 2. Disable HAP Camera Rig (disables CinemachineBrain & AudioListener)
            if (hapCameraRig)
                hapCameraRig.SetActive(false);

            // 3. Enable RCC Camera
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

            // 4. Enable Car control, release handbrake, start engine
            if (carController)
            {
                carController.SetCanControl(true);
                carController.handbrakeInput = 0f;
                carController.StartEngine();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (carController)
            {
                Vector3 center = doorPoint ? doorPoint.position : carController.transform.position;
                Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);
                Gizmos.DrawWireSphere(center, interactionDistance);

                Vector3 defaultDoor = carController.transform.TransformPoint(new Vector3(-1.15f, 0.9f, 0.2f));
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(doorPoint ? doorPoint.position : defaultDoor, 0.25f);
            }
        }
    }
}
