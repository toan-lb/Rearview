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
    /// Also includes proximity check and New Input System failsafe.
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
        [Tooltip("The HAP character GameObject (e.g. Cowboy). Auto-found if null.")]
        public GameObject character;

        [Tooltip("The Malbers Cinemachine Camera Rig (e.g. Cameras CM3). Auto-found if null.")]
        public GameObject hapCameraRig;

        [Header("--- Interaction Settings ---")]
        [Tooltip("Optional transform near the driver door for entering/exiting. If null, calculated automatically on the left of the car.")]
        public Transform doorPoint;

        [Tooltip("Maximum distance to interact with the vehicle when on foot.")]
        [Range(1f, 10f)]
        public float interactionDistance = 3.5f;

        [Tooltip("HAP MInteract component (optional). If attached to the car, it will hook into this manager.")]
        public MInteract carInteractable;

        [Header("--- UI Prompt ---")]
        [Tooltip("Show simple on-screen prompt when in interaction range.")]
        public bool showPrompt = true;

        [Tooltip("Prompt text when near the vehicle on foot.")]
        public string enterPromptText = "Nhấn [E] Để Lên Xe";

        [Tooltip("Prompt text when driving the vehicle.")]
        public string exitPromptText = "Nhấn [E] Để Xuống Xe";

        // Proximity tracking
        private bool isPlayerInRange = false;

        #region IInteractable Implementation (for Malbers MInteractor)
        public GameObject Owner => gameObject;
        public int Index => 0;
        public bool Active { get => currentState == ControlState.OnFoot; set { } }
        public bool SingleInteraction => false;
        public bool Auto { get => false; set { } }
        public bool Focused { get; set; }

        public void Focus(IInteractor focuser)
        {
            Focused = true;
            isPlayerInRange = true;
        }

        public void UnFocus(IInteractor focuser)
        {
            Focused = false;
            isPlayerInRange = false;
        }

        public bool Interact(IInteractor interactor)
        {
            if (currentState == ControlState.OnFoot)
            {
                EnterVehicle();
                return true;
            }
            return false;
        }

        public bool Interact(int interactorID, GameObject interactor)
        {
            if (currentState == ControlState.OnFoot)
            {
                EnterVehicle();
                return true;
            }
            return false;
        }

        public void Interact()
        {
            if (currentState == ControlState.OnFoot)
            {
                EnterVehicle();
            }
        }

        public void Restart()
        {
            Focused = false;
            isPlayerInRange = false;
        }
        #endregion

        private void Awake()
        {
            AutoFindReferences();

            // Hook into MInteract event if present on car
            if (!carInteractable && carController)
                carInteractable = carController.GetComponentInChildren<MInteract>();

            if (carInteractable)
            {
                carInteractable.OnInteractWithGO.AddListener(OnHAPInteractEvent);
            }
        }

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
        }

        private void CheckOnFootProximity()
        {
            if (!character || !carController) return;

            Vector3 target = doorPoint ? doorPoint.position : carController.transform.position;
            float dist = Vector3.Distance(character.transform.position, target);
            isPlayerInRange = (dist <= interactionDistance);

            if (isPlayerInRange && WasInteractPressed())
            {
                EnterVehicle();
            }
        }

        private void CheckInVehicleInput()
        {
            if (WasInteractPressed())
            {
                ExitVehicle();
            }
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
            if (currentState == ControlState.OnFoot)
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
            if (!carController) return;

            currentState = ControlState.InVehicle;
            ApplyInVehicleState(false);
        }

        /// <summary>
        /// Transfer control back to the HAP character.
        /// </summary>
        [ContextMenu("Exit Vehicle")]
        public void ExitVehicle()
        {
            if (!carController || !character) return;

            currentState = ControlState.OnFoot;
            ApplyOnFootState(false);
        }

        private void ApplyOnFootState(bool isInit)
        {
            // 1. Position character next to driver's door
            if (!isInit && carController && character)
            {
                Vector3 exitPos = doorPoint 
                    ? doorPoint.position 
                    : carController.transform.TransformPoint(new Vector3(-2.2f, 0f, 0f));

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

            // 5. Disable Car control, engage handbrake, shut off engine
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

        private void OnGUI()
        {
            if (!showPrompt) return;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 17;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            if (currentState == ControlState.OnFoot && isPlayerInRange)
            {
                float width = 260f;
                float height = 45f;
                float x = (Screen.width - width) * 0.5f;
                float y = Screen.height - 110f;
                GUI.Box(new Rect(x, y, width, height), enterPromptText, style);
            }
            else if (currentState == ControlState.InVehicle)
            {
                float width = 260f;
                float height = 45f;
                float x = (Screen.width - width) * 0.5f;
                float y = Screen.height - 110f;
                GUI.Box(new Rect(x, y, width, height), exitPromptText, style);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (carController)
            {
                Vector3 center = doorPoint ? doorPoint.position : carController.transform.position;
                Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);
                Gizmos.DrawWireSphere(center, interactionDistance);

                Vector3 defaultDoor = carController.transform.TransformPoint(new Vector3(-2.2f, 0.5f, 0f));
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(doorPoint ? doorPoint.position : defaultDoor, 0.25f);
            }
        }
    }
}
