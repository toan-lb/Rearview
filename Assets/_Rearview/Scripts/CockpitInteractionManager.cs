using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rearview
{
    /// <summary>
    /// Manages in-cockpit interactions for The Last Waypoint:
    /// 1. Focus / Zoom Screen Mode ([F] key or click screen):
    ///    - Smoothly transitions camera from driver eye position to an optimized infotainment focus position.
    ///    - Unlocks mouse cursor for tactile touch interaction with the curved HMI screen.
    ///    - Temporarily disables camera orbit look so mouse movements don't rotate the view.
    ///    - Pressing [Esc] or [F] smoothly returns camera to normal driving position.
    /// 2. Mathematical Ray-Plane Interaction:
    ///    - Translates mouse clicks on the 3D curved screen directly into 2D HMI Canvas events without any PhysX colliders.
    ///    - Strictly complies with the Zero-Child-Collider rule for RCC vehicle physics.
    /// </summary>
    [AddComponentMenu("Rearview/Cockpit Interaction Manager")]
    public class CockpitInteractionManager : MonoBehaviour
    {
        [Header("--- References ---")]
        [Tooltip("The vehicle character manager. Auto-found if null.")]
        public VehicleCharacterManager vehicleManager;

        [Tooltip("The HMI display manager. Auto-found if null.")]
        public HMIDisplayManager hmiManager;

        [Tooltip("The RCC camera. Auto-found if null.")]
        public RCC_Camera rccCamera;

        [Tooltip("The driver cabin FPS camera transform (Cabin_FPS_Camera).")]
        public Transform cabinCameraTransform;

        [Header("--- Focus Camera Positions (Local to Car) ---")]
        [Tooltip("Normal driver eye position.")]
        public Vector3 normalEyeLocalPosition = new Vector3(-0.358f, 0.65f, -0.12f);
        public Vector3 normalEyeLocalEuler = Vector3.zero;

        [Tooltip("Camera position when focused on HMI screen.")]
        public Vector3 focusScreenLocalPosition = new Vector3(-0.16f, 0.58f, 0.16f);
        public Vector3 focusScreenLocalEuler = new Vector3(12f, 22f, 0f);

        [Tooltip("Transition duration in seconds.")]
        [Range(0.1f, 1f)]
        public float transitionDuration = 0.35f;

        [Header("--- State ---")]
        [SerializeField]
        private bool isScreenFocused = false;
        public bool IsScreenFocused => isScreenFocused;

        private Coroutine transitionCoroutine;
        private bool originalOrbitSetting = true;

        [Header("--- Screen Plane Calibration (Local to Car) ---")]
        // Center and normal of the center infotainment area of Curve_Screen in vehicle local space
        public Vector3 screenCenterLocal = new Vector3(0.04f, 0.53f, 0.44f);
        public Vector3 screenNormalLocal = new Vector3(0f, -0.2f, -1f).normalized;
        public Vector2 screenHalfSizeLocal = new Vector2(0.55f, 0.12f); // half width X, half height Y

        private void Awake()
        {
            if (!vehicleManager) vehicleManager = FindFirstObjectByType<VehicleCharacterManager>();
            if (!hmiManager) hmiManager = FindFirstObjectByType<HMIDisplayManager>();
            if (!rccCamera) rccCamera = FindFirstObjectByType<RCC_Camera>();
        }

        private void Start()
        {
            FindCabinCamera();
        }

        private void Update()
        {
            // Only active when player is inside the vehicle in FPS camera mode
            if (!IsInsideVehicleInFPS())
            {
                if (isScreenFocused)
                {
                    ExitScreenFocus(instant: true);
                }
                return;
            }

            HandleInput();

            if (isScreenFocused)
            {
                HandleScreenMouseInteraction();
            }
        }

        private bool IsInsideVehicleInFPS()
        {
            if (vehicleManager == null) return false;
            if (vehicleManager.currentState != VehicleCharacterManager.ControlState.InVehicle) return false;
            if (rccCamera == null) return false;
            return rccCamera.cameraMode == RCC_Camera.CameraMode.FPS;
        }

        private void FindCabinCamera()
        {
            if (cabinCameraTransform != null) return;

            if (vehicleManager != null && vehicleManager.carController != null)
            {
                var hoodCam = vehicleManager.carController.GetComponentInChildren<RCC_HoodCamera>(true);
                if (hoodCam != null)
                {
                    cabinCameraTransform = hoodCam.transform;
                }
            }
        }

        private void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame)
                {
                    ToggleScreenFocus();
                }
                else if (isScreenFocused && kb.escapeKey.wasPressedThisFrame)
                {
                    ExitScreenFocus();
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleScreenFocus();
            }
            else if (isScreenFocused && Input.GetKeyDown(KeyCode.Escape))
            {
                ExitScreenFocus();
            }
#endif
        }

        public void ToggleScreenFocus()
        {
            if (isScreenFocused)
                ExitScreenFocus();
            else
                EnterScreenFocus();
        }

        public void EnterScreenFocus()
        {
            FindCabinCamera();
            if (cabinCameraTransform == null) return;

            isScreenFocused = true;

            // Remember orbit setting and disable orbit during focus so mouse doesn't turn camera
            if (rccCamera != null)
            {
                originalOrbitSetting = rccCamera.useOrbitInHoodCameraMode;
                rccCamera.useOrbitInHoodCameraMode = false;
            }

            // Unlock and show mouse cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Play audio feedback
            var audio = VehicleAudioFeedback.Instance;
            if (audio) audio.PlayTouchBeep();

            // Smoothly move camera to focus position
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(TransitionCamera(focusScreenLocalPosition, Quaternion.Euler(focusScreenLocalEuler)));
        }

        public void ExitScreenFocus(bool instant = false)
        {
            isScreenFocused = false;

            // Restore orbit
            if (rccCamera != null)
            {
                rccCamera.useOrbitInHoodCameraMode = originalOrbitSetting;
            }

            // Lock and hide mouse cursor for normal driving
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            FindCabinCamera();
            if (cabinCameraTransform == null) return;

            if (instant)
            {
                cabinCameraTransform.localPosition = normalEyeLocalPosition;
                cabinCameraTransform.localRotation = Quaternion.Euler(normalEyeLocalEuler);
            }
            else
            {
                if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
                transitionCoroutine = StartCoroutine(TransitionCamera(normalEyeLocalPosition, Quaternion.Euler(normalEyeLocalEuler)));
            }
        }

        private IEnumerator TransitionCamera(Vector3 targetLocalPos, Quaternion targetLocalRot)
        {
            if (cabinCameraTransform == null) yield break;

            Vector3 startPos = cabinCameraTransform.localPosition;
            Quaternion startRot = cabinCameraTransform.localRotation;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
                cabinCameraTransform.localPosition = Vector3.Lerp(startPos, targetLocalPos, t);
                cabinCameraTransform.localRotation = Quaternion.Slerp(startRot, targetLocalRot, t);
                yield return null;
            }

            cabinCameraTransform.localPosition = targetLocalPos;
            cabinCameraTransform.localRotation = targetLocalRot;
            transitionCoroutine = null;
        }

        /// <summary>
        /// Translates mouse clicks on the 3D curved screen into 2D HMI events using mathematical plane intersection.
        /// Zero colliders attached to the vehicle = zero PhysX compound collider interference.
        /// </summary>
        private void HandleScreenMouseInteraction()
        {
            if (rccCamera == null || rccCamera.actualCamera == null) return;
            if (vehicleManager == null || vehicleManager.carController == null) return;
            if (hmiManager == null) return;

            Camera cam = rccCamera.actualCamera;
            Transform car = vehicleManager.carController.transform;

            // 1. Cast ray from mouse position through the camera
            Vector3 mousePos = Input.mousePosition;
            Ray ray = cam.ScreenPointToRay(mousePos);

            // 2. Transform ray into vehicle's local space
            Vector3 localOrigin = car.InverseTransformPoint(ray.origin);
            Vector3 localDir = car.InverseTransformDirection(ray.direction).normalized;

            // 3. Mathematical plane intersection in local space
            Plane screenPlane = new Plane(screenNormalLocal, screenCenterLocal);
            if (screenPlane.Raycast(new Ray(localOrigin, localDir), out float enter))
            {
                Vector3 hitLocal = localOrigin + localDir * enter;
                Vector3 offset = hitLocal - screenCenterLocal;

                // Check if hit is within the active screen bounds
                if (Mathf.Abs(offset.x) <= screenHalfSizeLocal.x && Mathf.Abs(offset.y) <= screenHalfSizeLocal.y)
                {
                    // Map local hit to normalized UV coordinates (0..1, 0..1)
                    float u = Mathf.Clamp01((offset.x + screenHalfSizeLocal.x) / (screenHalfSizeLocal.x * 2f));
                    float v = Mathf.Clamp01((offset.y + screenHalfSizeLocal.y) / (screenHalfSizeLocal.y * 2f));

                    // Convert to 1920x320 Canvas coordinate space
                    float canvasX = u * 1920f;
                    float canvasY = v * 320f;

                    // Handle mouse click
                    bool mouseClicked = false;
#if ENABLE_INPUT_SYSTEM
                    var mouse = Mouse.current;
                    if (mouse != null && mouse.leftButton.wasPressedThisFrame) mouseClicked = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
                    if (Input.GetMouseButtonDown(0)) mouseClicked = true;
#endif

                    if (mouseClicked)
                    {
                        ProcessHmiClick(canvasX, canvasY);
                    }
                }
            }
        }

        /// <summary>
        /// Maps canvas coordinates to HMI actions (Launcher cards, Dock Home, or back button).
        /// </summary>
        private void ProcessHmiClick(float x, float y)
        {
            if (hmiManager == null) return;

            var audio = VehicleAudioFeedback.Instance;

            // 1. Left Rail Dock (X < 140): Click Home Grid button
            if (x < 140f && y < 140f)
            {
                if (audio) audio.PlayTouchBeep();
                hmiManager.SelectApp(HMIDisplayManager.AAOSApp.Launcher);
                return;
            }

            // 2. When in Launcher Grid: Click one of the 8 App Cards
            if (hmiManager.currentApp == HMIDisplayManager.AAOSApp.Launcher)
            {
                // App cards are arranged in 2 rows of 4 columns between X: 420..1880, Y: 20..300
                if (x >= 420f && x <= 1880f && y >= 20f && y <= 300f)
                {
                    int col = Mathf.Clamp((int)((x - 420f) / 365f), 0, 3);
                    int row = (y >= 160f) ? 0 : 1; // Row 0 is top row, Row 1 is bottom row
                    int gridIdx = row * 4 + col;

                    if (gridIdx >= 0 && gridIdx < 8)
                    {
                        if (audio) audio.PlayTouchBeep();
                        hmiManager.selectedGridIndex = gridIdx;
                        hmiManager.LaunchAppByIndex(gridIdx);
                    }
                }
            }
            // 3. When in an App: Click [ ◀ LAUNCHER ] back button (X: 180..420, Y: 20..90)
            else
            {
                if (x >= 160f && x <= 440f && y >= 20f && y <= 100f)
                {
                    if (audio) audio.PlayTouchBeep();
                    hmiManager.SelectApp(HMIDisplayManager.AAOSApp.Launcher);
                }
            }
        }
    }
}
