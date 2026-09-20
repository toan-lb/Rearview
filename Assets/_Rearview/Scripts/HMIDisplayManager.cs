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
    /// 2. Apple CarPlay / AAOS Center Infotainment:
    ///    - Left Rail Dock: Clock, 5G signal, battery, recent app shortcuts, and Home Grid button [ ⊞ ].
    ///    - App Launcher Grid: 8 vibrant squircle app cards (Phone, Music, Maps, Cassette, Diag, Notes, Weather, Settings).
    ///    - Focus / Selection Ring: Navigate between apps using Arrow keys [←][→][↑][↓] or number keys [1..8], and press [Enter]/[Space] to launch!
    ///    - Fullscreen App Screens with [ ◀ LAUNCHER ] return buttons.
    /// 3. HVAC Screen Off: Ensures the lower console HVAC screen remains dark and decoupled from the main display.
    /// </summary>
    [AddComponentMenu("Rearview/HMI Display Manager")]
    [DefaultExecutionOrder(-5)]
    public class HMIDisplayManager : MonoBehaviour
    {
        public enum AAOSApp
        {
            Launcher = 0,    // CarPlay / AAOS App Grid (8 Squircles)
            Phone = 1,       // Emergency Comms & Radio Calls
            Music = 2,       // FM Radio & Music Player
            Maps = 3,        // Satellite Navigation & Route
            Cassette = 4,    // Father's 1998 Cassette Tapes
            Diagnostics = 5, // OBD-II Diagnostics & DTC Fault Codes
            Notes = 6,       // Father's Handwritten DIY Notes
            Weather = 7,     // Coastal Weather & Road Conditions
            Settings = 8     // Cabin Controls & Baby Mode Stabilizer
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

        [Header("--- Vibrant Cluster ADAS Elements ---")]
        public RectTransform steeringWheelHudRoot;
        public Image clusterFrame;
        public TextMeshProUGUI clusterClockText;
        public TextMeshProUGUI clusterTempText;
        public Image leftTurnIcon;
        public Image rightTurnIcon;
        public RectTransform adasRoadContainer;
        public RectTransform[] roadLaneDashes;
        public RectTransform carAvatarRect;
        public Image carAvatarTaillights;
        public RectTransform trafficCarAhead;
        public RectTransform trafficTruckAhead;
        public TextMeshProUGUI clusterNavBannerText;
        public TextMeshProUGUI clusterNavSubText;
        public Image adasSteeringBadge;
        public Image batteryFillBar;
        public Image tempFillBar;
        public TextMeshProUGUI batteryPercentText;
        public TextMeshProUGUI gearSubLabelText;

        // ==========================================
        // CarPlay / AAOS Center Infotainment Elements
        // ==========================================
        [Header("--- CarPlay / AAOS Left Rail Dock ---")]
        public TextMeshProUGUI railClockText;
        public TextMeshProUGUI railSignalText;
        public TextMeshProUGUI railBatteryText;
        public Button railHomeBtn;

        [Header("--- AAOS App State & Panels ---")]
        public AAOSApp currentApp = AAOSApp.Launcher;
        public int selectedGridIndex = 0; // 0 to 7 for Arrow key navigation
        public GameObject launcherGridPanel;
        public GameObject[] appScreens; // Indexed 1 to 8 matching AAOSApp

        [Header("--- Launcher Grid Cards & Focus Outlines ---")]
        public Image[] gridCardOutlines; // Outlines that highlight when focused
        public Button[] gridCardButtons;

        // Individual App Screens UI
        [Header("--- Maps App Elements ---")]
        public TextMeshProUGUI navDestTitleText;
        public TextMeshProUGUI navDestDistText;
        public TextMeshProUGUI navConditionText;
        public TextMeshProUGUI navFatherNoteText;

        [Header("--- Music App Elements ---")]
        public TextMeshProUGUI radioStationText;
        public TextMeshProUGUI radioTrackText;
        public Image[] visualizerBars;

        [Header("--- Cassette App Elements ---")]
        public TextMeshProUGUI cassetteTitleText;
        public TextMeshProUGUI cassetteTrackText;
        public TextMeshProUGUI cassetteQuoteText;

        [Header("--- Diagnostics App Elements ---")]
        public TextMeshProUGUI diagDtcText;
        public TextMeshProUGUI diagSeverityText;
        public TextMeshProUGUI diagTempText;
        public TextMeshProUGUI diagBatText;
        public TextMeshProUGUI diagRpmText;

        [Header("--- Settings App Elements ---")]
        public TextMeshProUGUI babyModeText;
        public TextMeshProUGUI babyModeStatusBadge;
        public TextMeshProUGUI babyModeDescText;

        [Header("--- Notes App Elements ---")]
        public TextMeshProUGUI notesTitleText;
        public TextMeshProUGUI notesContentText;

        [Header("--- Weather App Elements ---")]
        public TextMeshProUGUI weatherMainText;
        public TextMeshProUGUI weatherDetailText;

        // Legacy compatibility properties
        [HideInInspector] public TextMeshProUGUI waypointText;
        [HideInInspector] public TextMeshProUGUI fatherNoteText;
        [HideInInspector] public TextMeshProUGUI clockText;
        [HideInInspector] public TextMeshProUGUI weatherText;

        [Header("--- Radio Stations & Tapes ---")]
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

        private float simulatedTemp = 40f;
        private float simulatedBattery = 12.6f;
        private float simulatedBatteryPercent = 86f;
        private float roadDashTimer = 0f;
        private float carAvatarBaseY = -35f;
        private float trafficCarBaseX = -45f;
        private float trafficCarBaseY = 32f;
        private float trafficTruckBaseX = 48f;
        private float trafficTruckBaseY = 18f;
        private float visualizerTimer = 0f;

        // Styling Colors
        private static readonly Color ColorActiveCyan = new Color(0f, 0.9f, 1f, 1f);        // #00E5FF
        private static readonly Color ColorFocusGlow = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color ColorCardBorderInactive = new Color(0.15f, 0.22f, 0.32f, 0.4f);
        private static readonly Color ColorTextPrimary = new Color(0.94f, 0.96f, 1f, 1f);
        private static readonly Color ColorTextSecondary = new Color(0.6f, 0.7f, 0.82f, 1f);
        private static readonly Color ColorAccentYellow = new Color(1f, 0.84f, 0.25f, 1f);
        private static readonly Color ColorAccentAmber = new Color(1f, 0.58f, 0f, 1f);       // #FF9500 Vibrant Cockpit Amber
        private static readonly Color ColorAmberGlow = new Color(1f, 0.42f, 0f, 0.85f);     // #FF6B00
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
            EnsureScreenMaterialAssigned();
            SyncMaterialProperties();
            SelectApp(currentApp);
            CleanScreenColliders();
            EnsureHVACScreenOff();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                if (vehicle == null) FindVehicle();
                EnsureScreenMaterialAssigned();
                SyncMaterialProperties();
                if (hmiCamera != null && targetRenderTexture != null)
                {
                    hmiCamera.Render();
                }
            }
        }
#endif

        public void EnsureScreenMaterialAssigned()
        {
            if (vehicle == null) FindVehicle();
            if (screenMaterial == null) InitDefaults();

            if (vehicle != null && screenMaterial != null)
            {
                MeshRenderer[] renderers = vehicle.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in renderers)
                {
                    if (mr.gameObject.name.Equals("Curve_Screen", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Material[] mats = mr.sharedMaterials;
                        bool assigned = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] != null && mats[i].name.Contains("plasticGlossy.001"))
                            {
                                mats[i] = screenMaterial;
                                assigned = true;
                            }
                        }
                        if (!assigned && mats.Length > 0)
                        {
                            mats[0] = screenMaterial;
                        }
                        mr.sharedMaterials = mats;
                        break;
                    }
                }
            }
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
                screenMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        private void CleanScreenColliders()
        {
            // Per RCC vehicle pipeline standards (rcc-vehicle-control-pipeline/SKILL.md):
            // Curve_Screen is strictly a visual display mesh and MUST NOT have any colliders attached.
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

        #region Input & CarPlay App Navigation
        private void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                // When on Launcher Grid: Arrow keys navigate between squircle cards
                if (currentApp == AAOSApp.Launcher)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame)
                    {
                        selectedGridIndex = (selectedGridIndex - 1 + 8) % 8;
                        UpdateGridSelectionVisuals();
                    }
                    else if (kb.rightArrowKey.wasPressedThisFrame)
                    {
                        selectedGridIndex = (selectedGridIndex + 1) % 8;
                        UpdateGridSelectionVisuals();
                    }
                    else if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                    {
                        selectedGridIndex = (selectedGridIndex + 4) % 8; // Switch between Row 1 and Row 2
                        UpdateGridSelectionVisuals();
                    }
                    else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                    {
                        LaunchAppByIndex(selectedGridIndex);
                    }
                }
                else
                {
                    // Inside an App: ESC or Backspace returns to Launcher
                    if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
                    {
                        SelectApp(AAOSApp.Launcher);
                    }
                }

                // Direct Hotkeys [1] to [8]
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SelectApp(AAOSApp.Phone);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SelectApp(AAOSApp.Music);
                else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SelectApp(AAOSApp.Maps);
                else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) SelectApp(AAOSApp.Cassette);
                else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) SelectApp(AAOSApp.Diagnostics);
                else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) SelectApp(AAOSApp.Notes);
                else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) SelectApp(AAOSApp.Weather);
                else if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) SelectApp(AAOSApp.Settings);
                else if (kb.tabKey.wasPressedThisFrame) CycleApp();

                // In-App Sub-controls
                if (kb.qKey.wasPressedThisFrame) PrevStation();
                if (kb.eKey.wasPressedThisFrame) NextStation();
                if (kb.rKey.wasPressedThisFrame) CycleDTC();
                if (kb.bKey.wasPressedThisFrame) ToggleBabyMode();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (currentApp == AAOSApp.Launcher)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    selectedGridIndex = (selectedGridIndex - 1 + 8) % 8;
                    UpdateGridSelectionVisuals();
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    selectedGridIndex = (selectedGridIndex + 1) % 8;
                    UpdateGridSelectionVisuals();
                }
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    selectedGridIndex = (selectedGridIndex + 4) % 8;
                    UpdateGridSelectionVisuals();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    LaunchAppByIndex(selectedGridIndex);
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                {
                    SelectApp(AAOSApp.Launcher);
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectApp(AAOSApp.Phone);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectApp(AAOSApp.Music);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectApp(AAOSApp.Maps);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectApp(AAOSApp.Cassette);
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SelectApp(AAOSApp.Diagnostics);
            else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) SelectApp(AAOSApp.Notes);
            else if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) SelectApp(AAOSApp.Weather);
            else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) SelectApp(AAOSApp.Settings);
            else if (Input.GetKeyDown(KeyCode.Tab)) CycleApp();

            if (Input.GetKeyDown(KeyCode.Q)) PrevStation();
            if (Input.GetKeyDown(KeyCode.E)) NextStation();
            if (Input.GetKeyDown(KeyCode.R)) CycleDTC();
            if (Input.GetKeyDown(KeyCode.B)) ToggleBabyMode();
