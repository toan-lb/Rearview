using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rearview
{
    /// <summary>
    /// Manages the in-vehicle HMI (Human-Machine Interface) curved display for The Last Waypoint.
    /// Implements:
    /// 1. Unobstructed Driver Cluster: Positions speed, gear, and KM/H inside the upper steering wheel opening,
    ///    and places RPM, Battery, Temp, and DTC in the open area to the left of the steering wheel.
    /// 2. True AAOS Center Infotainment:
    ///    - Default state is the App Launcher Grid (App Drawer with interactive cards/icons).
    ///    - Selecting an app opens that feature full-screen on the center display.
    ///    - Each feature screen has a prominent "[ ◀ LAUNCHER ]" button to return to the app grid.
    ///    - Top status bar with Clock, Weather, Network badges, and a Home "[ ⊞ APPS ]" shortcut.
    /// 3. HVAC Screen Off: Ensures the lower HVAC screen does not mirror the center display.
    /// </summary>
    [AddComponentMenu("Rearview/HMI Display Manager")]
    [DefaultExecutionOrder(-5)]
    public class HMIDisplayManager : MonoBehaviour
    {
        public enum AAOSApp
        {
            Launcher = 0,    // App Launcher Grid (List of icon cards)
            Navigation = 1,  // Satellite Navigation & Father's Route Notes
            Radio = 2,       // FM Radio & Cassette Player with Audio Visualizer
            Diagnostics = 3, // OBD-II Diagnostics & DTC Fault Codes
            Settings = 4     // Cabin Controls & Baby Mode Stabilizer
        }

        [Header("--- Target Vehicle ---")]
        [Tooltip("The player RCC vehicle. Auto-found if null.")]
        public RCC_CarControllerV4 vehicle;

        [Header("--- Render Texture & Material ---")]
        [Tooltip("The RenderTexture rendered by the HMI camera and assigned to the screen material.")]
        public RenderTexture targetRenderTexture;

        [Tooltip("Material of the screen glass (plasticGlossy.001).")]
        public Material screenMaterial;

        [Header("--- Layer Settings ---")]
        [Tooltip("Layer name used for the isolated HMI camera and canvas.")]
        public string hmiLayerName = "P2P_Screen";

        [Header("--- UI References (Optional - Auto-generated if null) ---")]
        public Canvas hmiCanvas;
        public Camera hmiCamera;

        // ==========================================
        // Driver Cluster Elements (Driver side - Left)
        // ==========================================
        [Header("--- Driver Cluster UI Elements ---")]
        public TextMeshProUGUI speedText;
        public TextMeshProUGUI kmhText;
        public TextMeshProUGUI gearText;
        public TextMeshProUGUI rpmText;
        public Image rpmBar;
        public TextMeshProUGUI dtcText;
        public TextMeshProUGUI tempText;
        public TextMeshProUGUI batteryText;
        public Image headlightIcon;
        public Image handbrakeIcon;

        // ==========================================
        // AAOS Infotainment Elements (Center side - Right)
        // ==========================================
        [Header("--- AAOS Top Status Bar ---")]
        public TextMeshProUGUI clockText;
        public TextMeshProUGUI weatherText;
        public TextMeshProUGUI networkBadgeText;
        public Button statusHomeBtn;

        [Header("--- AAOS App State & Panels ---")]
        public AAOSApp currentApp = AAOSApp.Launcher;
        public GameObject launcherAppPanel;
        public GameObject navAppPanel;
        public GameObject radioAppPanel;
        public GameObject diagAppPanel;
        public GameObject settingsAppPanel;

        [Header("--- AAOS Launcher Grid Cards ---")]
        public Button launcherNavCardBtn;
        public Button launcherRadioCardBtn;
        public Button launcherDiagCardBtn;
        public Button launcherSettingsCardBtn;

        [Header("--- AAOS Navigation App Elements ---")]
        public TextMeshProUGUI navDestTitleText;
        public TextMeshProUGUI navDestDistText;
        public TextMeshProUGUI navConditionText;
        public TextMeshProUGUI navFatherNoteText;

        [Header("--- AAOS Radio / Media App Elements ---")]
        public TextMeshProUGUI radioStationText;
        public TextMeshProUGUI radioTrackText;
        public Image[] visualizerBars;

        [Header("--- AAOS Diagnostics App Elements ---")]
        public TextMeshProUGUI diagDtcText;
        public TextMeshProUGUI diagSeverityText;
        public TextMeshProUGUI diagTempText;
        public TextMeshProUGUI diagBatText;
        public TextMeshProUGUI diagRpmText;

        [Header("--- AAOS Settings App Elements ---")]
        public TextMeshProUGUI babyModeText;
        public TextMeshProUGUI babyModeStatusBadge;
        public TextMeshProUGUI babyModeDescText;

        // Legacy compatibility properties
        [HideInInspector] public TextMeshProUGUI waypointText;
        [HideInInspector] public TextMeshProUGUI fatherNoteText;

        [Header("--- Radio Stations (The Last Waypoint) ---")]
        public string[] radioStations = new string[]
        {
            "FM 94.5 MHz - Coastal Waves (Mưa Đêm)",
            "FM 101.2 MHz - Old Town Memories",
            "FM 88.9 MHz - Father's Tape: Lời Nhắn Của Bố (1998)",
            "FM 104.7 MHz - Sea Breeze Jazz"
        };

        public string[] radioTracks = new string[]
        {
            "Now Playing: Rainy Highway Melancholy",
            "Now Playing: Nostalgia on the Radio",
            "Cassette Audio: 'Chào con, xe này bố tự đóng...'",
            "Now Playing: Midnight Saxophone"
        };

        private int currentStationIndex = 0;
        private bool babyModeActive = true;
        private int currentDtcIndex = 0;
        private string[] dtcCodes = new string[]
        {
            "DTC: NO FAULT CODES DETECTED",
            "DTC: P0118 - CẢM BIẾN NHIỆT ĐỘ NƯỚC LÀM MÁT (CHẬP)",
            "DTC: P0300 - BỎ ĐÁNH LỬA ĐA XY-LANH (BUGI CŨ)",
            "DTC: P0420 - HIỆU SUẤT BẦU LỌC KHÍ THẢI KÉM"
        };

        private float simulatedTemp = 88f;
        private float simulatedBattery = 12.6f;
        private float visualizerTimer = 0f;

        // Styling Colors (AAOS Modern Automotive Palette)
        private static readonly Color ColorActiveCyan = new Color(0f, 0.9f, 1f, 1f);        // #00E5FF
        private static readonly Color ColorCardBg = new Color(0.08f, 0.12f, 0.18f, 0.95f);
        private static readonly Color ColorCardBorder = new Color(0.18f, 0.26f, 0.38f, 0.9f);
        private static readonly Color ColorTextPrimary = new Color(0.92f, 0.96f, 1f, 1f);
        private static readonly Color ColorTextSecondary = new Color(0.55f, 0.68f, 0.82f, 1f);
        private static readonly Color ColorAccentYellow = new Color(1f, 0.84f, 0.25f, 1f);
        private static readonly Color ColorOkGreen = new Color(0.25f, 0.95f, 0.45f, 1f);
        private static readonly Color ColorWarnOrange = new Color(1f, 0.45f, 0.2f, 1f);

        private void Awake()
        {
            InitDefaults();
        }

        private void Start()
        {
            FindVehicle();
            EnsureUIExists();
            SyncMaterialProperties();
            SelectApp(currentApp);
            CleanScreenColliders();
            EnsureHVACScreenOff();
        }

        private void InitDefaults()
        {
            if (targetRenderTexture == null)
            {
                targetRenderTexture = Resources.Load<RenderTexture>("P2P_Screen_RT");
                if (targetRenderTexture == null)
                {
#if UNITY_EDITOR
                    targetRenderTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Rearview/RenderTexture/P2P_Screen_RT.renderTexture");
#endif
                }
            }

            if (screenMaterial == null)
            {
#if UNITY_EDITOR
                screenMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Rearview/Material/Car/plasticGlossy.001.mat");
#endif
            }
        }

        private void FindVehicle()
        {
            if (vehicle == null)
            {
                vehicle = FindFirstObjectByType<RCC_CarControllerV4>();
            }
        }

        private void SyncMaterialProperties()
        {
            if (screenMaterial != null && targetRenderTexture != null)
            {
                screenMaterial.SetTexture("_BaseMap", targetRenderTexture);
                screenMaterial.SetTexture("_EmissionMap", targetRenderTexture);
                screenMaterial.SetTexture("_MainTex", targetRenderTexture);
                screenMaterial.SetColor("_BaseColor", Color.white);
                screenMaterial.SetColor("_EmissionColor", Color.white);
                screenMaterial.EnableKeyword("_EMISSION");
            }
        }

        private void CleanScreenColliders()
        {
            // Per RCC vehicle pipeline standards (rcc-vehicle-control-pipeline/SKILL.md):
            // Curve_Screen is strictly a visual display mesh and MUST NOT have any colliders
            // attached to avoid polluting the vehicle's dynamic Rigidbody compound shape in PhysX.
            if (vehicle != null)
            {
                Transform screenTrans = vehicle.transform.Find("Curve_Screen") ?? vehicle.transform.Find("Interior_Frame/Curve_Screen");
                if (screenTrans == null)
                {
                    MeshRenderer[] renderers = vehicle.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var mr in renderers)
                    {
                        if (mr.gameObject.name.Equals("Curve_Screen", System.StringComparison.OrdinalIgnoreCase))
                        {
                            screenTrans = mr.transform;
                            break;
                        }
                    }
                }

                if (screenTrans != null)
                {
                    Collider[] colliders = screenTrans.GetComponents<Collider>();
                    foreach (var col in colliders)
                    {
                        if (Application.isPlaying)
                            Destroy(col);
                        else
                            DestroyImmediate(col);
                    }
                }
            }
        }

        public void EnsureHVACScreenOff()
        {
            // Ensures the lower HVAC screen does not mirror the P2P center screen texture
            if (vehicle == null) FindVehicle();
            if (vehicle != null)
            {
                Material hvacOffMat = Resources.Load<Material>("HVAC_Screen_Off");
                if (hvacOffMat == null)
                {
#if UNITY_EDITOR
                    hvacOffMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Rearview/Material/Car/HVAC_Screen_Off.mat");
                    if (hvacOffMat == null)
                        hvacOffMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Rearview/Material/Car/plasticGlossy.mat");
#endif
                }

                if (hvacOffMat != null)
                {
                    MeshRenderer[] renderers = vehicle.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var mr in renderers)
                    {
                        if (mr.gameObject.name.Equals("HVAC_Screen", System.StringComparison.OrdinalIgnoreCase))
                        {
                            Material[] mats = mr.sharedMaterials;
                            for (int i = 0; i < mats.Length; i++)
                            {
                                if (mats[i] != null && mats[i].name.Contains("plasticGlossy.001"))
                                {
                                    mats[i] = hvacOffMat;
                                }
                            }
                            mr.sharedMaterials = mats;
                        }
                    }
                }
            }
        }

        private void Update()
        {
            FindVehicle();
            HandleInput();
            UpdateTelemetry();
            UpdateInfotainment();
        }

        #region Input & AAOS App Switching
        private void HandleInput()
        {
            // AAOS Quick Keys:
            // [1] or [ESC]: Open App Launcher Grid
            // [2]: Open Navigation App
            // [3]: Open Radio / Media App
            // [4]: Open Diagnostics (DTC) App
            // [5]: Open Cabin Settings App
            // [Tab]: Cycle through apps
            // [Q] / [E] or Left/Right Arrow: Previous / Next Radio Station (when in Radio)
            // [R]: Scan / Cycle DTC
            // [B]: Toggle Baby Mode
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                {
                    SelectApp(AAOSApp.Launcher);
                }
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                {
                    SelectApp(AAOSApp.Navigation);
                }
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                {
                    SelectApp(AAOSApp.Radio);
                }
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                {
                    SelectApp(AAOSApp.Diagnostics);
                }
                else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame)
                {
                    SelectApp(AAOSApp.Settings);
                }
                else if (kb.tabKey.wasPressedThisFrame)
                {
                    CycleApp();
                }

                // Sub-controls in apps
                if (kb.qKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
                {
                    PrevStation();
                }
                if (kb.eKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                {
                    NextStation();
                }
                if (kb.rKey.wasPressedThisFrame)
                {
                    CycleDTC();
                }
                if (kb.bKey.wasPressedThisFrame)
                {
                    ToggleBabyMode();
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Escape))
            {
                SelectApp(AAOSApp.Launcher);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SelectApp(AAOSApp.Navigation);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SelectApp(AAOSApp.Radio);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                SelectApp(AAOSApp.Diagnostics);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                SelectApp(AAOSApp.Settings);
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                CycleApp();
            }

            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PrevStation();
            }
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                NextStation();
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                CycleDTC();
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                ToggleBabyMode();
            }
