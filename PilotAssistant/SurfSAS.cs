﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using AppLauncher;
    using UI;
    using Presets;

    internal enum SASList
    {
        Pitch,
        Hdg,
        Roll
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class SurfSAS : MonoBehaviour
    {
        internal static List<PID_Controller> SASControllers = new List<PID_Controller>();

        internal static bool bInit = false;
        internal static bool bArmed = false;
        internal static bool bActive = false;
        internal bool[] bPause = new bool[3]; // pause on a per axis basis
        internal bool bAtmosphere = false;
        internal static bool bStockSAS = false;
        internal static bool bWasStockSAS = false;

        internal static float activationFadeRoll = 1;
        internal static float activationFadePitch = 1;
        internal static float activationFadeYaw = 1;

        public void Initialise()
        {
            // register vessel if not already
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            // grab stock PID values
            if (FlightData.thisVessel.VesselSAS.pidLockedPitch != null)
            {
                PresetManager.defaultStockSASTuning = new PresetSAS(FlightData.thisVessel.VesselSAS, "Stock");
                PresetManager.activeStockSASPreset = PresetManager.defaultStockSASTuning;

                PID_Controller pitch = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(pitch);
                PID_Controller yaw = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(yaw);
                PID_Controller roll = new PID.PID_Controller(0.1, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(roll);
                PresetManager.defaultSASTuning = new PresetSAS(SASControllers, "Default");
                PresetManager.activeSASPreset = PresetManager.defaultSASTuning;

                bInit = true;
                bPause[0] = bPause[1] = bPause[2] = false;
            }

            // Init colours
            GeneralUI.InitColors();
        }

        public void OnDestroy()
        {
            bInit = false;
            bArmed = false;
            bActive = false;

            SASControllers.Clear();

            //AppLauncherInstance.bDisplaySAS = false;
            //SASPresetWindow.bShowPresets = false;
        }

        public void Update()
        {
            if (!bInit)
                Initialise();

            // SAS activated by user
            if (bArmed && !bActive && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bActive = true;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                updateTarget();
            }
            else if (bActive && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bActive = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }

            // Atmospheric mode tracks horizon, don't want in space
            if (FlightData.thisVessel.staticPressure > 0 && !bAtmosphere)
            {
                bAtmosphere = true;
                if (FlightData.thisVessel.ctrlState.killRot)
                {
                    bActive = true;
                    FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                }
            }
            else if (FlightData.thisVessel.staticPressure == 0 && bAtmosphere)
            {
                bAtmosphere = false;
                if (bActive)
                {
                    bActive = false;
                    FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                }
            }

            pauseManager(); // manage activation of SAS axes depending on user input
        }

        public void OnGUI()
        {
            if (!PilotAssistant.hide)
                SASMainWindow.Draw();
        }

        public void FixedUpdate()
        {
            if (bInit && bArmed && bActive)
            {
                FlightData.updateAttitude();

                float pitchResponse = -1 * (float)SASControllers[(int)SASList.Pitch].Response(FlightData.pitch);

                float yawResponse = 0;
                if (SASControllers[(int)SASList.Hdg].SetPoint - FlightData.heading >= -180 && SASControllers[(int)SASList.Hdg].SetPoint - FlightData.heading <= 180)
                    yawResponse = -1 * (float)SASControllers[(int)SASList.Hdg].Response(FlightData.heading);
                else if (SASControllers[(int)SASList.Hdg].SetPoint - FlightData.heading < -180)
                    yawResponse = -1 * (float)SASControllers[(int)SASList.Hdg].Response(FlightData.heading - 360);
                else if (SASControllers[(int)SASList.Hdg].SetPoint - FlightData.heading > 180)
                    yawResponse = -1 * (float)SASControllers[(int)SASList.Hdg].Response(FlightData.heading + 360);

                double rollRad = Math.PI / 180 * FlightData.roll;

                if (!bPause[(int)SASList.Pitch])
                {
                    FlightData.thisVessel.ctrlState.pitch = (pitchResponse * (float)Math.Cos(rollRad) - yawResponse * (float)Math.Sin(rollRad)) / activationFadePitch;
                    if (activationFadePitch > 1)
                        activationFadePitch *= 0.98f; // ~100 physics frames
                    else
                        activationFadePitch = 1;
                }

                if (!bPause[(int)SASList.Hdg])
                {
                    FlightData.thisVessel.ctrlState.yaw = (pitchResponse * (float)Math.Sin(rollRad) + yawResponse * (float)Math.Cos(rollRad)) / activationFadeYaw;
                    if (activationFadeYaw > 1)
                        activationFadeYaw *= 0.98f; // ~100 physics frames
                    else
                        activationFadeYaw = 1;
                }

                if (!bPause[(int)SASList.Roll])
                {
                    if (SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll >= -180 && SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll <= 180)
                        FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll) / activationFadeRoll;
                    else if (SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll > 180)
                        FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll + 360) / activationFadeRoll;
                    else if (SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll < -180)
                        FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll - 360) / activationFadeRoll;

                    if (activationFadeRoll > 1)
                        activationFadeRoll *= 0.98f; // ~100 physics frames
                    else
                        activationFadeRoll = 1;
                }
            }
        }

        internal static void updateTarget()
        {
            SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
            SASControllers[(int)SASList.Hdg].SetPoint = FlightData.heading;
            SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;

            activationFadeRoll = 10;
            activationFadePitch = 10;
            activationFadeYaw = 10;
        }

        private void pauseManager()
        {
            if (GameSettings.PITCH_DOWN.GetKeyDown() || GameSettings.PITCH_UP.GetKeyDown() || GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
            {
                bPause[(int)SASList.Pitch] = true;
                bPause[(int)SASList.Hdg] = true;
            }
            if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp() || GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = false;
                bPause[(int)SASList.Hdg] = false;
                if (bActive)
                {
                    SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
                    SASControllers[(int)SASList.Hdg].SetPoint = FlightData.heading;
                }

                activationFadePitch = 10;
                activationFadeYaw = 10;
            }

            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                bPause[(int)SASList.Roll] = true;
            if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Roll] = false;
                if (bActive)
                    SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;

                activationFadeRoll = 10;
            }

            if (GameSettings.SAS_HOLD.GetKeyDown())
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Hdg] = true;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
            if (GameSettings.SAS_HOLD.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Hdg] = false;
                updateTarget();
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
        }
    }
}