using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rearview
{
    /// <summary>
    /// Manages tactile mechanical and electronic audio feedback for vehicles in The Last Waypoint:
    /// - Door latch click, hinge creak, and heavy metal door slam.
    /// - Key turn, starter motor crank, and engine roar.
    /// - Mechanical dashboard toggle clicks and soft electronic HMI touchscreen beeps.
    /// - Windshield wiper sweeps.
    /// Includes built-in procedural synthesis fallbacks if audio assets are missing.
    /// </summary>
    [AddComponentMenu("Rearview/Vehicle Audio Feedback")]
    public class VehicleAudioFeedback : MonoBehaviour
    {
        public static VehicleAudioFeedback Instance { get; private set; }

        [Header("--- Audio Source ---")]
        public AudioSource audioSource;

        [Header("--- Door SFX ---")]
        public AudioClip doorHandleClip;
        public AudioClip doorOpenClip;
        public AudioClip doorCloseClip;

        [Header("--- Engine Ignition SFX ---")]
        public AudioClip engineIgnitionClip;

        [Header("--- Cockpit & HMI SFX ---")]
        public AudioClip buttonClickClip;
        public AudioClip touchBeepClip;
        public AudioClip wiperSweepClip;

        // Procedurally generated fallback clips if none assigned
        private AudioClip synthBeepClip;
        private AudioClip synthClickClip;
        private AudioClip synthThudClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (!audioSource)
            {
                audioSource = GetComponent<AudioSource>();
                if (!audioSource)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 0.5f; // half 2D, half 3D
                    audioSource.playOnAwake = false;
                }
            }

            GenerateProceduralFallbacks();
            AutoAssignProjectClips();
        }

        private void AutoAssignProjectClips()
        {
            // Auto-pick from existing RCC / Malbers audio clips if unassigned
            if (!buttonClickClip)
            {
                buttonClickClip = Resources.Load<AudioClip>("Sounds/Misc/Indicator");
            }
            if (!doorCloseClip)
            {
                doorCloseClip = Resources.Load<AudioClip>("Sounds/Misc/Bump");
            }
            if (!engineIgnitionClip)
            {
                engineIgnitionClip = Resources.Load<AudioClip>("Sounds/ExhaustFires/EngineStart");
            }
        }

        private void GenerateProceduralFallbacks()
        {
            synthBeepClip = CreateSineWaveClip("SynthTouchBeep", 1200f, 0.06f, 0.25f);
            synthClickClip = CreateNoiseClickClip("SynthButtonClick", 0.03f, 0.35f);
            synthThudClip = CreateThudClip("SynthDoorThud", 80f, 0.2f, 0.5f);
        }

        private AudioClip CreateSineWaveClip(string name, float frequency, float duration, float volume)
        {
            int sampleRate = 44100;
            int samplesCount = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samplesCount];

            for (int i = 0; i < samplesCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / samplesCount; // Linear decay
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateNoiseClickClip(string name, float duration, float volume)
        {
            int sampleRate = 44100;
            int samplesCount = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samplesCount];

            for (int i = 0; i < samplesCount; i++)
            {
                float decay = Mathf.Exp(-i / (sampleRate * 0.005f));
                data[i] = (Random.value * 2f - 1f) * decay * volume;
            }

            AudioClip clip = AudioClip.Create(name, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateThudClip(string name, float frequency, float duration, float volume)
        {
            int sampleRate = 44100;
            int samplesCount = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samplesCount];

            for (int i = 0; i < samplesCount; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-i / (sampleRate * 0.04f));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * decay * volume;
            }

            AudioClip clip = AudioClip.Create(name, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void PlayDoorHandle()
        {
            AudioClip clip = doorHandleClip ? doorHandleClip : synthClickClip;
            PlayClip(clip, 0.7f, Random.Range(0.95f, 1.05f));
        }

        public void PlayDoorOpen()
        {
            AudioClip clip = doorOpenClip ? doorOpenClip : synthClickClip;
            PlayClip(clip, 0.6f, Random.Range(0.9f, 1.1f));
        }

        public void PlayDoorClose()
        {
            AudioClip clip = doorCloseClip ? doorCloseClip : synthThudClip;
            PlayClip(clip, 0.85f, Random.Range(0.95f, 1.05f));
        }

        public void PlayEngineIgnition()
        {
            AudioClip clip = engineIgnitionClip ? engineIgnitionClip : synthThudClip;
            PlayClip(clip, 1.0f, 1.0f);
        }

        public void PlayButtonClick()
        {
            AudioClip clip = buttonClickClip ? buttonClickClip : synthClickClip;
            PlayClip(clip, 0.6f, Random.Range(0.95f, 1.1f));
        }

        public void PlayTouchBeep()
        {
            AudioClip clip = touchBeepClip ? touchBeepClip : synthBeepClip;
            PlayClip(clip, 0.5f, Random.Range(0.98f, 1.02f));
        }

        public void PlayWiperSweep()
        {
            AudioClip clip = wiperSweepClip ? wiperSweepClip : synthClickClip;
            PlayClip(clip, 0.45f, 1.0f);
        }

        private void PlayClip(AudioClip clip, float volume, float pitch)
        {
            if (audioSource == null || clip == null) return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
