﻿using PilotAssistant.Utility;
using System;
using UnityEngine;

namespace PilotAssistant.FlightModules
{
    public class AsstVesselModule : VesselModule
    {
        public PilotAssistant vesselAsst;
        public VesselData vesselData;

        public override Activation GetActivation()
        {
            return Activation.LoadedVessels;
        }

        public override bool ShouldBeActive()
        {
            return Vessel.loaded;
        }

        protected override void OnStart()
        {
            base.OnStart();
            try
            {
                vesselAsst = new PilotAssistant(this);
                vesselData = new VesselData(this);
                PilotAssistantFlightCore.Instance.AddVessel(this);

                vesselAsst.Start();

                Vessel.OnPreAutopilotUpdate += new FlightInputCallback(PreAutoPilotUpdate);
                Vessel.OnPostAutopilotUpdate += new FlightInputCallback(PostAutoPilotUpdate);

                GameEvents.onVesselChange.Add(VesselSwitch);
                GameEvents.onTimeWarpRateChanged.Add(WarpHandler);

                if (FlightGlobals.ActiveVessel == Vessel)
                {
                    VesselSwitch(Vessel);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Startup error", Logger.LogLevel.Error);
                Logger.Log(ex.Message, Logger.LogLevel.Error);
                Logger.Log(ex.InnerException, Logger.LogLevel.Error);
                Logger.Log(ex.StackTrace, Logger.LogLevel.Error);
            }
        }

        public void Update()
        {
            if (ReferenceEquals(Vessel, null))
            {
                return;
            }

            vesselAsst.Update();
        }

        public void WarpHandler()
        {
            vesselAsst.WarpHandler();
        }

        public void VesselSwitch(Vessel v)
        {
            if (v == Vessel)
            {
                vesselAsst.VesselSwitch(v);
            }
        }

        public void PreAutoPilotUpdate(FlightCtrlState state)
        {
            if (Vessel.HoldPhysics)
            {
                return;
            }

            vesselData.UpdateAttitude();
        }

        public void PostAutoPilotUpdate(FlightCtrlState state)
        {
            if (Vessel.HoldPhysics)
            {
                return;
            }

            vesselAsst.VesselController(state);
        }

        public void OnGUI()
        {
            if (PilotAssistantFlightCore.bHideUI || PilotAssistantFlightCore.Instance == null
                || PilotAssistantFlightCore.Instance.selectedVesselIndex >= PilotAssistantFlightCore.Instance.controlledVessels.Count
                || PilotAssistantFlightCore.Instance.controlledVessels[PilotAssistantFlightCore.Instance.selectedVesselIndex] != this)
            {
                return;
            }

            vesselAsst.DrawGUI();
        }

        public void OnDestroy()
        {
            if (Vessel != null)
            {
                GameEvents.onVesselChange.Remove(VesselSwitch);
                GameEvents.onTimeWarpRateChanged.Remove(WarpHandler);

                Vessel.OnPreAutopilotUpdate -= PreAutoPilotUpdate;
                Vessel.OnPostAutopilotUpdate -= PostAutoPilotUpdate;
                if (!ReferenceEquals(vesselAsst, null))
                {
                    vesselAsst.OnDestroy();
                    if (!ReferenceEquals(PilotAssistantFlightCore.Instance, null))
                    {
                        PilotAssistantFlightCore.Instance.RemoveVessel(this);
                    }
                }
                vesselAsst = null;
                vesselData = null;
            }
        }
    }
}