#endif
        }

        public void LaunchAppByIndex(int gridIdx)
        {
            // Maps 0..7 grid index to AAOSApp enum (1..8)
            AAOSApp target = (AAOSApp)(gridIdx + 1);
            SelectApp(target);
        }

        public void SelectApp(AAOSApp app)
        {
            currentApp = app;

            if (launcherGridPanel) launcherGridPanel.SetActive(app == AAOSApp.Launcher);

            if (appScreens != null)
            {
                for (int i = 0; i < appScreens.Length; i++)
                {
                    if (appScreens[i] != null)
                    {
                        // appScreens[0] corresponds to AAOSApp.Phone (1), etc.
                        appScreens[i].SetActive(app != AAOSApp.Launcher && (int)app == (i + 1));
                    }
                }
            }

            if (app == AAOSApp.Launcher)
            {
                UpdateGridSelectionVisuals();
            }
        }

        public void SelectApp(int appIndex)
        {
            SelectApp((AAOSApp)Mathf.Clamp(appIndex, 0, 8));
        }

        public void CycleApp()
        {
            int next = ((int)currentApp + 1) % 9;
            SelectApp((AAOSApp)next);
        }

        private void UpdateGridSelectionVisuals()
        {
            if (gridCardOutlines == null) return;
            for (int i = 0; i < gridCardOutlines.Length; i++)
            {
                if (gridCardOutlines[i] != null)
                {
                    bool isSelected = (i == selectedGridIndex);
                    gridCardOutlines[i].color = isSelected ? ColorActiveCyan : ColorCardBorderInactive;
                    // Slightly scale or highlight selected card
                    gridCardOutlines[i].transform.localScale = isSelected ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
                }
            }
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
            float steer = 0f;
            float brake = 0f;
            int indicators = 0;

            if (vehicle != null)
            {
                speed = Mathf.Abs(vehicle.speed);
                rpm = vehicle.engineRPM;
                headlights = vehicle.lowBeamHeadLightsOn || vehicle.highBeamHeadLightsOn;
                handbrake = vehicle.handbrakeInput > 0.5f;
                steer = vehicle.steerInput;
                brake = vehicle.brakeInput;
                indicators = (int)vehicle.indicatorsOn;

                if (vehicle.NGear)
                    gear = "N";
                else if (vehicle.direction == -1)
                    gear = "R";
                else
                    gear = "D" + (vehicle.currentGear + 1).ToString();

                simulatedTemp = Mathf.MoveTowards(simulatedTemp, 40f + (speed / 160f) * 45f, Time.deltaTime * 0.5f);
                simulatedBattery = 12.4f + (rpm / 7000f) * 1.8f;
                simulatedBatteryPercent = Mathf.Clamp(86f - (Time.timeSinceLevelLoad * 0.015f), 15f, 98f);
            }

            // 1. Primary Speed & Gear
            if (speedText != null)
                speedText.text = speed.ToString("0");

            if (gearText != null)
                gearText.text = gear;

            if (kmhText != null)
                kmhText.text = "KM/H";

            if (gearSubLabelText != null)
                gearSubLabelText.text = "3.4 KWH / 100KM";

            if (rpmText != null)
                rpmText.text = $"{rpm:0} RPM";

            if (rpmBar != null)
            {
                float fill = Mathf.Clamp01(rpm / 7000f);
                rpmBar.fillAmount = fill;
                rpmBar.color = Color.Lerp(ColorActiveCyan, Color.red, Mathf.InverseLerp(0.7f, 1f, fill));
            }

            // 2. Battery & Temperature Gauges
            if (batteryPercentText != null)
                batteryPercentText.text = $"BAT {simulatedBatteryPercent:0}%";

            if (batteryFillBar != null)
                batteryFillBar.fillAmount = simulatedBatteryPercent / 100f;

            if (batteryText != null)
                batteryText.text = $"BAT: {simulatedBattery:0.0}V";

            if (tempText != null)
            {
                tempText.text = $"{simulatedTemp:0}°C TEMP";
                tempText.color = simulatedTemp > 100f ? Color.red : ColorAccentAmber;
            }

            if (tempFillBar != null)
                tempFillBar.fillAmount = Mathf.Clamp01(simulatedTemp / 120f);

            // 3. Cluster Header (Clock, Turn Signals, Outside Temp)
            if (clusterClockText != null)
                clusterClockText.text = System.DateTime.Now.ToString("hh:mm tt");

            if (clusterTempText != null)
                clusterTempText.text = "27°C";

            bool blink = Mathf.PingPong(Time.time * 3.5f, 1f) > 0.5f;
            if (leftTurnIcon != null)
            {
                bool leftOn = (indicators == 2 || indicators == 3);
                leftTurnIcon.color = (leftOn && blink) ? ColorOkGreen : new Color(0.2f, 0.3f, 0.4f, 0.35f);
            }
            if (rightTurnIcon != null)
            {
                bool rightOn = (indicators == 1 || indicators == 3);
                rightTurnIcon.color = (rightOn && blink) ? ColorOkGreen : new Color(0.2f, 0.3f, 0.4f, 0.35f);
            }

            // 4. ADAS Road Dashes Flow Animation (Synchronized with Vehicle Speed & Direction)
            float speedKmh = (vehicle != null) ? Mathf.Abs(vehicle.speed) : 0f;
            if (speedKmh > 0.8f)
            {
                float directionSign = (vehicle != null && vehicle.direction == -1) ? -1f : 1f;
                float speedFactor = (speedKmh / 50f) * directionSign;
                roadDashTimer += speedFactor * Time.deltaTime * 2.2f;
                roadDashTimer = (roadDashTimer % 1.0f + 1.0f) % 1.0f;
                UpdateLaneDashesVisuals(roadDashTimer);
            }

            // 5. Mini Player Car Avatar Dynamics (Sway & Roll)
            if (carAvatarRect != null)
            {
                float targetSway = steer * 14f;
                float targetRoll = -steer * 6f;
                carAvatarRect.anchoredPosition = new Vector2(targetSway, carAvatarBaseY);
                carAvatarRect.localEulerAngles = new Vector3(0, 0, targetRoll);

                if (carAvatarTaillights != null)
                {
                    if (brake > 0.1f)
                        carAvatarTaillights.color = new Color(1f, 0.05f, 0.05f, 1f);
                    else
                        carAvatarTaillights.color = new Color(0.9f, 0.18f, 0.18f, 0.85f);
                }
            }

            // 6. Traffic Vehicles Subtle Drift
            if (trafficCarAhead != null)
                trafficCarAhead.anchoredPosition = new Vector2(trafficCarBaseX + Mathf.Sin(Time.time * 0.7f) * 3f, trafficCarBaseY);
            if (trafficTruckAhead != null)
                trafficTruckAhead.anchoredPosition = new Vector2(trafficTruckBaseX + Mathf.Sin(Time.time * 0.5f) * 2f, trafficTruckBaseY);

            // 7. ADAS Steering Badge (Baby Mode / Cabin Stabilizer)
            if (adasSteeringBadge != null)
                adasSteeringBadge.color = babyModeActive ? ColorOkGreen : ColorAccentAmber;

            // 8. Secondary Gauges (DTC, Lights, Handbrake)
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
            // Update Left Rail Dock Clock
            if (railClockText != null)
                railClockText.text = System.DateTime.Now.ToString("hh:mm");

            // Audio Visualizer (Animates during Music or Cassette app)
            visualizerTimer += Time.deltaTime * 8f;
            if (visualizerBars != null && (currentApp == AAOSApp.Music || currentApp == AAOSApp.Cassette))
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

            // Dynamic App Updates
            switch (currentApp)
            {
                case AAOSApp.Maps:
                    if (navDestTitleText != null) navDestTitleText.text = "WAYPOINT: BÃI PHẾ LIỆU BÁC BA (CHƯƠNG 2)";
                    if (navDestDistText != null) navDestDistText.text = "KHOẢNG CÁCH: 1.8 KM | THỜI GIAN: ~4 PHÚT | HƯỚNG BẮC";
                    if (navConditionText != null) navConditionText.text = "ĐIỀU KIỆN: ĐƯỜNG VEN BIỂN ĐÊM MƯA - TẦM NHÌN HẠN CHẾ DO SƯƠNG MÙ";
                    if (navFatherNoteText != null) navFatherNoteText.text = "\"Ghi chú của Bố: Đến bãi xe gặp anh Ba hỏi cái bo mạch điều khiển màn hình cũ. Đừng quên mang cho ổng gói thuốc lá.\"";
                    break;

                case AAOSApp.Music:
                    if (radioStationText != null) radioStationText.text = radioStations[currentStationIndex];
                    if (radioTrackText != null) radioTrackText.text = radioTracks[currentStationIndex];
                    break;

                case AAOSApp.Cassette:
                    if (cassetteTitleText != null) cassetteTitleText.text = "BĂNG CASSETTE: LỜI NHẮN CỦA BỐ (1998)";
                    if (cassetteTrackText != null) cassetteTrackText.text = "Track 01: 'Gửi con trai...'";
                    if (cassetteQuoteText != null) cassetteQuoteText.text = "\"Chào con. Xe này bố tự đóng, chạy tốt đấy. Nhớ kiểm tra dầu máy mỗi 5000 cây...\"";
                    break;

                case AAOSApp.Diagnostics:
                    if (diagDtcText != null)
                    {
                        diagDtcText.text = dtcCodes[currentDtcIndex];
                        diagDtcText.color = currentDtcIndex == 0 ? ColorOkGreen : ColorWarnOrange;
                    }
                    if (diagSeverityText != null)
                    {
                        diagSeverityText.text = currentDtcIndex == 0 ? "TRẠNG THÁI: TẤT CẢ HỆ THỐNG HOẠT ĐỘNG BÌNH THƯỜNG" : "MỨC ĐỘ: CẢNH BÁO - CẦN THAY THẾ LINH KIỆN TRƯỚC KHI VƯỢT ĐÈO";
                        diagSeverityText.color = currentDtcIndex == 0 ? ColorOkGreen : ColorWarnOrange;
                    }
                    if (diagTempText != null) diagTempText.text = $"NHIỆT ĐỘ LÀM MÁT: {simulatedTemp:0}°C";
                    if (diagBatText != null) diagBatText.text = $"ĐIỆN ÁP ẮC QUY: {simulatedBattery:0.0}V (BÌNH THƯỜNG)";
                    if (diagRpmText != null) diagRpmText.text = $"VÒNG TUA MÁY: {(vehicle != null ? vehicle.engineRPM : 800f):0} RPM";
                    break;

                case AAOSApp.Settings:
                    if (babyModeText != null) babyModeText.text = "CABIN STABILIZER (CHẾ ĐỘ GIỮ ÊM GHẾ SAU)";
                    if (babyModeStatusBadge != null)
                    {
                        babyModeStatusBadge.text = babyModeActive ? "TRẠNG THÁI: [ ĐANG BẬT - BẢO VỆ GIẤC NGỦ ]" : "TRẠNG THÁI: [ ĐANG TẮT ]";
                        babyModeStatusBadge.color = babyModeActive ? ColorOkGreen : ColorWarnOrange;
                    }
                    if (babyModeDescText != null) babyModeDescText.text = "Tự động cân bằng độ nhún phuộc và làm mượt chân ga để giữ cho đứa bé ở hàng ghế sau không bị giật mình thức giấc trong đêm mưa.";
                    break;

                case AAOSApp.Weather:
                    if (weatherMainText != null) weatherMainText.text = "MƯA ĐÊM VEN BIỂN (COASTAL RAIN)";
                    if (weatherDetailText != null) weatherDetailText.text = "NHIỆT ĐỘ: 16°C | ĐỘ ẨM: 92% | GIÓ BIỂN: 24 KM/H\nCẢNH BÁO: ĐƯỜNG TRƠN TRỢT TRÊN CUNG ĐƯỜNG ĐÈO TIẾP THEO";
                    break;

                case AAOSApp.Notes:
                    if (notesTitleText != null) notesTitleText.text = "SỔ TAY CŨ CỦA BỐ (DIY REPAIR LOG)";
                    if (notesContentText != null) notesContentText.text = "- 12/03/1998: Mua khung xe cũ từ bãi phế liệu anh Ba.\n- 05/06/1998: Hàn lại giàn gầm, thay bugi và cảm biến nhiệt độ nước.\n- 18/09/1998: Gắn thử màn hình LCD tự chế. Hy vọng con trai sẽ thích.";
                    break;
            }
        }
        #endregion

        #region Procedural UI Hierarchy Builder
        /// <summary>
        /// Automatically constructs the full CarPlay / AAOS layout with Left Rail Dock, 8 Squircle App Grid, and Screens.
        /// </summary>
        public void EnsureUIExists(bool forceRebuild = false)
        {
            if (!forceRebuild && hmiCanvas != null && hmiCamera != null && launcherGridPanel != null && steeringWheelHudRoot != null)
                return;

            int targetLayer = LayerMask.NameToLayer(hmiLayerName);
            if (targetLayer < 0) targetLayer = LayerMask.NameToLayer("UI");
            if (targetLayer < 0) targetLayer = 0;

            int uiLayer = LayerMask.NameToLayer("UI");
            int cullingMask = 1 << targetLayer;
            if (uiLayer >= 0) cullingMask |= (1 << uiLayer);

            // 1. Create or Find HMI Root & Camera
            GameObject hmiRoot = GameObject.Find("HMI_System");
            if (hmiRoot == null)
            {
                hmiRoot = new GameObject("HMI_System");
                hmiRoot.transform.position = new Vector3(0, -500f, 0);
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
                hmiCamera.cullingMask = cullingMask;
                hmiCamera.targetTexture = targetRenderTexture;
            }
            else
            {
                hmiCamera.cullingMask = cullingMask;
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

        public static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                if (child != null)
                    SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void BuildCanvasLayout(GameObject canvasObj, int layer)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }

            // Background Panel
            GameObject bg = CreateUIObject("Background", canvasObj.transform, layer);
            RectTransform bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.05f, 0.08f, 1f);

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
            // RIGHT SECTION: CARPLAY / AAOS INFOTAINMENT (0.495 to 1.0)
            // ==========================================
            BuildCarPlayInfotainment(canvasObj, layer, font);

            // Ensure every UI element in the Canvas is properly assigned to the targetLayer
            SetLayerRecursively(canvasObj, layer);

            Debug.Log("<color=cyan><b>[HMIDisplayManager]</b></color> Đã thiết lập hoàn chỉnh hệ thống CarPlay / AAOS Mockup (Left Rail + 8 Squircle App Grid)!");
        }

        private void BuildDriverCluster(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject clusterPanel = CreateUIObject("Driver_Cluster_Panel", parent.transform, layer);
            RectTransform cRect = clusterPanel.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 0f);
            cRect.anchorMax = new Vector2(0.48f, 1f);
            cRect.offsetMin = new Vector2(10f, 10f);
            cRect.offsetMax = new Vector2(-10f, -10f);

            // =========================================================================
            // 1. VIBRANT CLUSTER HUD (Mathematically Framed Inside Steering Wheel Opening)
            //    Sightline from Cockpit Camera: X: 0.35 to 0.96, Y: 0.05 to 0.95
            // =========================================================================
            GameObject hudRootObj = CreateUIObject("Steering_Wheel_Cluster_HUD", clusterPanel.transform, layer);
            steeringWheelHudRoot = hudRootObj.AddComponent<RectTransform>();
            steeringWheelHudRoot.anchorMin = new Vector2(0.35f, 0.05f);
            steeringWheelHudRoot.anchorMax = new Vector2(0.96f, 0.95f);
            steeringWheelHudRoot.offsetMin = Vector2.zero;
            steeringWheelHudRoot.offsetMax = Vector2.zero;

            // HUD Dark Glass Backplate & Sleek Cyber Bezel Frame
            GameObject hudBg = CreateUIObject("HUD_Backplate", hudRootObj.transform, layer);
            RectTransform hudBgRect = hudBg.AddComponent<RectTransform>();
            hudBgRect.anchorMin = Vector2.zero;
            hudBgRect.anchorMax = Vector2.one;
            hudBgRect.offsetMin = Vector2.zero;
            hudBgRect.offsetMax = Vector2.zero;
            Image hudBgImg = hudBg.AddComponent<Image>();
            hudBgImg.color = new Color(0.03f, 0.04f, 0.07f, 0.96f);

            // Sleek High-Res HUD Bezel Frame Overlay (Replaces crude rectangular bars)
            Sprite bezelSprite = LoadSpriteAsset("Assets/_Rearview/Textures/HMI/HUD_Bezel_Frame.png");
            if (bezelSprite != null)
            {
                GameObject hudFrameObj = CreateUIObject("HUD_Frame_Overlay", hudRootObj.transform, layer);
                RectTransform hfRect = hudFrameObj.AddComponent<RectTransform>();
                hfRect.anchorMin = Vector2.zero;
                hfRect.anchorMax = Vector2.one;
                hfRect.offsetMin = Vector2.zero;
                hfRect.offsetMax = Vector2.zero;
                clusterFrame = hudFrameObj.AddComponent<Image>();
                clusterFrame.sprite = bezelSprite;
                clusterFrame.color = Color.white;
            }

            // =========================================================================
            // 2. LEFT WING: SPEEDOMETER & BATTERY GAUGE (X: 0.03 to 0.28)
            // =========================================================================
            GameObject leftWing = CreateUIObject("Left_Wing_Speed", hudRootObj.transform, layer);
            RectTransform lwRect = leftWing.AddComponent<RectTransform>();
            lwRect.anchorMin = new Vector2(0.03f, 0.05f);
            lwRect.anchorMax = new Vector2(0.28f, 0.95f);
            lwRect.offsetMin = Vector2.zero;
            lwRect.offsetMax = Vector2.zero;

            GameObject speedObj = CreateUIObject("Speed_Number", leftWing.transform, layer);
            RectTransform speedRect = speedObj.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(0f, 0.44f);
            speedRect.anchorMax = new Vector2(1f, 0.90f);
            speedRect.offsetMin = Vector2.zero;
            speedRect.offsetMax = Vector2.zero;
            speedText = speedObj.AddComponent<TextMeshProUGUI>();
            if (font) speedText.font = font;
            speedText.text = "127";
            speedText.fontSize = 64;
            speedText.fontStyle = FontStyles.Bold;
            speedText.alignment = TextAlignmentOptions.Center;
            speedText.color = ColorTextPrimary;

            GameObject kmhObj = CreateUIObject("KMH_Label", leftWing.transform, layer);
            RectTransform kmhRect = kmhObj.AddComponent<RectTransform>();
            kmhRect.anchorMin = new Vector2(0f, 0.32f);
            kmhRect.anchorMax = new Vector2(1f, 0.44f);
            kmhRect.offsetMin = Vector2.zero;
            kmhRect.offsetMax = Vector2.zero;
            kmhText = kmhObj.AddComponent<TextMeshProUGUI>();
            if (font) kmhText.font = font;
            kmhText.text = "KM/H";
            kmhText.fontSize = 14;
            kmhText.fontStyle = FontStyles.Bold;
            kmhText.alignment = TextAlignmentOptions.Center;
            kmhText.color = ColorTextSecondary;

            // Battery Level Bar
            GameObject batContainer = CreateUIObject("Battery_Container", leftWing.transform, layer);
            RectTransform bcRect = batContainer.AddComponent<RectTransform>();
            bcRect.anchorMin = new Vector2(0.06f, 0.10f);
            bcRect.anchorMax = new Vector2(0.94f, 0.28f);
            bcRect.offsetMin = Vector2.zero;
            bcRect.offsetMax = Vector2.zero;

            GameObject batTxtObj = CreateUIObject("Battery_Text", batContainer.transform, layer);
            RectTransform btRect = batTxtObj.AddComponent<RectTransform>();
            btRect.anchorMin = new Vector2(0f, 0.50f);
            btRect.anchorMax = new Vector2(1f, 1f);
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            batteryPercentText = batTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) batteryPercentText.font = font;
            batteryPercentText.text = "BAT 86%";
            batteryPercentText.fontSize = 12;
            batteryPercentText.fontStyle = FontStyles.Bold;
            batteryPercentText.alignment = TextAlignmentOptions.Left;
            batteryPercentText.color = ColorActiveCyan;

            GameObject batBarBg = CreateUIObject("Battery_Bar_BG", batContainer.transform, layer);
            RectTransform bbbRect = batBarBg.AddComponent<RectTransform>();
            bbbRect.anchorMin = new Vector2(0f, 0.05f);
            bbbRect.anchorMax = new Vector2(1f, 0.40f);
            bbbRect.offsetMin = Vector2.zero;
            bbbRect.offsetMax = Vector2.zero;
            Image bbbImg = batBarBg.AddComponent<Image>();
            bbbImg.color = new Color(0.12f, 0.18f, 0.25f, 0.9f);

            GameObject batFillObj = CreateUIObject("Battery_Bar_Fill", batBarBg.transform, layer);
            RectTransform bfRect = batFillObj.AddComponent<RectTransform>();
            bfRect.anchorMin = Vector2.zero;
            bfRect.anchorMax = Vector2.one;
            bfRect.offsetMin = Vector2.zero;
            bfRect.offsetMax = Vector2.zero;
            batteryFillBar = batFillObj.AddComponent<Image>();
            batteryFillBar.type = Image.Type.Filled;
            batteryFillBar.fillMethod = Image.FillMethod.Horizontal;
            batteryFillBar.fillOrigin = 0;
            batteryFillBar.fillAmount = 0.86f;
            batteryFillBar.color = ColorActiveCyan;

            // =========================================================================
            // 3. CENTER ADAS ROAD & PERCEPTION (X: 0.28 to 0.72)
            // =========================================================================
            GameObject adasObj = CreateUIObject("Center_ADAS_Container", hudRootObj.transform, layer);
            adasRoadContainer = adasObj.AddComponent<RectTransform>();
            adasRoadContainer.anchorMin = new Vector2(0.28f, 0.02f);
            adasRoadContainer.anchorMax = new Vector2(0.72f, 0.98f);
            adasRoadContainer.offsetMin = Vector2.zero;
            adasRoadContainer.offsetMax = Vector2.zero;

            // 3.1. Top Status Bar (Clock, Turn Signals, Speed Limit, Outside Temp)
            GameObject topBar = CreateUIObject("ADAS_Top_Bar", adasObj.transform, layer);
            RectTransform tbRect = topBar.AddComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0f, 0.88f);
            tbRect.anchorMax = new Vector2(1f, 1f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;

            GameObject clkObj = CreateUIObject("Cluster_Clock", topBar.transform, layer);
            RectTransform clkRect = clkObj.AddComponent<RectTransform>();
            clkRect.anchorMin = new Vector2(0.02f, 0f);
            clkRect.anchorMax = new Vector2(0.25f, 1f);
            clkRect.offsetMin = Vector2.zero;
            clkRect.offsetMax = Vector2.zero;
            clusterClockText = clkObj.AddComponent<TextMeshProUGUI>();
            if (font) clusterClockText.font = font;
            clusterClockText.text = "08:29 AM";
            clusterClockText.fontSize = 12;
            clusterClockText.alignment = TextAlignmentOptions.Left;
            clusterClockText.color = ColorTextPrimary;

            GameObject lTurn = CreateUIObject("Left_Turn_Icon", topBar.transform, layer);
            RectTransform ltRect = lTurn.AddComponent<RectTransform>();
            ltRect.anchorMin = new Vector2(0.26f, 0f);
            ltRect.anchorMax = new Vector2(0.35f, 1f);
            ltRect.offsetMin = Vector2.zero;
            ltRect.offsetMax = Vector2.zero;
            leftTurnIcon = lTurn.AddComponent<Image>();
            leftTurnIcon.color = new Color(0.25f, 0.35f, 0.45f, 0.35f);
            GameObject ltTxtObj = CreateUIObject("Text", lTurn.transform, layer);
            RectTransform lttRect = ltTxtObj.AddComponent<RectTransform>();
            lttRect.anchorMin = Vector2.zero;
            lttRect.anchorMax = Vector2.one;
            lttRect.offsetMin = Vector2.zero;
            lttRect.offsetMax = Vector2.zero;
            TextMeshProUGUI ltTxt = ltTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) ltTxt.font = font;
            ltTxt.text = "<";
            ltTxt.fontSize = 14;
            ltTxt.alignment = TextAlignmentOptions.Center;
            ltTxt.color = ColorOkGreen;

            // Speed Limit Badge (120 Circle)
            GameObject spdBadge = CreateUIObject("Speed_Limit_Badge", topBar.transform, layer);
            RectTransform spdbRect = spdBadge.AddComponent<RectTransform>();
            spdbRect.anchorMin = new Vector2(0.44f, 0.12f);
            spdbRect.anchorMax = new Vector2(0.56f, 0.92f);
            spdbRect.offsetMin = Vector2.zero;
            spdbRect.offsetMax = Vector2.zero;
            Image spdbImg = spdBadge.AddComponent<Image>();
            spdbImg.color = Color.white;
            GameObject spdbRing = CreateUIObject("Red_Ring", spdBadge.transform, layer);
            RectTransform spdbrRect = spdbRing.AddComponent<RectTransform>();
            spdbrRect.anchorMin = Vector2.zero;
            spdbrRect.anchorMax = Vector2.one;
            spdbrRect.offsetMin = new Vector2(2f, 2f);
            spdbrRect.offsetMax = new Vector2(-2f, -2f);
            Image spdbrImg = spdbRing.AddComponent<Image>();
            spdbrImg.color = new Color(0.95f, 0.15f, 0.15f, 1f);
            GameObject spdbTxtObj = CreateUIObject("Text", spdBadge.transform, layer);
            RectTransform spdbtRect = spdbTxtObj.AddComponent<RectTransform>();
            spdbtRect.anchorMin = Vector2.zero;
            spdbtRect.anchorMax = Vector2.one;
            spdbtRect.offsetMin = Vector2.zero;
            spdbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI spdbt = spdbTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) spdbt.font = font;
            spdbt.text = "120";
            spdbt.fontSize = 11;
            spdbt.fontStyle = FontStyles.Bold;
            spdbt.alignment = TextAlignmentOptions.Center;
            spdbt.color = Color.black;

            GameObject rTurn = CreateUIObject("Right_Turn_Icon", topBar.transform, layer);
            RectTransform rtRect = rTurn.AddComponent<RectTransform>();
            rtRect.anchorMin = new Vector2(0.65f, 0f);
            rtRect.anchorMax = new Vector2(0.74f, 1f);
            rtRect.offsetMin = Vector2.zero;
            rtRect.offsetMax = Vector2.zero;
            rightTurnIcon = rTurn.AddComponent<Image>();
            rightTurnIcon.color = new Color(0.25f, 0.35f, 0.45f, 0.35f);
            GameObject rtTxtObj = CreateUIObject("Text", rTurn.transform, layer);
            RectTransform rttRect = rtTxtObj.AddComponent<RectTransform>();
            rttRect.anchorMin = Vector2.zero;
            rttRect.anchorMax = Vector2.one;
            rttRect.offsetMin = Vector2.zero;
            rttRect.offsetMax = Vector2.zero;
            TextMeshProUGUI rtTxt = rtTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) rtTxt.font = font;
            rtTxt.text = ">";
            rtTxt.fontSize = 14;
            rtTxt.alignment = TextAlignmentOptions.Center;
            rtTxt.color = ColorOkGreen;

            GameObject outTempObj = CreateUIObject("Cluster_Temp", topBar.transform, layer);
            RectTransform otRect = outTempObj.AddComponent<RectTransform>();
            otRect.anchorMin = new Vector2(0.75f, 0f);
            otRect.anchorMax = new Vector2(0.98f, 1f);
            otRect.offsetMin = Vector2.zero;
            otRect.offsetMax = Vector2.zero;
            clusterTempText = outTempObj.AddComponent<TextMeshProUGUI>();
            if (font) clusterTempText.font = font;
            clusterTempText.text = "27°C";
            clusterTempText.fontSize = 12;
            clusterTempText.alignment = TextAlignmentOptions.Right;
            clusterTempText.color = ColorTextSecondary;

            // 3.2. Floating Navigation Waypoint Banner
            GameObject navBanner = CreateUIObject("ADAS_Nav_Banner", adasObj.transform, layer);
            RectTransform nbRect = navBanner.AddComponent<RectTransform>();
            nbRect.anchorMin = new Vector2(0.06f, 0.74f);
            nbRect.anchorMax = new Vector2(0.94f, 0.88f);
            nbRect.offsetMin = Vector2.zero;
            nbRect.offsetMax = Vector2.zero;
            Image nbImg = navBanner.AddComponent<Image>();
            nbImg.color = new Color(0.06f, 0.10f, 0.16f, 0.92f);

            GameObject nbTxtObj = CreateUIObject("Nav_Banner_Text", navBanner.transform, layer);
            RectTransform nbtRect = nbTxtObj.AddComponent<RectTransform>();
            nbtRect.anchorMin = Vector2.zero;
            nbtRect.anchorMax = Vector2.one;
            nbtRect.offsetMin = new Vector2(6f, 0f);
            nbtRect.offsetMax = new Vector2(-6f, 0f);
            clusterNavBannerText = nbTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) clusterNavBannerText.font = font;
            clusterNavBannerText.text = "<  1.8 KM BÃI XE BÁC BA";
            clusterNavBannerText.fontSize = 12;
            clusterNavBannerText.fontStyle = FontStyles.Bold;
            clusterNavBannerText.alignment = TextAlignmentOptions.Center;
            clusterNavBannerText.color = ColorTextPrimary;

            // 3.3. High-Resolution Perspective Road Surface (Replaces crude rotated rectangles)
            GameObject roadSurface = CreateUIObject("ADAS_Road_Surface", adasObj.transform, layer);
            RectTransform rsRect = roadSurface.AddComponent<RectTransform>();
            rsRect.anchorMin = new Vector2(0.04f, 0.10f);
            rsRect.anchorMax = new Vector2(0.96f, 0.74f);
            rsRect.offsetMin = Vector2.zero;
            rsRect.offsetMax = Vector2.zero;

            // Pre-rendered 512x256 Perspective Road with neon glowing lane rails, sunset bloom & perspective depth
            Sprite roadSprite = LoadSpriteAsset("Assets/_Rearview/Textures/HMI/ADAS_Road_Perspective.png");
            if (roadSprite != null)
            {
                Image rsImg = roadSurface.AddComponent<Image>();
                rsImg.sprite = roadSprite;
                rsImg.color = Color.white;
            }

            // Animated Center Lane Dashes with True 3D Perspective Depth (4 segments for comfortable spacing)
            roadLaneDashes = new RectTransform[4];
            for (int d = 0; d < 4; d++)
            {
                GameObject dashObj = CreateUIObject($"Lane_Dash_{d}", roadSurface.transform, layer);
                roadLaneDashes[d] = dashObj.AddComponent<RectTransform>();
                Image dImg = dashObj.AddComponent<Image>();
                dImg.color = new Color(1f, 0.95f, 0.85f, 0.85f);
            }
            UpdateLaneDashesVisuals(0f);

            // Perception: Traffic Ahead (Mini Car & Truck Silhouettes)
            Sprite trafficCarSprite = LoadSpriteAsset("Assets/_Rearview/Textures/HMI/Traffic_Car.png");
            GameObject trCar = CreateUIObject("Traffic_Car_Ahead", roadSurface.transform, layer);
            trafficCarAhead = trCar.AddComponent<RectTransform>();
            trafficCarAhead.anchorMin = new Vector2(0.5f, 0.5f);
            trafficCarAhead.anchorMax = new Vector2(0.5f, 0.5f);
            trafficCarAhead.sizeDelta = new Vector2(32f, 20f);
            trafficCarAhead.anchoredPosition = new Vector2(trafficCarBaseX, trafficCarBaseY);
            Image trcImg = trCar.AddComponent<Image>();
            if (trafficCarSprite != null)
            {
                trcImg.sprite = trafficCarSprite;
                trcImg.color = Color.white;
            }
            else
            {
                trcImg.color = new Color(0.20f, 0.28f, 0.38f, 0.95f);
            }

            Sprite trafficTruckSprite = LoadSpriteAsset("Assets/_Rearview/Textures/HMI/Traffic_Truck.png");
            GameObject trTruck = CreateUIObject("Traffic_Truck_Ahead", roadSurface.transform, layer);
            trafficTruckAhead = trTruck.AddComponent<RectTransform>();
            trafficTruckAhead.anchorMin = new Vector2(0.5f, 0.5f);
            trafficTruckAhead.anchorMax = new Vector2(0.5f, 0.5f);
            trafficTruckAhead.sizeDelta = new Vector2(36f, 26f);
            trafficTruckAhead.anchoredPosition = new Vector2(trafficTruckBaseX, trafficTruckBaseY);
            Image trtImg = trTruck.AddComponent<Image>();
            if (trafficTruckSprite != null)
            {
                trtImg.sprite = trafficTruckSprite;
                trtImg.color = Color.white;
            }
            else
            {
                trtImg.color = new Color(0.18f, 0.24f, 0.32f, 0.95f);
            }

            // Sleek Mini Player Car Avatar (Aerodynamic Sports Coupe Rear View)
            Sprite carAvatarSprite = LoadSpriteAsset("Assets/_Rearview/Textures/HMI/ADAS_Car_Avatar.png");
            GameObject carAvatar = CreateUIObject("Player_Car_Avatar", roadSurface.transform, layer);
            carAvatarRect = carAvatar.AddComponent<RectTransform>();
            carAvatarRect.anchorMin = new Vector2(0.5f, 0.5f);
            carAvatarRect.anchorMax = new Vector2(0.5f, 0.5f);
            carAvatarRect.sizeDelta = new Vector2(48f, 32f);
            carAvatarRect.anchoredPosition = new Vector2(0f, carAvatarBaseY);
            Image caImg = carAvatar.AddComponent<Image>();
            if (carAvatarSprite != null)
            {
                caImg.sprite = carAvatarSprite;
                caImg.color = Color.white;
            }
            else
            {
                caImg.color = new Color(0.12f, 0.18f, 0.26f, 0.98f);
            }

            // Dynamic Brake Flare Lightbar Overlay (Brightens when braking)
            GameObject caTail = CreateUIObject("LED_Brake_Flare_Overlay", carAvatar.transform, layer);
            RectTransform catRect = caTail.AddComponent<RectTransform>();
            catRect.anchorMin = new Vector2(0.12f, 0.16f);
            catRect.anchorMax = new Vector2(0.88f, 0.34f);
            catRect.offsetMin = Vector2.zero;
            catRect.offsetMax = Vector2.zero;
            carAvatarTaillights = caTail.AddComponent<Image>();
            carAvatarTaillights.color = new Color(1f, 0.15f, 0.15f, 0.4f);

            // 3.4. Bottom ADAS Dock (Compass, Baby Mode Autopilot Badge, Headset)
            GameObject adasDock = CreateUIObject("ADAS_Bottom_Dock", adasObj.transform, layer);
            RectTransform adRect = adasDock.AddComponent<RectTransform>();
            adRect.anchorMin = new Vector2(0.12f, 0f);
            adRect.anchorMax = new Vector2(0.88f, 0.09f);
            adRect.offsetMin = Vector2.zero;
            adRect.offsetMax = Vector2.zero;

            GameObject navIco = CreateUIObject("Compass_Icon", adasDock.transform, layer);
            RectTransform niRect = navIco.AddComponent<RectTransform>();
            niRect.anchorMin = new Vector2(0.15f, 0f);
            niRect.anchorMax = new Vector2(0.30f, 1f);
            niRect.offsetMin = Vector2.zero;
            niRect.offsetMax = Vector2.zero;
            TextMeshProUGUI niTxt = navIco.AddComponent<TextMeshProUGUI>();
            if (font) niTxt.font = font;
            niTxt.text = "N";
            niTxt.fontSize = 12;
            niTxt.fontStyle = FontStyles.Bold;
            niTxt.alignment = TextAlignmentOptions.Center;
            niTxt.color = ColorTextSecondary;

            GameObject steerBadge = CreateUIObject("ADAS_Steering_Badge", adasDock.transform, layer);
            RectTransform sbRect = steerBadge.AddComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(0.42f, 0f);
            sbRect.anchorMax = new Vector2(0.58f, 1f);
            sbRect.offsetMin = Vector2.zero;
            sbRect.offsetMax = Vector2.zero;
            adasSteeringBadge = steerBadge.AddComponent<Image>();
            adasSteeringBadge.color = ColorOkGreen;
            GameObject sbTxtObj = CreateUIObject("Icon", steerBadge.transform, layer);
            RectTransform sbtRect = sbTxtObj.AddComponent<RectTransform>();
            sbtRect.anchorMin = Vector2.zero;
            sbtRect.anchorMax = Vector2.one;
            sbtRect.offsetMin = Vector2.zero;
            sbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI sbt = sbTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) sbt.font = font;
            sbt.text = "ADAS";
            sbt.fontSize = 11;
            sbt.fontStyle = FontStyles.Bold;
            sbt.alignment = TextAlignmentOptions.Center;
            sbt.color = Color.black;

            GameObject headIco = CreateUIObject("Headset_Icon", adasDock.transform, layer);
            RectTransform hiRect = headIco.AddComponent<RectTransform>();
            hiRect.anchorMin = new Vector2(0.70f, 0f);
            hiRect.anchorMax = new Vector2(0.85f, 1f);
            hiRect.offsetMin = Vector2.zero;
            hiRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hiTxt = headIco.AddComponent<TextMeshProUGUI>();
            if (font) hiTxt.font = font;
            hiTxt.text = "AUX";
            hiTxt.fontSize = 11;
            hiTxt.fontStyle = FontStyles.Bold;
            hiTxt.alignment = TextAlignmentOptions.Center;
            hiTxt.color = ColorTextSecondary;

            // =========================================================================
            // 4. RIGHT WING: GEAR, POWER & COOLANT TEMP (X: 0.72 to 0.97)
            // =========================================================================
            GameObject rightWing = CreateUIObject("Right_Wing_Telemetry", hudRootObj.transform, layer);
            RectTransform rwRect = rightWing.AddComponent<RectTransform>();
            rwRect.anchorMin = new Vector2(0.72f, 0.05f);
            rwRect.anchorMax = new Vector2(0.97f, 0.95f);
            rwRect.offsetMin = Vector2.zero;
            rwRect.offsetMax = Vector2.zero;

            GameObject gearObj = CreateUIObject("Gear_Number", rightWing.transform, layer);
            RectTransform gearRect = gearObj.AddComponent<RectTransform>();
            gearRect.anchorMin = new Vector2(0f, 0.50f);
            gearRect.anchorMax = new Vector2(1f, 0.90f);
            gearRect.offsetMin = Vector2.zero;
            gearRect.offsetMax = Vector2.zero;
            gearText = gearObj.AddComponent<TextMeshProUGUI>();
            if (font) gearText.font = font;
            gearText.text = "D7";
            gearText.fontSize = 54;
            gearText.fontStyle = FontStyles.Bold;
            gearText.alignment = TextAlignmentOptions.Center;
            gearText.color = ColorAccentYellow;

            GameObject gearSubObj = CreateUIObject("Gear_Sub_Label", rightWing.transform, layer);
            RectTransform gsRect = gearSubObj.AddComponent<RectTransform>();
            gsRect.anchorMin = new Vector2(0f, 0.38f);
            gsRect.anchorMax = new Vector2(1f, 0.50f);
            gsRect.offsetMin = Vector2.zero;
            gsRect.offsetMax = Vector2.zero;
            gearSubLabelText = gearSubObj.AddComponent<TextMeshProUGUI>();
            if (font) gearSubLabelText.font = font;
            gearSubLabelText.text = "3.4 KWH / 100KM";
            gearSubLabelText.fontSize = 11;
            gearSubLabelText.fontStyle = FontStyles.Bold;
            gearSubLabelText.alignment = TextAlignmentOptions.Center;
            gearSubLabelText.color = ColorTextSecondary;

            GameObject rpmTxtObj = CreateUIObject("RPM_Text", rightWing.transform, layer);
            RectTransform rpmTxtRect = rpmTxtObj.AddComponent<RectTransform>();
            rpmTxtRect.anchorMin = new Vector2(0f, 0.26f);
            rpmTxtRect.anchorMax = new Vector2(1f, 0.38f);
            rpmTxtRect.offsetMin = Vector2.zero;
            rpmTxtRect.offsetMax = Vector2.zero;
            rpmText = rpmTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) rpmText.font = font;
            rpmText.text = "800 RPM";
            rpmText.fontSize = 12;
            rpmText.alignment = TextAlignmentOptions.Center;
            rpmText.color = ColorActiveCyan;

            // Coolant Temperature Gauge
            GameObject tempContainer = CreateUIObject("Temp_Container", rightWing.transform, layer);
            RectTransform tcRect = tempContainer.AddComponent<RectTransform>();
            tcRect.anchorMin = new Vector2(0.06f, 0.10f);
            tcRect.anchorMax = new Vector2(0.94f, 0.28f);
            tcRect.offsetMin = Vector2.zero;
            tcRect.offsetMax = Vector2.zero;

            GameObject tempTxtObj = CreateUIObject("Temp_Text", tempContainer.transform, layer);
            RectTransform ttRect = tempTxtObj.AddComponent<RectTransform>();
            ttRect.anchorMin = new Vector2(0f, 0.50f);
            ttRect.anchorMax = new Vector2(1f, 1f);
            ttRect.offsetMin = Vector2.zero;
            ttRect.offsetMax = Vector2.zero;
            tempText = tempTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) tempText.font = font;
            tempText.text = "40°C TEMP";
            tempText.fontSize = 12;
            tempText.fontStyle = FontStyles.Bold;
            tempText.alignment = TextAlignmentOptions.Left;
            tempText.color = ColorAccentAmber;

            GameObject tempBarBg = CreateUIObject("Temp_Bar_BG", tempContainer.transform, layer);
            RectTransform tbbRect = tempBarBg.AddComponent<RectTransform>();
            tbbRect.anchorMin = new Vector2(0f, 0.05f);
            tbbRect.anchorMax = new Vector2(1f, 0.40f);
            tbbRect.offsetMin = Vector2.zero;
            tbbRect.offsetMax = Vector2.zero;
            Image tbbImg = tempBarBg.AddComponent<Image>();
            tbbImg.color = new Color(0.12f, 0.18f, 0.25f, 0.9f);

            GameObject tempFillObj = CreateUIObject("Temp_Bar_Fill", tempBarBg.transform, layer);
            RectTransform tfRect = tempFillObj.AddComponent<RectTransform>();
            tfRect.anchorMin = Vector2.zero;
            tfRect.anchorMax = Vector2.one;
            tfRect.offsetMin = Vector2.zero;
            tfRect.offsetMax = Vector2.zero;
            tempFillBar = tempFillObj.AddComponent<Image>();
            tempFillBar.type = Image.Type.Filled;
            tempFillBar.fillMethod = Image.FillMethod.Horizontal;
            tempFillBar.fillOrigin = 0;
            tempFillBar.fillAmount = 0.33f;
            tempFillBar.color = ColorAmberGlow;

            // =========================================================================
            // 5. OUTSIDE LEFT AUXILIARY TELEMETRY (X: 0.02 to 0.33)
            // =========================================================================
            GameObject auxLeft = CreateUIObject("Outside_Wheel_Left_Area", clusterPanel.transform, layer);
            RectTransform alRect = auxLeft.AddComponent<RectTransform>();
            alRect.anchorMin = new Vector2(0.02f, 0.10f);
            alRect.anchorMax = new Vector2(0.33f, 0.90f);
            alRect.offsetMin = Vector2.zero;
            alRect.offsetMax = Vector2.zero;

            GameObject dtcObj = CreateUIObject("DTC_Text", auxLeft.transform, layer);
            RectTransform dtcRect = dtcObj.AddComponent<RectTransform>();
            dtcRect.anchorMin = new Vector2(0.04f, 0.65f);
            dtcRect.anchorMax = new Vector2(0.96f, 0.95f);
            dtcRect.offsetMin = Vector2.zero;
            dtcRect.offsetMax = Vector2.zero;
            dtcText = dtcObj.AddComponent<TextMeshProUGUI>();
            if (font) dtcText.font = font;
            dtcText.text = "DTC: NO FAULT CODES DETECTED";
            dtcText.fontSize = 11;
            dtcText.color = ColorOkGreen;

            GameObject sysStatObj = CreateUIObject("Sys_Status", auxLeft.transform, layer);
            RectTransform ssRect = sysStatObj.AddComponent<RectTransform>();
            ssRect.anchorMin = new Vector2(0.04f, 0.40f);
            ssRect.anchorMax = new Vector2(0.96f, 0.60f);
            ssRect.offsetMin = Vector2.zero;
            ssRect.offsetMax = Vector2.zero;
            TextMeshProUGUI ssTxt = sysStatObj.AddComponent<TextMeshProUGUI>();
            if (font) ssTxt.font = font;
            ssTxt.text = "SYS: OPTIMAL | CAM FPS OK";
            ssTxt.fontSize = 10;
            ssTxt.color = new Color(0.4f, 0.6f, 0.75f, 0.7f);

            // Headlight & Handbrake Icons
            GameObject iconsRow = CreateUIObject("Icons_Row", auxLeft.transform, layer);
            RectTransform irRect = iconsRow.AddComponent<RectTransform>();
            irRect.anchorMin = new Vector2(0.04f, 0.10f);
            irRect.anchorMax = new Vector2(0.96f, 0.35f);
            irRect.offsetMin = Vector2.zero;
            irRect.offsetMax = Vector2.zero;

            GameObject hlObj = CreateUIObject("Headlight_Icon", iconsRow.transform, layer);
            RectTransform hlRect = hlObj.AddComponent<RectTransform>();
            hlRect.anchorMin = new Vector2(0.05f, 0f);
            hlRect.anchorMax = new Vector2(0.45f, 1f);
            hlRect.offsetMin = Vector2.zero;
            hlRect.offsetMax = Vector2.zero;
            headlightIcon = hlObj.AddComponent<Image>();
            headlightIcon.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            GameObject hlTxtObj = CreateUIObject("Text", hlObj.transform, layer);
            RectTransform hltRect = hlTxtObj.AddComponent<RectTransform>();
            hltRect.anchorMin = Vector2.zero;
            hltRect.anchorMax = Vector2.one;
            hltRect.offsetMin = Vector2.zero;
            hltRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hlTxt = hlTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) hlTxt.font = font;
            hlTxt.text = "BEAM";
            hlTxt.fontSize = 10;
            hlTxt.alignment = TextAlignmentOptions.Center;
            hlTxt.color = ColorTextSecondary;

            GameObject hbObj = CreateUIObject("Handbrake_Icon", iconsRow.transform, layer);
            RectTransform hbRect = hbObj.AddComponent<RectTransform>();
            hbRect.anchorMin = new Vector2(0.55f, 0f);
            hbRect.anchorMax = new Vector2(0.95f, 1f);
            hbRect.offsetMin = Vector2.zero;
            hbRect.offsetMax = Vector2.zero;
            handbrakeIcon = hbObj.AddComponent<Image>();
            handbrakeIcon.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            GameObject hbTxtObj = CreateUIObject("Text", hbObj.transform, layer);
            RectTransform hbtRect = hbTxtObj.AddComponent<RectTransform>();
            hbtRect.anchorMin = Vector2.zero;
            hbtRect.anchorMax = Vector2.one;
            hbtRect.offsetMin = Vector2.zero;
            hbtRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hbTxt = hbTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) hbTxt.font = font;
            hbTxt.text = "(P) BRAKE";
            hbTxt.fontSize = 10;
            hbTxt.alignment = TextAlignmentOptions.Center;
            hbTxt.color = new Color(1f, 0.2f, 0.2f, 1f);
        }

        private void UpdateLaneDashesVisuals(float timer)
        {
            if (roadLaneDashes == null) return;

            const float yHorizon = 0.50f; // Vanishing point at the horizon in ADAS_Road_Perspective.png
            const float yNear = 0.06f;    // Foreground road near player car avatar
            const float wNear = 7.5f;     // Foreground dash width (px)
            const float wFar = 1.0f;      // Horizon dash width (px)
            const float hNear = 22.0f;    // Foreground dash length (px) - reduced from 36px so spacing between dashes is much larger
            const float hFar = 2.0f;      // Horizon dash length (px)

            for (int i = 0; i < roadLaneDashes.Length; i++)
            {
                if (roadLaneDashes[i] != null)
                {
                    // t = 0 (horizon) to t = 1 (near car)
                    float t = (timer + (float)i / roadLaneDashes.Length) % 1.0f;

                    // 3D Perspective Power Curve (p = 2.2 gives natural geometric spacing)
                    float tDepth = Mathf.Pow(t, 2.2f);
                    float yNorm = Mathf.Lerp(yHorizon, yNear, tDepth);

                    // Width & height compress toward the horizon
                    float w = Mathf.Lerp(wFar, wNear, Mathf.Pow(t, 1.8f));
                    float h = Mathf.Lerp(hFar, hNear, Mathf.Pow(t, 2.0f));
                    float alpha = Mathf.Lerp(0.12f, 0.95f, Mathf.Pow(t, 1.2f));

                    roadLaneDashes[i].anchorMin = new Vector2(0.5f, yNorm);
                    roadLaneDashes[i].anchorMax = new Vector2(0.5f, yNorm);
                    roadLaneDashes[i].sizeDelta = new Vector2(w, h);

                    Image dImg = roadLaneDashes[i].GetComponent<Image>();
                    if (dImg != null) dImg.color = new Color(1f, 0.95f, 0.85f, alpha);
                }
            }
        }

        private void BuildCarPlayInfotainment(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject infoPanel = CreateUIObject("CarPlay_Infotainment_Panel", parent.transform, layer);
            RectTransform iRect = infoPanel.AddComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.495f, 0f);
            iRect.anchorMax = new Vector2(1f, 1f);
            iRect.offsetMin = new Vector2(10f, 10f);
            iRect.offsetMax = new Vector2(-15f, -10f);

            // =============================================================
            // 1. CARPLAY LEFT RAIL DOCK (X: 0 to 0.08, ~75px wide)
            // =============================================================
            BuildLeftRailDock(infoPanel, layer, font);

            // =============================================================
            // 2. MAIN CONTENT AREA (X: 0.085 to 1.0)
            // =============================================================
            GameObject contentArea = CreateUIObject("CarPlay_Content_Area", infoPanel.transform, layer);
            RectTransform caRect = contentArea.AddComponent<RectTransform>();
            caRect.anchorMin = new Vector2(0.085f, 0f);
            caRect.anchorMax = new Vector2(1f, 1f);
            caRect.offsetMin = Vector2.zero;
            caRect.offsetMax = Vector2.zero;

            // Build Launcher Grid (8 Squircles)
            BuildCarPlayLauncherGrid(contentArea, layer, font);

            // Build 8 Fullscreen App Screens
            appScreens = new GameObject[8];
            appScreens[0] = BuildAppScreen("App_Phone", contentArea, layer, font, "LIÊN LẠC KHẨN CẤP (EMERGENCY COMMS)", "KÊNH TRỰC BAN CỨU HỘ DUYÊN HẢI: CHƯA CÓ CUỘC GỌI\nBẤM [1..8] ĐỂ ĐỔI APP");
            appScreens[1] = BuildMusicApp(contentArea, layer, font);
            appScreens[2] = BuildNavApp(contentArea, layer, font);
            appScreens[3] = BuildCassetteApp(contentArea, layer, font);
            appScreens[4] = BuildDiagnosticsApp(contentArea, layer, font);
            appScreens[5] = BuildNotesApp(contentArea, layer, font);
            appScreens[6] = BuildWeatherApp(contentArea, layer, font);
            appScreens[7] = BuildSettingsApp(contentArea, layer, font);
        }

        private void BuildLeftRailDock(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject rail = CreateUIObject("Left_Rail_Dock", parent.transform, layer);
            RectTransform rRect = rail.AddComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0f, 0f);
            rRect.anchorMax = new Vector2(0.08f, 1f);
            rRect.offsetMin = Vector2.zero;
            rRect.offsetMax = Vector2.zero;

            Image rImg = rail.AddComponent<Image>();
            rImg.color = new Color(0.05f, 0.07f, 0.11f, 0.95f);

            // Time Badge at Top
            GameObject clkObj = CreateUIObject("Rail_Clock", rail.transform, layer);
            RectTransform clkRect = clkObj.AddComponent<RectTransform>();
            clkRect.anchorMin = new Vector2(0.05f, 0.82f);
            clkRect.anchorMax = new Vector2(0.95f, 0.98f);
            clkRect.offsetMin = Vector2.zero;
            clkRect.offsetMax = Vector2.zero;
            railClockText = clkObj.AddComponent<TextMeshProUGUI>();
            if (font) railClockText.font = font;
            railClockText.text = "10:48";
            railClockText.fontSize = 17;
            railClockText.fontStyle = FontStyles.Bold;
            railClockText.alignment = TextAlignmentOptions.Center;
            railClockText.color = ColorAccentYellow;

            // Signal Badge
            GameObject sigObj = CreateUIObject("Rail_Signal", rail.transform, layer);
            RectTransform sigRect = sigObj.AddComponent<RectTransform>();
            sigRect.anchorMin = new Vector2(0.05f, 0.68f);
            sigRect.anchorMax = new Vector2(0.95f, 0.82f);
            sigRect.offsetMin = Vector2.zero;
            sigRect.offsetMax = Vector2.zero;
            railSignalText = sigObj.AddComponent<TextMeshProUGUI>();
            if (font) railSignalText.font = font;
            railSignalText.text = "5G";
            railSignalText.fontSize = 14;
            railSignalText.fontStyle = FontStyles.Bold;
            railSignalText.alignment = TextAlignmentOptions.Center;
            railSignalText.color = ColorTextSecondary;

            // 3 Recent App Mini Icons (Maps, Music, Car)
            CreateRailMiniIcon(rail.transform, layer, font, new Vector2(0.12f, 0.48f), new Vector2(0.88f, 0.64f), "NAV", new Color(0f, 0.48f, 1f), () => SelectApp(AAOSApp.Maps));
            CreateRailMiniIcon(rail.transform, layer, font, new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.46f), "FM", new Color(1f, 0.18f, 0.33f), () => SelectApp(AAOSApp.Music));
            CreateRailMiniIcon(rail.transform, layer, font, new Vector2(0.12f, 0.14f), new Vector2(0.88f, 0.28f), "OBD", new Color(1f, 0.58f, 0f), () => SelectApp(AAOSApp.Diagnostics));

            // Home Grid Button at Bottom [ :: ]
            GameObject homeObj = CreateUIObject("Rail_Home_Btn", rail.transform, layer);
            RectTransform hRect = homeObj.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.1f, 0.02f);
            hRect.anchorMax = new Vector2(0.9f, 0.12f);
            hRect.offsetMin = Vector2.zero;
            hRect.offsetMax = Vector2.zero;
            Image hImg = homeObj.AddComponent<Image>();
            hImg.color = new Color(0.15f, 0.22f, 0.32f, 0.8f);
            railHomeBtn = homeObj.AddComponent<Button>();
            railHomeBtn.targetGraphic = hImg;
            railHomeBtn.onClick.AddListener(() => SelectApp(AAOSApp.Launcher));

            GameObject hTxtObj = CreateUIObject("Text", homeObj.transform, layer);
            RectTransform htRect = hTxtObj.AddComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.offsetMin = Vector2.zero;
            htRect.offsetMax = Vector2.zero;
            TextMeshProUGUI hTxt = hTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) hTxt.font = font;
            hTxt.text = "::";
            hTxt.fontSize = 18;
            hTxt.fontStyle = FontStyles.Bold;
            hTxt.alignment = TextAlignmentOptions.Center;
            hTxt.color = ColorActiveCyan;
        }

        private void CreateRailMiniIcon(Transform parent, int layer, TMP_FontAsset font, Vector2 min, Vector2 max, string icon, Color color, UnityEngine.Events.UnityAction action)
        {
            GameObject obj = CreateUIObject("MiniIcon", parent, layer);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = obj.AddComponent<Image>();
            img.color = color;
            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(action);

            GameObject tObj = CreateUIObject("Text", obj.transform, layer);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            TextMeshProUGUI txt = tObj.AddComponent<TextMeshProUGUI>();
            if (font) txt.font = font;
            txt.text = icon;
            txt.fontSize = 12;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
        }

        private void BuildCarPlayLauncherGrid(GameObject parent, int layer, TMP_FontAsset font)
        {
            launcherGridPanel = CreateUIObject("CarPlay_Launcher_Grid", parent.transform, layer);
            RectTransform lgRect = launcherGridPanel.AddComponent<RectTransform>();
            lgRect.anchorMin = Vector2.zero;
            lgRect.anchorMax = Vector2.one;
            lgRect.offsetMin = Vector2.zero;
            lgRect.offsetMax = Vector2.zero;

            gridCardOutlines = new Image[8];
            gridCardButtons = new Button[8];

            // 8 Squircles Definition (Matching user's reference image):
            // Row 1:
            // 0: Phone (Green #34C759)
            // 1: Music (Red #FF2D55)
            // 2: Maps (Blue #007AFF)
            // 3: Cassette (Purple #AF52DE)
            // Row 2:
            // 4: Diag (Orange #FF9500)
            // 5: Notes (Amber #FF9F0A)
            // 6: Weather (Sky Blue #32ADE6)
            // 7: Settings (Silver/Gray #8E8E93)

            string[] icons = new string[] { "TEL", "FM", "NAV", "TAPE", "OBD", "NOTE", "METEO", "SYS" };
            string[] titles = new string[] { "Phone", "Music", "Maps", "Cassette", "Diag", "Notes", "Weather", "Settings" };
            Color[] colors = new Color[]
            {
                new Color(0.2f, 0.78f, 0.35f),  // Phone Green
                new Color(1f, 0.18f, 0.33f),    // Music Red
                new Color(0f, 0.48f, 1f),       // Maps Blue
                new Color(0.69f, 0.32f, 0.87f), // Cassette Purple
                new Color(1f, 0.58f, 0f),       // Diag Orange
                new Color(1f, 0.62f, 0.04f),    // Notes Amber
                new Color(0.2f, 0.68f, 0.9f),   // Weather Sky Blue
                new Color(0.56f, 0.56f, 0.58f)  // Settings Silver
            };

            float colWidth = 0.23f;
            float colGap = 0.02f;
            float leftMargin = 0.02f;

            for (int i = 0; i < 8; i++)
            {
                int appIdx = i;
                int row = i / 4; // 0 for Top Row, 1 for Bottom Row
                int col = i % 4;

                Vector2 min, max;
                if (row == 0)
                {
                    min = new Vector2(leftMargin + col * (colWidth + colGap), 0.52f);
                    max = new Vector2(min.x + colWidth, 0.94f);
                }
                else
                {
                    min = new Vector2(leftMargin + col * (colWidth + colGap), 0.08f);
                    max = new Vector2(min.x + colWidth, 0.48f);
                }

                GameObject cardObj = CreateUIObject($"Squircle_{titles[i]}", launcherGridPanel.transform, layer);
                RectTransform crt = cardObj.AddComponent<RectTransform>();
                crt.anchorMin = min;
                crt.anchorMax = max;
                crt.offsetMin = Vector2.zero;
                crt.offsetMax = Vector2.zero;

                // Squircle Outline / Border for Focus Navigation
                Image outlineImg = cardObj.AddComponent<Image>();
                outlineImg.color = (i == 0) ? ColorActiveCyan : ColorCardBorderInactive;
                gridCardOutlines[i] = outlineImg;

                Button btn = cardObj.AddComponent<Button>();
                btn.targetGraphic = outlineImg;
                btn.onClick.AddListener(() => LaunchAppByIndex(appIdx));
                gridCardButtons[i] = btn;

                // Inner Squircle Fill
                GameObject fillObj = CreateUIObject("InnerFill", cardObj.transform, layer);
                RectTransform frt = fillObj.AddComponent<RectTransform>();
                frt.anchorMin = new Vector2(0.04f, 0.04f);
                frt.anchorMax = new Vector2(0.96f, 0.96f);
                frt.offsetMin = Vector2.zero;
                frt.offsetMax = Vector2.zero;
                Image fillImg = fillObj.AddComponent<Image>();
                fillImg.color = colors[i];

                // Central Icon
                GameObject icObj = CreateUIObject("Icon", fillObj.transform, layer);
                RectTransform irt = icObj.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.1f, 0.32f);
                irt.anchorMax = new Vector2(0.9f, 0.92f);
                irt.offsetMin = Vector2.zero;
                irt.offsetMax = Vector2.zero;
                TextMeshProUGUI itxt = icObj.AddComponent<TextMeshProUGUI>();
                if (font) itxt.font = font;
                itxt.text = icons[i];
                itxt.fontSize = 38;
                itxt.alignment = TextAlignmentOptions.Center;

                // Label Underneath
                GameObject lbObj = CreateUIObject("Label", fillObj.transform, layer);
                RectTransform lbrt = lbObj.AddComponent<RectTransform>();
                lbrt.anchorMin = new Vector2(0.05f, 0.06f);
                lbrt.anchorMax = new Vector2(0.95f, 0.32f);
                lbrt.offsetMin = Vector2.zero;
                lbrt.offsetMax = Vector2.zero;
                TextMeshProUGUI lbtxt = lbObj.AddComponent<TextMeshProUGUI>();
                if (font) lbtxt.font = font;
                lbtxt.text = $"{titles[i]} [{i + 1}]";
                lbtxt.fontSize = 12;
                lbtxt.fontStyle = FontStyles.Bold;
                lbtxt.alignment = TextAlignmentOptions.Center;
                lbtxt.color = Color.white;
            }

            // Pagination Dots at Bottom
            GameObject dotsObj = CreateUIObject("Pagination_Dots", launcherGridPanel.transform, layer);
            RectTransform drt = dotsObj.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.4f, 0.005f);
            drt.anchorMax = new Vector2(0.6f, 0.07f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            TextMeshProUGUI dtxt = dotsObj.AddComponent<TextMeshProUGUI>();
            if (font) dtxt.font = font;
            dtxt.text = "●  ○  ○";
            dtxt.fontSize = 12;
            dtxt.alignment = TextAlignmentOptions.Center;
            dtxt.color = ColorTextSecondary;
        }

        private GameObject BuildAppScreen(string name, GameObject parent, int layer, TMP_FontAsset font, string title, string detail)
        {
            GameObject panel = CreateUIObject(name, parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("FullCard", panel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack(title, card.transform, layer, font);

            GameObject dObj = CreateUIObject("Detail", card.transform, layer);
            RectTransform drt = dObj.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.05f, 0.1f);
            drt.anchorMax = new Vector2(0.95f, 0.75f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            TextMeshProUGUI dtxt = dObj.AddComponent<TextMeshProUGUI>();
            if (font) dtxt.font = font;
            dtxt.text = detail;
            dtxt.fontSize = 16;
            dtxt.color = ColorTextPrimary;

            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildMusicApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject panel = CreateUIObject("App_Music", parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("MusicCard", panel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("ĐÀI PHÁT THANH FM & TRÌNH PHÁT NHẠC (MUSIC)", card.transform, layer, font);

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

            CreateActionButton("Btn_Prev", card.transform, layer, font, new Vector2(0.04f, 0.06f), new Vector2(0.32f, 0.28f), "< KÊNH TRƯỚC (Q)", PrevStation);
            CreateActionButton("Btn_Next", card.transform, layer, font, new Vector2(0.35f, 0.06f), new Vector2(0.63f, 0.28f), "KÊNH KẾ TIẾP (E) >", NextStation);

            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildNavApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject panel = CreateUIObject("App_Maps", parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("NavCard", panel.transform, layer, Vector2.zero, Vector2.one);
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

            waypointText = navDestTitleText;
            fatherNoteText = navFatherNoteText;

            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildCassetteApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject panel = CreateUIObject("App_Cassette", parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("CassetteCard", panel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("BĂNG CASSETTE KỶ VẬT CỦA BỐ (1998)", card.transform, layer, font);

            GameObject tObj = CreateUIObject("Title", card.transform, layer);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.05f, 0.58f);
            trt.anchorMax = new Vector2(0.95f, 0.78f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            cassetteTitleText = tObj.AddComponent<TextMeshProUGUI>();
            if (font) cassetteTitleText.font = font;
            cassetteTitleText.text = "BĂNG CASSETTE: LỜI NHẮN CỦA BỐ (1998)";
            cassetteTitleText.fontSize = 20;
            cassetteTitleText.fontStyle = FontStyles.Bold;
            cassetteTitleText.color = ColorAccentYellow;

            GameObject qObj = CreateUIObject("Quote", card.transform, layer);
            RectTransform qrt = qObj.AddComponent<RectTransform>();
            qrt.anchorMin = new Vector2(0.05f, 0.20f);
            qrt.anchorMax = new Vector2(0.95f, 0.55f);
            qrt.offsetMin = Vector2.zero;
            qrt.offsetMax = Vector2.zero;
            cassetteQuoteText = qObj.AddComponent<TextMeshProUGUI>();
            if (font) cassetteQuoteText.font = font;
            cassetteQuoteText.text = "\"Chào con. Xe này bố tự đóng, chạy tốt đấy. Nhớ kiểm tra dầu máy mỗi 5000 cây...\"";
            cassetteQuoteText.fontSize = 16;
            cassetteQuoteText.fontStyle = FontStyles.Italic;
            cassetteQuoteText.color = ColorTextPrimary;

            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildDiagnosticsApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject panel = CreateUIObject("App_Diag", parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("DiagCard", panel.transform, layer, Vector2.zero, Vector2.one);
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

            CreateActionButton("Btn_Scan", card.transform, layer, font, new Vector2(0.04f, 0.05f), new Vector2(0.45f, 0.22f), "🔍 QUÉT / CHUYỂN MÃ LỖI (R)", CycleDTC);

            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildNotesApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject panel = CreateUIObject("App_Notes", parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("NotesCard", panel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("SỔ TAY GHI CHÉP CỦA BỐ (FATHER'S LOG)", card.transform, layer, font);

            GameObject tObj = CreateUIObject("Title", card.transform, layer);
            RectTransform trt = tObj.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.05f, 0.65f);
            trt.anchorMax = new Vector2(0.95f, 0.78f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            notesTitleText = tObj.AddComponent<TextMeshProUGUI>();
            if (font) notesTitleText.font = font;
            notesTitleText.text = "SỔ TAY CŨ CỦA BỐ (DIY REPAIR LOG)";
            notesTitleText.fontSize = 18;
            notesTitleText.fontStyle = FontStyles.Bold;
            notesTitleText.color = ColorAccentYellow;

            GameObject cObj = CreateUIObject("Content", card.transform, layer);
            RectTransform crt = cObj.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.05f, 0.1f);
            crt.anchorMax = new Vector2(0.95f, 0.65f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            notesContentText = cObj.AddComponent<TextMeshProUGUI>();
            if (font) notesContentText.font = font;
            notesContentText.text = "- 12/03/1998: Mua khung xe cũ từ bãi phế liệu anh Ba.\n- 05/06/1998: Hàn lại giàn gầm, thay bugi và cảm biến nhiệt độ nước.\n- 18/09/1998: Gắn thử màn hình LCD tự chế. Hy vọng con trai sẽ thích.";
            notesContentText.fontSize = 14;
            notesContentText.color = ColorTextPrimary;

            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildWeatherApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject panel = CreateUIObject("App_Weather", parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("WeatherCard", panel.transform, layer, Vector2.zero, Vector2.one);
            CreateAppHeaderWithBack("THỜI TIẾT KHU VỰC VEN BIỂN (COASTAL WEATHER)", card.transform, layer, font);

            GameObject mObj = CreateUIObject("Main", card.transform, layer);
            RectTransform mrt = mObj.AddComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.05f, 0.55f);
            mrt.anchorMax = new Vector2(0.95f, 0.78f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            weatherMainText = mObj.AddComponent<TextMeshProUGUI>();
            if (font) weatherMainText.font = font;
            weatherMainText.text = "MƯA ĐÊM VEN BIỂN (COASTAL RAIN)";
            weatherMainText.fontSize = 22;
            weatherMainText.fontStyle = FontStyles.Bold;
            weatherMainText.color = ColorAccentYellow;

            GameObject dObj = CreateUIObject("Detail", card.transform, layer);
            RectTransform drt = dObj.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.05f, 0.1f);
            drt.anchorMax = new Vector2(0.95f, 0.55f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            weatherDetailText = dObj.AddComponent<TextMeshProUGUI>();
            if (font) weatherDetailText.font = font;
            weatherDetailText.text = "NHIỆT ĐỘ: 16°C | ĐỘ ẨM: 92% | GIÓ BIỂN: 24 KM/H\nCẢNH BÁO: ĐƯỜNG TRƠN TRỢT TRÊN CUNG ĐƯỜNG ĐÈO TIẾP THEO";
            weatherDetailText.fontSize = 15;
            weatherDetailText.color = ColorTextSecondary;

            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildSettingsApp(GameObject parent, int layer, TMP_FontAsset font)
        {
            GameObject panel = CreateUIObject("App_Settings", parent.transform, layer);
            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            GameObject card = CreateCard("SettingsCard", panel.transform, layer, Vector2.zero, Vector2.one);
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

            CreateActionButton("Btn_ToggleBabyMode", card.transform, layer, font, new Vector2(0.04f, 0.05f), new Vector2(0.48f, 0.22f), "BẬT / TẮT BABY MODE (B)", ToggleBabyMode);

            panel.SetActive(false);
            return panel;
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
            img.color = new Color(0.06f, 0.09f, 0.14f, 0.95f);
            return card;
        }

        private void CreateAppHeaderWithBack(string title, Transform cardTransform, int layer, TMP_FontAsset font)
        {
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
            bbTxt.text = "< LAUNCHER (ESC)";
            bbTxt.fontSize = 13;
            bbTxt.fontStyle = FontStyles.Bold;
            bbTxt.alignment = TextAlignmentOptions.Center;
            bbTxt.color = ColorActiveCyan;

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

        public static Sprite LoadSpriteAsset(string relativePath)
        {
            Sprite sp = null;
#if UNITY_EDITOR
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativePath);
            if (sp != null) return sp;
#endif
            // Standalone or fallback runtime loading
            string fullPath = System.IO.Path.Combine(Application.dataPath, relativePath.StartsWith("Assets/") ? relativePath.Substring(7) : relativePath);
            if (System.IO.File.Exists(fullPath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(fileData))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            return sp;
        }
        #endregion
    }
}