#endif
        }

        public void SelectApp(AAOSApp app)
        {
            currentApp = app;

            if (launcherAppPanel) launcherAppPanel.SetActive(app == AAOSApp.Launcher);
            if (navAppPanel) navAppPanel.SetActive(app == AAOSApp.Navigation);
            if (radioAppPanel) radioAppPanel.SetActive(app == AAOSApp.Radio);
            if (diagAppPanel) diagAppPanel.SetActive(app == AAOSApp.Diagnostics);
            if (settingsAppPanel) settingsAppPanel.SetActive(app == AAOSApp.Settings);
        }

        public void SelectApp(int appIndex)
        {
            SelectApp((AAOSApp)Mathf.Clamp(appIndex, 0, 4));
        }

        public void CycleApp()
        {
            int next = ((int)currentApp + 1) % 5;
            SelectApp((AAOSApp)next);
        }

        public void NextStation()
        {
            currentStationIndex = (currentStationIndex + 1) % radioStations.Length;
        }

        public void PrevStation()
        {
            currentStationIndex = (currentStationIndex - 1 + radioStations.Length) % radioStations.Length;
        }

        public void CycleDTC()
        {
            currentDtcIndex = (currentDtcIndex + 1) % dtcCodes.Length;
        }

        public void ToggleBabyMode()
        {
            babyModeActive = !babyModeActive;
        }
        #endregion

        #region Telemetry & Infotainment Updates
        private void UpdateTelemetry()
        {
            float speed = 0f;
            float rpm = 800f;
            string gear = "P";
            bool headlights = false;
            bool handbrake = true;

            if (vehicle != null)
            {
                speed = Mathf.Abs(vehicle.speed);
                rpm = vehicle.engineRPM;
                headlights = vehicle.lowBeamHeadLightsOn || vehicle.highBeamHeadLightsOn;
                handbrake = vehicle.handbrakeInput > 0.5f;

                if (vehicle.NGear)
                    gear = "N";
                else if (vehicle.direction == -1)
                    gear = "R";
                else
                    gear = "D" + (vehicle.currentGear + 1).ToString();

                // Dynamic temperature simulation based on engine stress
                simulatedTemp = Mathf.MoveTowards(simulatedTemp, 85f + (speed / 160f) * 20f, Time.deltaTime * 0.5f);
                simulatedBattery = 12.4f + (rpm / 7000f) * 1.8f;
            }

            if (speedText != null)
                speedText.text = speed.ToString("0");

            if (gearText != null)
                gearText.text = gear;

            if (rpmText != null)
                rpmText.text = rpm.ToString("0") + " RPM";

            if (rpmBar != null)
            {
                float fill = Mathf.Clamp01(rpm / 7000f);
                rpmBar.fillAmount = fill;
                rpmBar.color = Color.Lerp(ColorActiveCyan, Color.red, Mathf.InverseLerp(0.7f, 1f, fill));
            }

            if (tempText != null)
            {
                tempText.text = $"TEMP: {simulatedTemp:0}°C";
                tempText.color = simulatedTemp > 100f ? Color.red : ColorOkGreen;
            }

            if (batteryText != null)
                batteryText.text = $"BAT: {simulatedBattery:0.0}V";

            if (dtcText != null)
            {
                dtcText.text = dtcCodes[currentDtcIndex];
                dtcText.color = currentDtcIndex == 0 ? ColorOkGreen : ColorWarnOrange;
            }

            if (headlightIcon != null)
                headlightIcon.color = headlights ? ColorActiveCyan : new Color(0.3f, 0.3f, 0.3f, 0.4f);

            if (handbrakeIcon != null)
                handbrakeIcon.color = handbrake ? new Color(1f, 0.2f, 0.2f) : new Color(0.3f, 0.3f, 0.3f, 0.4f);
        }

        private void UpdateInfotainment()
        {
            // 1. Top System Bar (Always visible)
            if (clockText != null)
                clockText.text = System.DateTime.Now.ToString("hh:mm tt");

            if (weatherText != null)
                weatherText.text = "COASTAL RAIN  16°C";

            if (networkBadgeText != null)
                networkBadgeText.text = "GPS: LOCKED | 4G LTE | HMI v2.4";

            // 2. Audio Visualizer (Animates during Radio app)
            visualizerTimer += Time.deltaTime * 8f;
            if (visualizerBars != null && currentApp == AAOSApp.Radio)
            {
                for (int i = 0; i < visualizerBars.Length; i++)
                {
                    if (visualizerBars[i] != null)
                    {
                        float wave = Mathf.PingPong(visualizerTimer + i * 0.7f, 1f);
                        visualizerBars[i].fillAmount = Mathf.Lerp(0.12f, 0.98f, wave);
                    }
                }
            }

            // 3. Dynamic content per active app
            switch (currentApp)
            {
                case AAOSApp.Navigation:
                    UpdateNavApp();
                    break;
                case AAOSApp.Radio:
                    UpdateRadioApp();
                    break;
                case AAOSApp.Diagnostics:
                    UpdateDiagnosticsApp();
                    break;
                case AAOSApp.Settings:
                    UpdateSettingsApp();
                    break;
            }
        }

        private void UpdateNavApp()
        {
            if (navDestTitleText != null)
                navDestTitleText.text = "WAYPOINT: BÃI PHẾ LIỆU BÁC BA (CHƯƠNG 2)";

            if (navDestDistText != null)
                navDestDistText.text = "KHOẢNG CÁCH: 1.8 KM | THỜI GIAN: ~4 PHÚT | HƯỚNG BẮC";

            if (navConditionText != null)
                navConditionText.text = "ĐIỀU KIỆN: ĐƯỜNG VEN BIỂN ĐÊM MƯA - TẦM NHÌN HẠN CHẾ DO SƯƠNG MÙ";

            if (navFatherNoteText != null)
                navFatherNoteText.text = "\"Ghi chú của Bố: Đến bãi xe gặp anh Ba hỏi cái bo mạch điều khiển màn hình cũ. Đừng quên mang cho ổng gói thuốc lá.\"";
        }

        private void UpdateRadioApp()
        {
            if (radioStationText != null)
                radioStationText.text = radioStations[currentStationIndex];

            if (radioTrackText != null)
                radioTrackText.text = radioTracks[currentStationIndex];
        }

        private void UpdateDiagnosticsApp()
        {
            if (diagDtcText != null)
            {
                diagDtcText.text = dtcCodes[currentDtcIndex];
                diagDtcText.color = currentDtcIndex == 0 ? ColorOkGreen : ColorWarnOrange;
            }

            if (diagSeverityText != null)
            {
                if (currentDtcIndex == 0)
                {
                    diagSeverityText.text = "TRẠNG THÁI: TẤT CẢ HỆ THỐNG HOẠT ĐỘNG BÌNH THƯỜNG";
                    diagSeverityText.color = ColorOkGreen;
                }
                else
                {
                    diagSeverityText.text = "MỨC ĐỘ: CẢNH BÁO - CẦN THAY THẾ LINH KIỆN TRƯỚC KHI VƯỢT ĐÈO";
                    diagSeverityText.color = ColorWarnOrange;
                }
            }

            if (diagTempText != null)
            {
                diagTempText.text = $"NHIỆT ĐỘ LÀM MÁT: {simulatedTemp:0}°C";
                diagTempText.color = simulatedTemp > 100f ? Color.red : ColorOkGreen;
            }

            if (diagBatText != null)
                diagBatText.text = $"ĐIỆN ÁP ẮC QUY: {simulatedBattery:0.0}V (BÌNH THƯỜNG)";

            if (diagRpmText != null)
                diagRpmText.text = $"VÒNG TUA MÁY: {(vehicle != null ? vehicle.engineRPM : 800f):0} RPM";
        }

        private void UpdateSettingsApp()
        {
            if (babyModeText != null)
                babyModeText.text = "CABIN STABILIZER (CHẾ ĐỘ GIỮ ÊM GHẾ SAU)";

            if (babyModeStatusBadge != null)
            {
                babyModeStatusBadge.text = babyModeActive ? "TRẠNG THÁI: [ ĐANG BẬT - BẢO VỆ GIẤC NGỦ ]" : "TRẠNG THÁI: [ ĐANG TẮT ]";
                babyModeStatusBadge.color = babyModeActive ? ColorOkGreen : ColorWarnOrange;
            }

            if (babyModeDescText != null)
                babyModeDescText.text = "Tự động cân bằng độ nhún phuộc và làm mượt chân ga để giữ cho đứa bé ở hàng ghế sau không bị giật mình thức giấc trong đêm mưa.";
        }
        #endregion

        #region Procedural UI Hierarchy Builder
        /// <summary>
        /// Automatically constructs the full AAOS camera, canvas, app grid launcher, and app screens.
        /// </summary>
        public void EnsureUIExists(bool forceRebuild = false)
        {
            if (!forceRebuild && hmiCanvas != null && hmiCamera != null && launcherAppPanel != null)
                return;

            int targetLayer = LayerMask.NameToLayer(hmiLayerName);
            if (targetLayer < 0) targetLayer = LayerMask.NameToLayer("UI");
            if (targetLayer < 0) targetLayer = 0;

            // 1. Create or Find HMI Root & Camera
            GameObject hmiRoot = GameObject.Find("HMI_System");
            if (hmiRoot == null)
            {
                hmiRoot = new GameObject("HMI_System");
                hmiRoot.transform.position = new Vector3(0, -500f, 0); // Isolated off-screen space
            }

            if (hmiCamera == null)
            {
                Transform existingCam = hmiRoot.transform.Find("HMI_Camera");
                if (existingCam != null)
                {
                    hmiCamera = existingCam.GetComponent<Camera>();
                }
                else
                {
                    GameObject camObj = new GameObject("HMI_Camera");
                    camObj.transform.SetParent(hmiRoot.transform, false);
                    camObj.transform.localPosition = new Vector3(0, 0, -10f);
                    hmiCamera = camObj.AddComponent<Camera>();
                }

                hmiCamera.clearFlags = CameraClearFlags.SolidColor;
                hmiCamera.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
                hmiCamera.orthographic = true;
                hmiCamera.orthographicSize = 1.6f;
                hmiCamera.nearClipPlane = 0.1f;
                hmiCamera.farClipPlane = 20f;
                hmiCamera.cullingMask = 1 << targetLayer;
                hmiCamera.targetTexture = targetRenderTexture;
            }

            // 2. Create or Reset Canvas
            Transform existingCanvas = hmiRoot.transform.Find("HMI_Canvas");
            if (forceRebuild && existingCanvas != null)
            {
                DestroyImmediate(existingCanvas.gameObject);
                existingCanvas = null;
            }

            GameObject canvasObj;
            if (existingCanvas != null)
            {
                canvasObj = existingCanvas.gameObject;
                hmiCanvas = canvasObj.GetComponent<Canvas>();
            }
            else
            {
                canvasObj = new GameObject("HMI_Canvas");
                canvasObj.transform.SetParent(hmiRoot.transform, false);
                canvasObj.layer = targetLayer;
                hmiCanvas = canvasObj.AddComponent<Canvas>();
                hmiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                hmiCanvas.worldCamera = hmiCamera;
                hmiCanvas.planeDistance = 5f;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 320);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            BuildCanvasLayout(canvasObj, targetLayer);
        }

        private void BuildCanvasLayout(GameObject canvasObj, int layer)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // Clean up existing children if any
            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }

            // Global Background Panel
            GameObject bg = CreateUIObject("Background", canvasObj.transform, layer);
            RectTransform bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.04f, 0.06f, 1f);

            // ==========================================
            // LEFT SECTION: DRIVER CLUSTER (0 to 0.48)
            // ==========================================
            BuildDriverCluster(canvasObj, layer, font);

            // ==========================================
            // CENTER DIVIDER LINE (0.485 to 0.49)
            // ==========================================
            GameObject divider = CreateUIObject("Divider", canvasObj.transform, layer);
            RectTransform divRect = divider.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.485f, 0.05f);
            divRect.anchorMax = new Vector2(0.490f, 0.95f);
            divRect.offsetMin = Vector2.zero;
            divRect.offsetMax = Vector2.zero;
            Image divImg = divider.AddComponent<Image>();
            divImg.color = new Color(0.18f, 0.24f, 0.32f, 0.5f);

            // ==========================================
            // RIGHT SECTION: AAOS INFOTAINMENT (0.495 to 1.0)
            // ==========================================
            BuildAAOSInfotainment(canvasObj, layer, font);

            Debug.Log("<color=cyan><b>[HMIDisplayManager]</b></color> Đã thiết lập hoàn chỉnh hệ thống AAOS HMI Display (Unobstructed Cluster + App Grid Launcher)!");
        }

        private void BuildDriverCluster(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject clusterPanel = CreateUIObject("Driver_Cluster_Panel", parent.transform, layer);
            RectTransform cRect = clusterPanel.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 0f);
            cRect.anchorMax = new Vector2(0.48f, 1f);
            cRect.offsetMin = new Vector2(10f, 10f);
            cRect.offsetMax = new Vector2(-10f, -10f);

            // -------------------------------------------------------------
            // 1. PRIMARY SPEEDOMETER & GEAR: INSIDE STEERING WHEEL OPENING
            // In the cockpit view, the steering wheel upper gap is at X: 0.42 to 0.82, Y: 0.40 to 0.76.
            // -------------------------------------------------------------
            // Big Speed Number
            GameObject speedObj = CreateUIObject("Speed_Number", clusterPanel.transform, layer);
            RectTransform speedRect = speedObj.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(0.42f, 0.40f);
            speedRect.anchorMax = new Vector2(0.70f, 0.76f);
            speedRect.offsetMin = Vector2.zero;
            speedRect.offsetMax = Vector2.zero;
            speedText = speedObj.AddComponent<TextMeshProUGUI>();
            if (font) speedText.font = font;
            speedText.text = "0";
            speedText.fontSize = 72;
            speedText.fontStyle = FontStyles.Bold;
            speedText.alignment = TextAlignmentOptions.Center;
            speedText.color = ColorTextPrimary;

            // Gear Label (above KM/H)
            GameObject gearObj = CreateUIObject("Gear_Label", clusterPanel.transform, layer);
            RectTransform gearRect = gearObj.AddComponent<RectTransform>();
            gearRect.anchorMin = new Vector2(0.70f, 0.58f);
            gearRect.anchorMax = new Vector2(0.85f, 0.76f);
            gearRect.offsetMin = Vector2.zero;
            gearRect.offsetMax = Vector2.zero;
            gearText = gearObj.AddComponent<TextMeshProUGUI>();
            if (font) gearText.font = font;
            gearText.text = "P";
            gearText.fontSize = 30;
            gearText.fontStyle = FontStyles.Bold;
            gearText.alignment = TextAlignmentOptions.Left;
            gearText.color = ColorAccentYellow;

            // KM/H Unit Label (below Gear)
            GameObject kmhObj = CreateUIObject("KMH_Label", clusterPanel.transform, layer);
            RectTransform kmhRect = kmhObj.AddComponent<RectTransform>();
            kmhRect.anchorMin = new Vector2(0.70f, 0.40f);
            kmhRect.anchorMax = new Vector2(0.88f, 0.58f);
            kmhRect.offsetMin = Vector2.zero;
            kmhRect.offsetMax = Vector2.zero;
            kmhText = kmhObj.AddComponent<TextMeshProUGUI>();
            if (font) kmhText.font = font;
            kmhText.text = "KM/H";
            kmhText.fontSize = 16;
            kmhText.fontStyle = FontStyles.Bold;
            kmhText.alignment = TextAlignmentOptions.Left;
            kmhText.color = ColorTextSecondary;

            // -------------------------------------------------------------
            // 2. UNOBSTRUCTED LEFT AREA: VISIBLE TO THE LEFT OF STEERING WHEEL (X: 0.03 to 0.38)
            // -------------------------------------------------------------
            // Top Row: Battery & Temp
            GameObject battObj = CreateUIObject("Battery_Text", clusterPanel.transform, layer);
            RectTransform battRect = battObj.AddComponent<RectTransform>();
            battRect.anchorMin = new Vector2(0.03f, 0.78f);
            battRect.anchorMax = new Vector2(0.20f, 0.94f);
            battRect.offsetMin = Vector2.zero;
            battRect.offsetMax = Vector2.zero;
            batteryText = battObj.AddComponent<TextMeshProUGUI>();
            if (font) batteryText.font = font;
            batteryText.text = "BAT: 12.6V";
            batteryText.fontSize = 15;
            batteryText.color = ColorTextSecondary;

            GameObject tempObj = CreateUIObject("Temp_Text", clusterPanel.transform, layer);
            RectTransform tempRect = tempObj.AddComponent<RectTransform>();
            tempRect.anchorMin = new Vector2(0.21f, 0.78f);
            tempRect.anchorMax = new Vector2(0.38f, 0.94f);
            tempRect.offsetMin = Vector2.zero;
            tempRect.offsetMax = Vector2.zero;
            tempText = tempObj.AddComponent<TextMeshProUGUI>();
            if (font) tempText.font = font;
            tempText.text = "TEMP: 88°C";
            tempText.fontSize = 15;
            tempText.color = ColorTextSecondary;

            // Middle: RPM Text & RPM Horizontal Bar
            GameObject rpmTxtObj = CreateUIObject("RPM_Text", clusterPanel.transform, layer);
            RectTransform rpmTxtRect = rpmTxtObj.AddComponent<RectTransform>();
            rpmTxtRect.anchorMin = new Vector2(0.03f, 0.58f);
            rpmTxtRect.anchorMax = new Vector2(0.38f, 0.74f);
            rpmTxtRect.offsetMin = Vector2.zero;
            rpmTxtRect.offsetMax = Vector2.zero;
            rpmText = rpmTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) rpmText.font = font;
            rpmText.text = "800 RPM";
            rpmText.fontSize = 19;
            rpmText.fontStyle = FontStyles.Bold;
            rpmText.color = ColorActiveCyan;

            GameObject rpmBg = CreateUIObject("RPM_Bar_BG", clusterPanel.transform, layer);
            RectTransform rpmBgRect = rpmBg.AddComponent<RectTransform>();
            rpmBgRect.anchorMin = new Vector2(0.03f, 0.46f);
            rpmBgRect.anchorMax = new Vector2(0.38f, 0.54f);
            rpmBgRect.offsetMin = Vector2.zero;
            rpmBgRect.offsetMax = Vector2.zero;
            Image rpmBgImg = rpmBg.AddComponent<Image>();
            rpmBgImg.color = new Color(0.12f, 0.16f, 0.22f, 0.8f);

            GameObject rpmFill = CreateUIObject("RPM_Bar_Fill", rpmBg.transform, layer);
            RectTransform rpmFillRect = rpmFill.AddComponent<RectTransform>();
            rpmFillRect.anchorMin = Vector2.zero;
            rpmFillRect.anchorMax = Vector2.one;
            rpmFillRect.offsetMin = Vector2.zero;
            rpmFillRect.offsetMax = Vector2.zero;
            rpmBar = rpmFill.AddComponent<Image>();
            rpmBar.type = Image.Type.Filled;
            rpmBar.fillMethod = Image.FillMethod.Horizontal;
            rpmBar.fillOrigin = 0;
            rpmBar.fillAmount = 0.2f;
            rpmBar.color = ColorActiveCyan;

            // Bottom: DTC Fault Code
            GameObject dtcObj = CreateUIObject("DTC_Text", clusterPanel.transform, layer);
            RectTransform dtcRect = dtcObj.AddComponent<RectTransform>();
            dtcRect.anchorMin = new Vector2(0.03f, 0.16f);
            dtcRect.anchorMax = new Vector2(0.40f, 0.38f);
            dtcRect.offsetMin = Vector2.zero;
            dtcRect.offsetMax = Vector2.zero;
            dtcText = dtcObj.AddComponent<TextMeshProUGUI>();
            if (font) dtcText.font = font;
            dtcText.text = "DTC: NO FAULT CODES DETECTED";
            dtcText.fontSize = 12;
            dtcText.color = ColorOkGreen;
        }

        private void BuildAAOSInfotainment(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject infoPanel = CreateUIObject("AAOS_Infotainment_Panel", parent.transform, layer);
            RectTransform iRect = infoPanel.AddComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.495f, 0f);
            iRect.anchorMax = new Vector2(1f, 1f);
            iRect.offsetMin = new Vector2(10f, 10f);
            iRect.offsetMax = new Vector2(-15f, -10f);

            // 1. AAOS Top Status Bar (0 to 1 horizontal, 0.86 to 1.0 vertical)
            GameObject topBar = CreateUIObject("AAOS_Top_Status_Bar", infoPanel.transform, layer);
            RectTransform tbRect = topBar.AddComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0f, 0.86f);
            tbRect.anchorMax = new Vector2(1f, 1f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;

            // Shortcut Button to Open Launcher Grid
            GameObject homeBtnObj = CreateUIObject("Btn_Home_Launcher", topBar.transform, layer);
            RectTransform hbRect = homeBtnObj.AddComponent<RectTransform>();
            hbRect.anchorMin = new Vector2(0f, 0f);
            hbRect.anchorMax = new Vector2(0.15f, 1f);
            hbRect.offsetMin = Vector2.zero;
            hbRect.offsetMax = Vector2.zero;
            Image hbImg = homeBtnObj.AddComponent<Image>();
            hbImg.color = new Color(0.12f, 0.20f, 0.30f, 0.9f);
            statusHomeBtn = homeBtnObj.AddComponent<Button>();
            statusHomeBtn.targetGraphic = hbImg;
            statusHomeBtn.onClick.AddListener(() => SelectApp(AAOSApp.Launcher));

            GameObject hbTxtObj = CreateUIObject("Text", homeBtnObj.transform, layer);
            RectTransform hbtRect = hbTxtObj.AddComponent<RectTransform>();
            hbtRect.anchorMin = Vector2.zero;
            hbtRect.anchorMax = Vector2.one;
            hbtRect.offsetMin = Vector2.zero;
            hbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hbTxt = hbTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) hbTxt.font = font;
            hbTxt.text = "⊞ APPS (1)";
            hbTxt.fontSize = 13;
            hbTxt.fontStyle = FontStyles.Bold;
            hbTxt.alignment = TextAlignmentOptions.Center;
            hbTxt.color = ColorActiveCyan;

            // Clock Text
            GameObject clockObj = CreateUIObject("Clock_Text", topBar.transform, layer);
            RectTransform clkRect = clockObj.AddComponent<RectTransform>();
            clkRect.anchorMin = new Vector2(0.17f, 0f);
            clkRect.anchorMax = new Vector2(0.35f, 1f);
            clkRect.offsetMin = Vector2.zero;
            clkRect.offsetMax = Vector2.zero;
            clockText = clockObj.AddComponent<TextMeshProUGUI>();
            if (font) clockText.font = font;
            clockText.text = "04:45 AM";
            clockText.fontSize = 19;
            clockText.fontStyle = FontStyles.Bold;
            clockText.color = ColorTextPrimary;

            // Weather Text
            GameObject weatherObj = CreateUIObject("Weather_Text", topBar.transform, layer);
            RectTransform wthRect = weatherObj.AddComponent<RectTransform>();
            wthRect.anchorMin = new Vector2(0.36f, 0f);
            wthRect.anchorMax = new Vector2(0.65f, 1f);
            wthRect.offsetMin = Vector2.zero;
            wthRect.offsetMax = Vector2.zero;
            weatherText = weatherObj.AddComponent<TextMeshProUGUI>();
            if (font) weatherText.font = font;
            weatherText.text = "COASTAL RAIN  16°C";
            weatherText.fontSize = 16;
            weatherText.color = ColorTextSecondary;

            // System Badges
            GameObject netObj = CreateUIObject("Network_Badge_Text", topBar.transform, layer);
            RectTransform netRect = netObj.AddComponent<RectTransform>();
            netRect.anchorMin = new Vector2(0.66f, 0f);
            netRect.anchorMax = new Vector2(0.99f, 1f);
            netRect.offsetMin = Vector2.zero;
            netRect.offsetMax = Vector2.zero;
            networkBadgeText = netObj.AddComponent<TextMeshProUGUI>();
            if (font) networkBadgeText.font = font;
            networkBadgeText.text = "GPS: LOCKED | 4G LTE | HMI v2.4";
            networkBadgeText.fontSize = 13;
            networkBadgeText.alignment = TextAlignmentOptions.Right;
            networkBadgeText.color = new Color(0.4f, 0.7f, 0.9f, 0.9f);

            // 2. AAOS Main Content Area (0 to 1 horizontal, 0 to 0.84 vertical)
            GameObject contentArea = CreateUIObject("AAOS_Content_Area", infoPanel.transform, layer);
            RectTransform caRect = contentArea.AddComponent<RectTransform>();
            caRect.anchorMin = new Vector2(0f, 0f);
            caRect.anchorMax = new Vector2(1f, 0.84f);
            caRect.offsetMin = Vector2.zero;
            caRect.offsetMax = Vector2.zero;

            // Build Launcher Grid and the 4 Fullscreen App Screens
            BuildAppLauncherGrid(contentArea, layer, font);
            BuildNavApp(contentArea, layer, font);
            BuildRadioApp(contentArea, layer, font);
            BuildDiagnosticsApp(contentArea, layer, font);
            BuildSettingsApp(contentArea, layer, font);
        }

        private void BuildAppLauncherGrid(GameObject parent, int layer, TMP_FontAsset font)
        {
            launcherAppPanel = CreateUIObject("App_Launcher_Grid_Panel", parent.transform, layer);
            RectTransform lRect = launcherAppPanel.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = Vector2.zero;
            lRect.offsetMax = Vector2.zero;

            // Section Header
            GameObject headerObj = CreateUIObject("Launcher_Header", launcherAppPanel.transform, layer);
            RectTransform hRect = headerObj.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.02f, 0.84f);
            hRect.anchorMax = new Vector2(0.98f, 0.98f);
            hRect.offsetMin = Vector2.zero;
            hRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hTxt = headerObj.AddComponent<TextMeshProUGUI>();
            if (font) hTxt.font = font;
            hTxt.text = "CÁC ỨNG DỤNG XE (AAOS APP LAUNCHER) - CHỌN BIỂU TƯỢNG ĐỂ MỞ:";
            hTxt.fontSize = 13;
            hTxt.fontStyle = FontStyles.Bold;
            hTxt.color = ColorTextSecondary;

            // 4 App Cards arranged in a row (Each card is 23% wide with 2% gaps)
            launcherNavCardBtn = CreateLauncherCard(
                launcherAppPanel.transform, layer, font,
                new Vector2(0.01f, 0.05f), new Vector2(0.245f, 0.82f),
                "🧭", "BẢN ĐỒ", "Lộ trình & Điểm đến", "[Phím 2]",
                () => SelectApp(AAOSApp.Navigation)
            );

            launcherRadioCardBtn = CreateLauncherCard(
                launcherAppPanel.transform, layer, font,
                new Vector2(0.26f, 0.05f), new Vector2(0.495f, 0.82f),
                "📻", "RADIO FM", "Đài phát & Cassette", "[Phím 3]",
                () => SelectApp(AAOSApp.Radio)
            );

            launcherDiagCardBtn = CreateLauncherCard(
                launcherAppPanel.transform, layer, font,
                new Vector2(0.51f, 0.05f), new Vector2(0.745f, 0.82f),
                "🔧", "CHẨN ĐOÁN", "Mã lỗi động cơ DTC", "[Phím 4]",
                () => SelectApp(AAOSApp.Diagnostics)
            );

            launcherSettingsCardBtn = CreateLauncherCard(
                launcherAppPanel.transform, layer, font,
                new Vector2(0.76f, 0.05f), new Vector2(0.99f, 0.82f),
                "⚙️", "CÀI ĐẶT", "Cabin & Baby Mode", "[Phím 5]",
                () => SelectApp(AAOSApp.Settings)
            );
        }

        private Button CreateLauncherCard(Transform parent, int layer, TMP_FontAsset font, Vector2 min, Vector2 max, string icon, string title, string sub, string hotkey, UnityEngine.Events.UnityAction onClick)
        {
            GameObject card = CreateUIObject($"Card_{title}", parent, layer);
            RectTransform rt = card.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = card.AddComponent<Image>();
            img.color = ColorCardBg;

            Button btn = card.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            // Icon Text
            GameObject iconObj = CreateUIObject("Icon", card.transform, layer);
            RectTransform icRect = iconObj.AddComponent<RectTransform>();
            icRect.anchorMin = new Vector2(0.1f, 0.52f);
            icRect.anchorMax = new Vector2(0.9f, 0.92f);
            icRect.offsetMin = Vector2.zero;
            icRect.offsetMax = Vector2.zero;
            TextMeshProUGUI icTxt = iconObj.AddComponent<TextMeshProUGUI>();
            if (font) icTxt.font = font;
            icTxt.text = icon;
            icTxt.fontSize = 44;
            icTxt.alignment = TextAlignmentOptions.Center;

            // Title Text
            GameObject titleObj = CreateUIObject("Title", card.transform, layer);
            RectTransform tRect = titleObj.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.05f, 0.30f);
            tRect.anchorMax = new Vector2(0.95f, 0.52f);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
            TextMeshProUGUI tTxt = titleObj.AddComponent<TextMeshProUGUI>();
            if (font) tTxt.font = font;
            tTxt.text = title;
            tTxt.fontSize = 18;
            tTxt.fontStyle = FontStyles.Bold;
            tTxt.alignment = TextAlignmentOptions.Center;
            tTxt.color = ColorTextPrimary;

            // Subtitle Text
            GameObject subObj = CreateUIObject("Sub", card.transform, layer);
            RectTransform sRect = subObj.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.05f, 0.14f);
            sRect.anchorMax = new Vector2(0.95f, 0.30f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;
            TextMeshProUGUI sTxt = subObj.AddComponent<TextMeshProUGUI>();
            if (font) sTxt.font = font;
            sTxt.text = sub;
            sTxt.fontSize = 12;
            sTxt.alignment = TextAlignmentOptions.Center;
            sTxt.color = ColorTextSecondary;

            // Hotkey Badge
            GameObject hkObj = CreateUIObject("Hotkey", card.transform, layer);
            RectTransform hkRect = hkObj.AddComponent<RectTransform>();
            hkRect.anchorMin = new Vector2(0.05f, 0.02f);
            hkRect.anchorMax = new Vector2(0.95f, 0.14f);
            hkRect.offsetMin = Vector2.zero;
            hkRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hkTxt = hkObj.AddComponent<TextMeshProUGUI>();
            if (font) hkTxt.font = font;
            hkTxt.text = hotkey;
            hkTxt.fontSize = 11;
            hkTxt.alignment = TextAlignmentOptions.Center;
            hkTxt.color = ColorActiveCyan;

            return btn;
        }

        private void BuildNavApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            navAppPanel = CreateUIObject("App_Nav_Panel", parent.transform, layer);
            RectTransform nRect = navAppPanel.AddComponent<RectTransform>();
            nRect.anchorMin = Vector2.zero;
            nRect.anchorMax = Vector2.one;
            nRect.offsetMin = Vector2.zero;
            nRect.offsetMax = Vector2.zero;

            GameObject card = CreateCard("Nav_Full_Card", navAppPanel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("HỆ THỐNG ĐỊNH VỊ VỆ TINH (GPS NAVIGATION)", card.transform, layer, font);

            GameObject destObj = CreateUIObject("Dest_Title", card.transform, layer);
            RectTransform destRect = destObj.AddComponent<RectTransform>();
            destRect.anchorMin = new Vector2(0.04f, 0.58f);
            destRect.anchorMax = new Vector2(0.96f, 0.78f);
            destRect.offsetMin = Vector2.zero;
            destRect.offsetMax = Vector2.zero;
            navDestTitleText = destObj.AddComponent<TextMeshProUGUI>();
            if (font) navDestTitleText.font = font;
            navDestTitleText.text = "WAYPOINT: BÃI PHẾ LIỆU BÁC BA (CHƯƠNG 2)";
            navDestTitleText.fontSize = 22;
            navDestTitleText.fontStyle = FontStyles.Bold;
            navDestTitleText.color = ColorAccentYellow;

            GameObject distObj = CreateUIObject("Dist_Info", card.transform, layer);
            RectTransform distRect = distObj.AddComponent<RectTransform>();
            distRect.anchorMin = new Vector2(0.04f, 0.40f);
            distRect.anchorMax = new Vector2(0.96f, 0.58f);
            distRect.offsetMin = Vector2.zero;
            distRect.offsetMax = Vector2.zero;
            navDestDistText = distObj.AddComponent<TextMeshProUGUI>();
            if (font) navDestDistText.font = font;
            navDestDistText.text = "KHOẢNG CÁCH: 1.8 KM | THỜI GIAN: ~4 PHÚT | HƯỚNG BẮC";
            navDestDistText.fontSize = 16;
            navDestDistText.color = ColorTextPrimary;

            GameObject condObj = CreateUIObject("Condition_Info", card.transform, layer);
            RectTransform condRect = condObj.AddComponent<RectTransform>();
            condRect.anchorMin = new Vector2(0.04f, 0.24f);
            condRect.anchorMax = new Vector2(0.96f, 0.40f);
            condRect.offsetMin = Vector2.zero;
            condRect.offsetMax = Vector2.zero;
            navConditionText = condObj.AddComponent<TextMeshProUGUI>();
            if (font) navConditionText.font = font;
            navConditionText.text = "ĐIỀU KIỆN: ĐƯỜNG VEN BIỂN ĐÊM MƯA - TẦM NHÌN HẠN CHẾ DO SƯƠNG MÙ";
            navConditionText.fontSize = 14;
            navConditionText.color = ColorTextSecondary;

            GameObject noteObj = CreateUIObject("Father_Note", card.transform, layer);
            RectTransform noteRect = noteObj.AddComponent<RectTransform>();
            noteRect.anchorMin = new Vector2(0.04f, 0.04f);
            noteRect.anchorMax = new Vector2(0.96f, 0.24f);
            noteRect.offsetMin = Vector2.zero;
            noteRect.offsetMax = Vector2.zero;
            navFatherNoteText = noteObj.AddComponent<TextMeshProUGUI>();
            if (font) navFatherNoteText.font = font;
            navFatherNoteText.text = "\"Ghi chú của Bố: Đến bãi xe gặp anh Ba hỏi cái bo mạch điều khiển màn hình cũ. Đừng quên mang cho ổng gói thuốc lá.\"";
            navFatherNoteText.fontSize = 13;
            navFatherNoteText.fontStyle = FontStyles.Italic;
            navFatherNoteText.color = new Color(1f, 0.95f, 0.7f, 0.95f);

            // Backward compatibility
            waypointText = navDestTitleText;
            fatherNoteText = navFatherNoteText;
        }

        private void BuildRadioApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            radioAppPanel = CreateUIObject("App_Radio_Panel", parent.transform, layer);
            RectTransform rRect = radioAppPanel.AddComponent<RectTransform>();
            rRect.anchorMin = Vector2.zero;
            rRect.anchorMax = Vector2.one;
            rRect.offsetMin = Vector2.zero;
            rRect.offsetMax = Vector2.zero;

            GameObject card = CreateCard("Radio_Full_Card", radioAppPanel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("TRUNG TÂM PHÁT THANH & BĂNG CASSETTE (MEDIA / RADIO)", card.transform, layer, font);

            GameObject stObj = CreateUIObject("Station_Text", card.transform, layer);
            RectTransform stRect = stObj.AddComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0.04f, 0.54f);
            stRect.anchorMax = new Vector2(0.6f, 0.78f);
            stRect.offsetMin = Vector2.zero;
            stRect.offsetMax = Vector2.zero;
            radioStationText = stObj.AddComponent<TextMeshProUGUI>();
            if (font) radioStationText.font = font;
            radioStationText.text = radioStations[0];
            radioStationText.fontSize = 20;
            radioStationText.fontStyle = FontStyles.Bold;
            radioStationText.color = ColorAccentYellow;

            GameObject trObj = CreateUIObject("Track_Text", card.transform, layer);
            RectTransform trRect = trObj.AddComponent<RectTransform>();
            trRect.anchorMin = new Vector2(0.04f, 0.34f);
            trRect.anchorMax = new Vector2(0.6f, 0.54f);
            trRect.offsetMin = Vector2.zero;
            trRect.offsetMax = Vector2.zero;
            radioTrackText = trObj.AddComponent<TextMeshProUGUI>();
            if (font) radioTrackText.font = font;
            radioTrackText.text = radioTracks[0];
            radioTrackText.fontSize = 15;
            radioTrackText.color = ColorTextSecondary;

            // Audio Visualizer (12 bars)
            visualizerBars = new Image[12];
            for (int i = 0; i < 12; i++)
            {
                GameObject bar = CreateUIObject($"VisBar_{i}", card.transform, layer);
                RectTransform bRect = bar.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.63f + i * 0.028f, 0.34f);
                bRect.anchorMax = new Vector2(0.65f + i * 0.028f, 0.78f);
                bRect.offsetMin = Vector2.zero;
                bRect.offsetMax = Vector2.zero;
                Image barImg = bar.AddComponent<Image>();
                barImg.type = Image.Type.Filled;
                barImg.fillMethod = Image.FillMethod.Vertical;
                barImg.fillOrigin = 0;
                barImg.fillAmount = 0.5f;
                barImg.color = ColorOkGreen;
                visualizerBars[i] = barImg;
            }

            // Interactive Buttons Row at bottom
            CreateActionButton("Btn_Prev", card.transform, layer, font, new Vector2(0.04f, 0.06f), new Vector2(0.32f, 0.28f), "◄ KÊNH TRƯỚC (Q)", PrevStation);
            CreateActionButton("Btn_Next", card.transform, layer, font, new Vector2(0.35f, 0.06f), new Vector2(0.63f, 0.28f), "KÊNH KẾ TIẾP (E) ►", NextStation);
        }

        private void BuildDiagnosticsApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            diagAppPanel = CreateUIObject("App_Diag_Panel", parent.transform, layer);
            RectTransform dRect = diagAppPanel.AddComponent<RectTransform>();
            dRect.anchorMin = Vector2.zero;
            dRect.anchorMax = Vector2.one;
            dRect.offsetMin = Vector2.zero;
            dRect.offsetMax = Vector2.zero;

            GameObject card = CreateCard("Diag_Full_Card", diagAppPanel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("CHẨN ĐOÁN HỆ THỐNG ĐỘNG CƠ (OBD-II DIAGNOSTICS)", card.transform, layer, font);

            GameObject dtcObj = CreateUIObject("Dtc_Text", card.transform, layer);
            RectTransform dtcRect = dtcObj.AddComponent<RectTransform>();
            dtcRect.anchorMin = new Vector2(0.04f, 0.58f);
            dtcRect.anchorMax = new Vector2(0.96f, 0.78f);
            dtcRect.offsetMin = Vector2.zero;
            dtcRect.offsetMax = Vector2.zero;
            diagDtcText = dtcObj.AddComponent<TextMeshProUGUI>();
            if (font) diagDtcText.font = font;
            diagDtcText.text = dtcCodes[0];
            diagDtcText.fontSize = 19;
            diagDtcText.fontStyle = FontStyles.Bold;
            diagDtcText.color = ColorOkGreen;

            GameObject sevObj = CreateUIObject("Severity_Text", card.transform, layer);
            RectTransform sevRect = sevObj.AddComponent<RectTransform>();
            sevRect.anchorMin = new Vector2(0.04f, 0.40f);
            sevRect.anchorMax = new Vector2(0.96f, 0.58f);
            sevRect.offsetMin = Vector2.zero;
            sevRect.offsetMax = Vector2.zero;
            diagSeverityText = sevObj.AddComponent<TextMeshProUGUI>();
            if (font) diagSeverityText.font = font;
            diagSeverityText.text = "TRẠNG THÁI: TẤT CẢ HỆ THỐNG HOẠT ĐỘNG BÌNH THƯỜNG";
            diagSeverityText.fontSize = 15;
            diagSeverityText.color = ColorOkGreen;

            // Live Telemetry readouts
            GameObject tmObj = CreateUIObject("Temp_Text", card.transform, layer);
            RectTransform tmRect = tmObj.AddComponent<RectTransform>();
            tmRect.anchorMin = new Vector2(0.04f, 0.24f);
            tmRect.anchorMax = new Vector2(0.35f, 0.40f);
            tmRect.offsetMin = Vector2.zero;
            tmRect.offsetMax = Vector2.zero;
            diagTempText = tmObj.AddComponent<TextMeshProUGUI>();
            if (font) diagTempText.font = font;
            diagTempText.text = "NHIỆT ĐỘ LÀM MÁT: 88°C";
            diagTempText.fontSize = 14;
            diagTempText.color = ColorTextSecondary;

            GameObject batObj = CreateUIObject("Bat_Text", card.transform, layer);
            RectTransform batRect = batObj.AddComponent<RectTransform>();
            batRect.anchorMin = new Vector2(0.37f, 0.24f);
            batRect.anchorMax = new Vector2(0.68f, 0.40f);
            batRect.offsetMin = Vector2.zero;
            batRect.offsetMax = Vector2.zero;
            diagBatText = batObj.AddComponent<TextMeshProUGUI>();
            if (font) diagBatText.font = font;
            diagBatText.text = "ĐIỆN ÁP ẮC QUY: 12.6V";
            diagBatText.fontSize = 14;
            diagBatText.color = ColorTextSecondary;

            GameObject rpmObj = CreateUIObject("Rpm_Text", card.transform, layer);
            RectTransform rpmRect = rpmObj.AddComponent<RectTransform>();
            rpmRect.anchorMin = new Vector2(0.70f, 0.24f);
            rpmRect.anchorMax = new Vector2(0.96f, 0.40f);
            rpmRect.offsetMin = Vector2.zero;
            rpmRect.offsetMax = Vector2.zero;
            diagRpmText = rpmObj.AddComponent<TextMeshProUGUI>();
            if (font) diagRpmText.font = font;
            diagRpmText.text = "VÒNG TUA: 800 RPM";
            diagRpmText.fontSize = 14;
            diagRpmText.color = ColorTextSecondary;

            // Interactive Scan Button
            CreateActionButton("Btn_Scan", card.transform, layer, font, new Vector2(0.04f, 0.05f), new Vector2(0.45f, 0.22f), "🔍 QUÉT / CHUYỂN MÃ LỖI (R)", CycleDTC);
        }

        private void BuildSettingsApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            settingsAppPanel = CreateUIObject("App_Settings_Panel", parent.transform, layer);
            RectTransform sRect = settingsAppPanel.AddComponent<RectTransform>();
            sRect.anchorMin = Vector2.zero;
            sRect.anchorMax = Vector2.one;
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;

            GameObject card = CreateCard("Settings_Full_Card", settingsAppPanel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("CÀI ĐẶT XE & KHOANG LÁI (CABIN CONTROLS)", card.transform, layer, font);

            GameObject bmObj = CreateUIObject("BabyMode_Title", card.transform, layer);
            RectTransform bmRect = bmObj.AddComponent<RectTransform>();
            bmRect.anchorMin = new Vector2(0.04f, 0.58f);
            bmRect.anchorMax = new Vector2(0.96f, 0.78f);
            bmRect.offsetMin = Vector2.zero;
            bmRect.offsetMax = Vector2.zero;
            babyModeText = bmObj.AddComponent<TextMeshProUGUI>();
            if (font) babyModeText.font = font;
            babyModeText.text = "CABIN STABILIZER (CHẾ ĐỘ GIỮ ÊM GHẾ SAU)";
            babyModeText.fontSize = 20;
            babyModeText.fontStyle = FontStyles.Bold;
            babyModeText.color = ColorTextPrimary;

            GameObject badgeObj = CreateUIObject("BabyMode_Badge", card.transform, layer);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.04f, 0.40f);
            badgeRect.anchorMax = new Vector2(0.96f, 0.58f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            babyModeStatusBadge = badgeObj.AddComponent<TextMeshProUGUI>();
            if (font) babyModeStatusBadge.font = font;
            babyModeStatusBadge.text = "TRẠNG THÁI: [ ĐANG BẬT - BẢO VỆ GIẤC NGỦ ]";
            babyModeStatusBadge.fontSize = 16;
            babyModeStatusBadge.fontStyle = FontStyles.Bold;
            babyModeStatusBadge.color = ColorOkGreen;

            GameObject descObj = CreateUIObject("BabyMode_Desc", card.transform, layer);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.04f, 0.24f);
            descRect.anchorMax = new Vector2(0.96f, 0.40f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            babyModeDescText = descObj.AddComponent<TextMeshProUGUI>();
            if (font) babyModeDescText.font = font;
            babyModeDescText.text = "Tự động cân bằng độ nhún phuộc và làm mượt chân ga để giữ cho đứa bé ở hàng ghế sau không bị giật mình thức giấc trong đêm mưa.";
            babyModeDescText.fontSize = 13;
            babyModeDescText.color = ColorTextSecondary;

            // Interactive Toggle Button
            CreateActionButton("Btn_ToggleBabyMode", card.transform, layer, font, new Vector2(0.04f, 0.05f), new Vector2(0.48f, 0.22f), "BẬT / TẮT BABY MODE (B)", ToggleBabyMode);
        }

        private GameObject CreateCard(string name, Transform parent, int layer, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject card = CreateUIObject(name, parent, layer);
            RectTransform rt = card.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
            Image img = card.AddComponent<Image>();
            img.color = ColorCardBg;
            return card;
        }

        private void CreateAppHeaderWithBack(string title, Transform cardTransform, int layer, TMP_FontAsset font)
        {
            // Back to Launcher Button on the Left
            GameObject backBtnObj = CreateUIObject("Btn_Back_Launcher", cardTransform, layer);
            RectTransform bbRect = backBtnObj.AddComponent<RectTransform>();
            bbRect.anchorMin = new Vector2(0.02f, 0.80f);
            bbRect.anchorMax = new Vector2(0.24f, 0.98f);
            bbRect.offsetMin = Vector2.zero;
            bbRect.offsetMax = Vector2.zero;

            Image bbImg = backBtnObj.AddComponent<Image>();
            bbImg.color = new Color(0.14f, 0.22f, 0.34f, 0.95f);

            Button bbBtn = backBtnObj.AddComponent<Button>();
            bbBtn.targetGraphic = bbImg;
            bbBtn.onClick.AddListener(() => SelectApp(AAOSApp.Launcher));

            GameObject bbTxtObj = CreateUIObject("Text", backBtnObj.transform, layer);
            RectTransform bbtRect = bbTxtObj.AddComponent<RectTransform>();
            bbtRect.anchorMin = Vector2.zero;
            bbtRect.anchorMax = Vector2.one;
            bbtRect.offsetMin = Vector2.zero;
            bbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI bbTxt = bbTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) bbTxt.font = font;
            bbTxt.text = "◀ LAUNCHER (1)";
            bbTxt.fontSize = 13;
            bbTxt.fontStyle = FontStyles.Bold;
            bbTxt.alignment = TextAlignmentOptions.Center;
            bbTxt.color = ColorActiveCyan;

            // App Title on the Right of Back Button
            GameObject hObj = CreateUIObject("Header", cardTransform, layer);
            RectTransform hRect = hObj.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.26f, 0.80f);
            hRect.anchorMax = new Vector2(0.98f, 0.98f);
            hRect.offsetMin = Vector2.zero;
            hRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hTxt = hObj.AddComponent<TextMeshProUGUI>();
            if (font) hTxt.font = font;
            hTxt.text = title;
            hTxt.fontSize = 14;
            hTxt.fontStyle = FontStyles.Bold;
            hTxt.color = new Color(0.4f, 0.6f, 0.8f, 0.9f);
        }

        private GameObject CreateActionButton(string name, Transform parent, int layer, TMP_FontAsset font, Vector2 anchorMin, Vector2 anchorMax, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = CreateUIObject(name, parent, layer);
            RectTransform bRect = btnObj.AddComponent<RectTransform>();
            bRect.anchorMin = anchorMin;
            bRect.anchorMax = anchorMax;
            bRect.offsetMin = Vector2.zero;
            bRect.offsetMax = Vector2.zero;

            Image bgImg = btnObj.AddComponent<Image>();
            bgImg.color = new Color(0.14f, 0.22f, 0.32f, 0.9f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(action);

            GameObject txtObj = CreateUIObject("Text", btnObj.transform, layer);
            RectTransform tRect = txtObj.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
            TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
            if (font) txt.font = font;
            txt.text = label;
            txt.fontSize = 13;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = ColorTextPrimary;

            return btnObj;
        }

        private GameObject CreateUIObject(string name, Transform parent, int layer)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.layer = layer;
            return obj;
        }
        #endregion
    }
}
