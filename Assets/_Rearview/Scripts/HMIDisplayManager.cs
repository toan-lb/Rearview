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
    /// Renders a dedicated 2D Canvas into a RenderTexture (P2P_Screen_RT) mapped to the 3D Curve_Screen.
    /// Displays driver cluster telemetry (Speed, RPM, Gear, DTC) and central infotainment (FM Radio, Baby Mode, Waypoints).
    /// </summary>
    [AddComponentMenu("Rearview/HMI Display Manager")]
    [DefaultExecutionOrder(-5)]
    public class HMIDisplayManager : MonoBehaviour
    {
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

        // Cluster Elements (Driver side)
        [Header("--- Cluster UI Elements ---")]
        public TextMeshProUGUI speedText;
        public TextMeshProUGUI rpmText;
        public TextMeshProUGUI gearText;
        public Image rpmBar;
        public TextMeshProUGUI dtcText;
        public TextMeshProUGUI tempText;
        public TextMeshProUGUI batteryText;
        public Image headlightIcon;
        public Image handbrakeIcon;

        // Infotainment Elements (Center side)
        [Header("--- Infotainment UI Elements ---")]
        public TextMeshProUGUI clockText;
        public TextMeshProUGUI weatherText;
        public TextMeshProUGUI radioStationText;
        public TextMeshProUGUI radioTrackText;
        public Image[] visualizerBars;
        public TextMeshProUGUI babyModeText;
        public TextMeshProUGUI waypointText;
        public TextMeshProUGUI fatherNoteText;

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

        private void Awake()
        {
            InitDefaults();
        }

        private void Start()
        {
            FindVehicle();
            EnsureUIExists();
            SyncMaterialProperties();
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

        private void Update()
        {
            FindVehicle();
            HandleInput();
            UpdateTelemetry();
            UpdateInfotainment();
        }

        private void HandleInput()
        {
            // Interactive keys: [1] Prev Station, [2] Next Station, [3] Scan DTC, [4] Toggle Baby Mode
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                {
                    currentStationIndex = (currentStationIndex - 1 + radioStations.Length) % radioStations.Length;
                }
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                {
                    currentStationIndex = (currentStationIndex + 1) % radioStations.Length;
                }
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                {
                    currentDtcIndex = (currentDtcIndex + 1) % dtcCodes.Length;
                }
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                {
                    babyModeActive = !babyModeActive;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                currentStationIndex = (currentStationIndex - 1 + radioStations.Length) % radioStations.Length;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                currentStationIndex = (currentStationIndex + 1) % radioStations.Length;
            }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                currentDtcIndex = (currentDtcIndex + 1) % dtcCodes.Length;
            }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                babyModeActive = !babyModeActive;
            }
#endif
        }

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

            if (rpmText != null)
                rpmText.text = rpm.ToString("0") + " RPM";

            if (gearText != null)
                gearText.text = gear;

            if (rpmBar != null)
            {
                float fill = Mathf.Clamp01(rpm / 7000f);
                rpmBar.fillAmount = fill;
                rpmBar.color = Color.Lerp(new Color(0.2f, 0.8f, 1f), Color.red, Mathf.InverseLerp(0.7f, 1f, fill));
            }

            if (tempText != null)
            {
                tempText.text = $"TEMP: {simulatedTemp:0}°C";
                tempText.color = simulatedTemp > 100f ? Color.red : new Color(0.7f, 0.9f, 1f);
            }

            if (batteryText != null)
                batteryText.text = $"BAT: {simulatedBattery:0.0}V";

            if (dtcText != null)
            {
                dtcText.text = dtcCodes[currentDtcIndex];
                dtcText.color = currentDtcIndex == 0 ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.4f, 0.2f);
            }

            if (headlightIcon != null)
                headlightIcon.color = headlights ? new Color(0.3f, 0.8f, 1f) : new Color(0.3f, 0.3f, 0.3f, 0.4f);

            if (handbrakeIcon != null)
                handbrakeIcon.color = handbrake ? new Color(1f, 0.2f, 0.2f) : new Color(0.3f, 0.3f, 0.3f, 0.4f);
        }

        private void UpdateInfotainment()
        {
            if (clockText != null)
                clockText.text = System.DateTime.Now.ToString("hh:mm tt");

            if (radioStationText != null)
                radioStationText.text = radioStations[currentStationIndex];

            if (radioTrackText != null)
                radioTrackText.text = radioTracks[currentStationIndex];

            if (babyModeText != null)
            {
                babyModeText.text = babyModeActive ? "CABIN_STABILIZER (BABY_MODE): ON" : "CABIN_STABILIZER: OFF";
                babyModeText.color = babyModeActive ? new Color(0.4f, 1f, 0.7f) : new Color(0.7f, 0.7f, 0.7f);
            }

            // Audio visualizer bars animation
            visualizerTimer += Time.deltaTime * 8f;
            if (visualizerBars != null)
            {
                for (int i = 0; i < visualizerBars.Length; i++)
                {
                    if (visualizerBars[i] != null)
                    {
                        float wave = Mathf.PingPong(visualizerTimer + i * 0.7f, 1f);
                        visualizerBars[i].fillAmount = Mathf.Lerp(0.15f, 0.95f, wave);
                    }
                }
            }
        }

        #region Procedural UI Hierarchy Builder
        /// <summary>
        /// Automatically constructs the full HMI camera, canvas, and UI layout if not assigned.
        /// </summary>
        public void EnsureUIExists()
        {
            if (hmiCanvas != null && hmiCamera != null)
                return;

            int targetLayer = LayerMask.NameToLayer(hmiLayerName);
            if (targetLayer < 0) targetLayer = LayerMask.NameToLayer("UI");
            if (targetLayer < 0) targetLayer = 0;

            // 1. Create HMI Root & Camera
            GameObject hmiRoot = GameObject.Find("HMI_System");
            if (hmiRoot == null)
            {
                hmiRoot = new GameObject("HMI_System");
                hmiRoot.transform.position = new Vector3(0, -500f, 0); // Put in isolated off-screen space
            }

            if (hmiCamera == null)
            {
                GameObject camObj = new GameObject("HMI_Camera");
                camObj.transform.SetParent(hmiRoot.transform, false);
                camObj.transform.localPosition = new Vector3(0, 0, -10f);
                hmiCamera = camObj.AddComponent<Camera>();
                hmiCamera.clearFlags = CameraClearFlags.SolidColor;
                hmiCamera.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
                hmiCamera.orthographic = true;
                hmiCamera.orthographicSize = 1.6f;
                hmiCamera.nearClipPlane = 0.1f;
                hmiCamera.farClipPlane = 20f;
                hmiCamera.cullingMask = 1 << targetLayer;
                hmiCamera.targetTexture = targetRenderTexture;
            }

            // 2. Create Canvas
            if (hmiCanvas == null)
            {
                GameObject canvasObj = new GameObject("HMI_Canvas");
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

                BuildCanvasLayout(canvasObj, targetLayer);
            }
        }

        private void BuildCanvasLayout(GameObject canvasObj, int layer)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // Background Panel
            GameObject bg = CreateUIObject("Background", canvasObj.transform, layer);
            RectTransform bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.05f, 0.08f, 1f);

            // Left Section: Driver Cluster (Anchor: 0 to 0.48)
            GameObject clusterPanel = CreateUIObject("Driver_Cluster_Panel", canvasObj.transform, layer);
            RectTransform cRect = clusterPanel.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 0f);
            cRect.anchorMax = new Vector2(0.48f, 1f);
            cRect.offsetMin = new Vector2(20f, 15f);
            cRect.offsetMax = new Vector2(-10f, -15f);

            // Speedometer Big Number
            GameObject speedObj = CreateUIObject("Speed_Number", clusterPanel.transform, layer);
            RectTransform speedRect = speedObj.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(0.15f, 0.2f);
            speedRect.anchorMax = new Vector2(0.6f, 0.85f);
            speedRect.offsetMin = Vector2.zero;
            speedRect.offsetMax = Vector2.zero;
            speedText = speedObj.AddComponent<TextMeshProUGUI>();
            if (font) speedText.font = font;
            speedText.text = "0";
            speedText.fontSize = 110;
            speedText.fontStyle = FontStyles.Bold;
            speedText.alignment = TextAlignmentOptions.Center;
            speedText.color = Color.white;

            // KM/H Unit Label
            GameObject kmhObj = CreateUIObject("KMH_Label", clusterPanel.transform, layer);
            RectTransform kmhRect = kmhObj.AddComponent<RectTransform>();
            kmhRect.anchorMin = new Vector2(0.55f, 0.25f);
            kmhRect.anchorMax = new Vector2(0.8f, 0.5f);
            kmhRect.offsetMin = Vector2.zero;
            kmhRect.offsetMax = Vector2.zero;
            TextMeshProUGUI kmhText = kmhObj.AddComponent<TextMeshProUGUI>();
            if (font) kmhText.font = font;
            kmhText.text = "KM/H";
            kmhText.fontSize = 24;
            kmhText.fontStyle = FontStyles.Bold;
            kmhText.color = new Color(0.5f, 0.7f, 1f);

            // Gear Label
            GameObject gearObj = CreateUIObject("Gear_Label", clusterPanel.transform, layer);
            RectTransform gearRect = gearObj.AddComponent<RectTransform>();
            gearRect.anchorMin = new Vector2(0.55f, 0.55f);
            gearRect.anchorMax = new Vector2(0.8f, 0.85f);
            gearRect.offsetMin = Vector2.zero;
            gearRect.offsetMax = Vector2.zero;
            gearText = gearObj.AddComponent<TextMeshProUGUI>();
            if (font) gearText.font = font;
            gearText.text = "P";
            gearText.fontSize = 44;
            gearText.fontStyle = FontStyles.Bold;
            gearText.color = new Color(1f, 0.85f, 0.3f);

            // RPM Bar (Horizontal fill)
            GameObject rpmBg = CreateUIObject("RPM_Bar_BG", clusterPanel.transform, layer);
            RectTransform rpmBgRect = rpmBg.AddComponent<RectTransform>();
            rpmBgRect.anchorMin = new Vector2(0.05f, 0.08f);
            rpmBgRect.anchorMax = new Vector2(0.95f, 0.18f);
            rpmBgRect.offsetMin = Vector2.zero;
            rpmBgRect.offsetMax = Vector2.zero;
            Image rpmBgImg = rpmBg.AddComponent<Image>();
            rpmBgImg.color = new Color(0.15f, 0.2f, 0.25f, 0.8f);

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
            rpmBar.color = new Color(0.2f, 0.8f, 1f);

            // RPM Text
            GameObject rpmTxtObj = CreateUIObject("RPM_Text", clusterPanel.transform, layer);
            RectTransform rpmTxtRect = rpmTxtObj.AddComponent<RectTransform>();
            rpmTxtRect.anchorMin = new Vector2(0.7f, 0.2f);
            rpmTxtRect.anchorMax = new Vector2(0.98f, 0.35f);
            rpmTxtRect.offsetMin = Vector2.zero;
            rpmTxtRect.offsetMax = Vector2.zero;
            rpmText = rpmTxtObj.AddComponent<TextMeshProUGUI>();
            if (font) rpmText.font = font;
            rpmText.text = "800 RPM";
            rpmText.fontSize = 18;
            rpmText.color = new Color(0.6f, 0.8f, 1f);

            // Status Bar (Top of Cluster): Battery & Temp
            GameObject battObj = CreateUIObject("Battery_Text", clusterPanel.transform, layer);
            RectTransform battRect = battObj.AddComponent<RectTransform>();
            battRect.anchorMin = new Vector2(0.05f, 0.85f);
            battRect.anchorMax = new Vector2(0.35f, 0.98f);
            battRect.offsetMin = Vector2.zero;
            battRect.offsetMax = Vector2.zero;
            batteryText = battObj.AddComponent<TextMeshProUGUI>();
            if (font) batteryText.font = font;
            batteryText.text = "BAT: 12.6V";
            batteryText.fontSize = 18;
            batteryText.color = new Color(0.7f, 0.9f, 1f);

            GameObject tempObj = CreateUIObject("Temp_Text", clusterPanel.transform, layer);
            RectTransform tempRect = tempObj.AddComponent<RectTransform>();
            tempRect.anchorMin = new Vector2(0.4f, 0.85f);
            tempRect.anchorMax = new Vector2(0.75f, 0.98f);
            tempRect.offsetMin = Vector2.zero;
            tempRect.offsetMax = Vector2.zero;
            tempText = tempObj.AddComponent<TextMeshProUGUI>();
            if (font) tempText.font = font;
            tempText.text = "TEMP: 88°C";
            tempText.fontSize = 18;
            tempText.color = new Color(0.7f, 0.9f, 1f);

            // DTC Fault Code
            GameObject dtcObj = CreateUIObject("DTC_Text", clusterPanel.transform, layer);
            RectTransform dtcRect = dtcObj.AddComponent<RectTransform>();
            dtcRect.anchorMin = new Vector2(0.05f, 0.0f);
            dtcRect.anchorMax = new Vector2(0.95f, 0.08f);
            dtcRect.offsetMin = Vector2.zero;
            dtcRect.offsetMax = Vector2.zero;
            dtcText = dtcObj.AddComponent<TextMeshProUGUI>();
            if (font) dtcText.font = font;
            dtcText.text = "DTC: NO FAULT CODES DETECTED";
            dtcText.fontSize = 13;
            dtcText.color = new Color(0.3f, 1f, 0.4f);

            // Center Divider Line
            GameObject divider = CreateUIObject("Divider", canvasObj.transform, layer);
            RectTransform divRect = divider.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.495f, 0.05f);
            divRect.anchorMax = new Vector2(0.505f, 0.95f);
            divRect.offsetMin = Vector2.zero;
            divRect.offsetMax = Vector2.zero;
            Image divImg = divider.AddComponent<Image>();
            divImg.color = new Color(0.2f, 0.3f, 0.4f, 0.6f);

            // Right Section: Central Infotainment (Anchor: 0.52 to 1.0)
            GameObject infoPanel = CreateUIObject("Center_Infotainment_Panel", canvasObj.transform, layer);
            RectTransform iRect = infoPanel.AddComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.52f, 0f);
            iRect.anchorMax = new Vector2(1f, 1f);
            iRect.offsetMin = new Vector2(10f, 15f);
            iRect.offsetMax = new Vector2(-20f, -15f);

            // Top Bar: Clock & Weather
            GameObject clockObj = CreateUIObject("Clock_Text", infoPanel.transform, layer);
            RectTransform clockRect = clockObj.AddComponent<RectTransform>();
            clockRect.anchorMin = new Vector2(0f, 0.85f);
            clockRect.anchorMax = new Vector2(0.4f, 0.98f);
            clockRect.offsetMin = Vector2.zero;
            clockRect.offsetMax = Vector2.zero;
            clockText = clockObj.AddComponent<TextMeshProUGUI>();
            if (font) clockText.font = font;
            clockText.text = "04:45 AM";
            clockText.fontSize = 22;
            clockText.fontStyle = FontStyles.Bold;
            clockText.color = Color.white;

            GameObject weatherObj = CreateUIObject("Weather_Text", infoPanel.transform, layer);
            RectTransform weatherRect = weatherObj.AddComponent<RectTransform>();
            weatherRect.anchorMin = new Vector2(0.45f, 0.85f);
            weatherRect.anchorMax = new Vector2(0.95f, 0.98f);
            weatherRect.offsetMin = Vector2.zero;
            weatherRect.offsetMax = Vector2.zero;
            weatherText = weatherObj.AddComponent<TextMeshProUGUI>();
            if (font) weatherText.font = font;
            weatherText.text = "COASTAL RAIN  16°C";
            weatherText.fontSize = 18;
            weatherText.alignment = TextAlignmentOptions.Right;
            weatherText.color = new Color(0.6f, 0.8f, 1f);

            // Radio Station Box
            GameObject radioBox = CreateUIObject("Radio_Box", infoPanel.transform, layer);
            RectTransform radioBoxRect = radioBox.AddComponent<RectTransform>();
            radioBoxRect.anchorMin = new Vector2(0f, 0.45f);
            radioBoxRect.anchorMax = new Vector2(0.95f, 0.82f);
            radioBoxRect.offsetMin = Vector2.zero;
            radioBoxRect.offsetMax = Vector2.zero;
            Image rBoxImg = radioBox.AddComponent<Image>();
            rBoxImg.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);

            GameObject rStationObj = CreateUIObject("Station_Text", radioBox.transform, layer);
            RectTransform rStationRect = rStationObj.AddComponent<RectTransform>();
            rStationRect.anchorMin = new Vector2(0.03f, 0.5f);
            rStationRect.anchorMax = new Vector2(0.95f, 0.95f);
            rStationRect.offsetMin = Vector2.zero;
            rStationRect.offsetMax = Vector2.zero;
            radioStationText = rStationObj.AddComponent<TextMeshProUGUI>();
            if (font) radioStationText.font = font;
            radioStationText.text = radioStations[0];
            radioStationText.fontSize = 20;
            radioStationText.fontStyle = FontStyles.Bold;
            radioStationText.color = new Color(1f, 0.85f, 0.4f);

            GameObject rTrackObj = CreateUIObject("Track_Text", radioBox.transform, layer);
            RectTransform rTrackRect = rTrackObj.AddComponent<RectTransform>();
            rTrackRect.anchorMin = new Vector2(0.03f, 0.05f);
            rTrackRect.anchorMax = new Vector2(0.6f, 0.5f);
            rTrackRect.offsetMin = Vector2.zero;
            rTrackRect.offsetMax = Vector2.zero;
            radioTrackText = rTrackObj.AddComponent<TextMeshProUGUI>();
            if (font) radioTrackText.font = font;
            radioTrackText.text = radioTracks[0];
            radioTrackText.fontSize = 15;
            radioTrackText.color = new Color(0.8f, 0.8f, 0.8f);

            // Audio Visualizer Bars (8 bars)
            visualizerBars = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                GameObject bar = CreateUIObject($"VisBar_{i}", radioBox.transform, layer);
                RectTransform bRect = bar.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.65f + i * 0.04f, 0.1f);
                bRect.anchorMax = new Vector2(0.675f + i * 0.04f, 0.55f);
                bRect.offsetMin = Vector2.zero;
                bRect.offsetMax = Vector2.zero;
                Image barImg = bar.AddComponent<Image>();
                barImg.type = Image.Type.Filled;
                barImg.fillMethod = Image.FillMethod.Vertical;
                barImg.fillOrigin = 0;
                barImg.fillAmount = 0.5f;
                barImg.color = new Color(0.3f, 0.9f, 0.6f);
                visualizerBars[i] = barImg;
            }

            // Baby Mode & Waypoint Section (Bottom of Infotainment)
            GameObject babyObj = CreateUIObject("BabyMode_Text", infoPanel.transform, layer);
            RectTransform babyRect = babyObj.AddComponent<RectTransform>();
            babyRect.anchorMin = new Vector2(0f, 0.24f);
            babyRect.anchorMax = new Vector2(0.95f, 0.42f);
            babyRect.offsetMin = Vector2.zero;
            babyRect.offsetMax = Vector2.zero;
            babyModeText = babyObj.AddComponent<TextMeshProUGUI>();
            if (font) babyModeText.font = font;
            babyModeText.text = "CABIN_STABILIZER (BABY_MODE): ON";
            babyModeText.fontSize = 17;
            babyModeText.fontStyle = FontStyles.Bold;
            babyModeText.color = new Color(0.4f, 1f, 0.7f);

            // Father's Waypoint & DIY Note
            GameObject wpObj = CreateUIObject("Waypoint_Text", infoPanel.transform, layer);
            RectTransform wpRect = wpObj.AddComponent<RectTransform>();
            wpRect.anchorMin = new Vector2(0f, 0.05f);
            wpRect.anchorMax = new Vector2(0.95f, 0.22f);
            wpRect.offsetMin = Vector2.zero;
            wpRect.offsetMax = Vector2.zero;
            waypointText = wpObj.AddComponent<TextMeshProUGUI>();
            if (font) waypointText.font = font;
            waypointText.text = "WAYPOINT: Bãi Phế Liệu Bác Ba (1.8 km)";
            waypointText.fontSize = 15;
            waypointText.color = new Color(1f, 0.9f, 0.6f);

            Debug.Log("<color=cyan><b>[HMIDisplayManager]</b></color> Đã tự động tạo hoàn chỉnh hệ thống HMI Canvas & Camera!");
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
