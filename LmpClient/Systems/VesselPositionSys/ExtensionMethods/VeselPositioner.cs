﻿using LmpClient.Systems.TimeSyncer;
using LmpCommon;
using UnityEngine;

namespace LmpClient.Systems.VesselPositionSys.ExtensionMethods
{
    public static class VeselPositioner
    {
        public static void SetVesselPosition(this Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, float percentage)
        {
            if (vessel == null || update == null || target == null) return;

            var lerpedBody = percentage < 0.5 ? update.Body : target.Body;

            ApplyOrbitInterpolation(vessel, update, target, lerpedBody, percentage);

            //Do not use CoM. It's not needed and it generate issues when you patch the protovessel with it as it generate weird commnet lines
            //It's important to set the static pressure as otherwise the vessel situation is not updated correctly when
            //Vessel.updateSituation() is called in the Vessel.LateUpdate(). Same applies for landed and splashed
            vessel.staticPressurekPa = FlightGlobals.getStaticPressure(target.LatLonAlt[2], lerpedBody);
            vessel.heightFromTerrain = target.HeightFromTerrain;

            if (!vessel.loaded)
            {
                //DO NOT lerp the latlonalt as otherwise if you are in orbit you will see landed vessels in the map view with weird jittering
                vessel.latitude = target.LatLonAlt[0];
                vessel.longitude = target.LatLonAlt[1];
                vessel.altitude = target.LatLonAlt[2];

                if (vessel.LandedOrSplashed)
                    vessel.SetPosition(lerpedBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude));
            }
            else
            {
                ApplyInterpolationsToLoadedVessel(vessel, update, target, lerpedBody, percentage);
            }
        }

        private static void ApplyOrbitInterpolation(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            var currentPos = update.KspOrbit.getRelativePositionAtUT(TimeSyncerSystem.UniversalTime);
            var targetPos = target.KspOrbit.getRelativePositionAtUT(TimeSyncerSystem.UniversalTime);

            var currentVel = update.KspOrbit.getOrbitalVelocityAtUT(TimeSyncerSystem.UniversalTime);
            var targetVel = target.KspOrbit.getOrbitalVelocityAtUT(TimeSyncerSystem.UniversalTime);

            var lerpedPos = Vector3d.Lerp(currentPos, targetPos, percentage);
            var lerpedVel = Vector3d.Lerp(currentVel, targetVel, percentage);

            vessel.orbit.UpdateFromStateVectors(lerpedPos, lerpedVel, lerpedBody, TimeSyncerSystem.UniversalTime);
        }

        private static void ApplyInterpolationsToLoadedVessel(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            var currentSurfaceRelRotation = Quaternion.Slerp(update.SurfaceRelRotation, target.SurfaceRelRotation, percentage);

            //If you don't set srfRelRotation and vessel is packed it won't change it's rotation
            vessel.srfRelRotation = currentSurfaceRelRotation;

            vessel.Landed = percentage < 0.5 ? update.Landed : target.Landed;
            vessel.Splashed = percentage < 0.5 ? update.Splashed : target.Splashed;

            vessel.latitude = LunaMath.Lerp(update.LatLonAlt[0], target.LatLonAlt[0], percentage);
            vessel.longitude = LunaMath.Lerp(update.LatLonAlt[1], target.LatLonAlt[1], percentage);
            vessel.altitude = LunaMath.Lerp(update.LatLonAlt[2], target.LatLonAlt[2], percentage);

            var rotation = (Quaternion)lerpedBody.rotation * currentSurfaceRelRotation;
            if (vessel.situation <= Vessel.Situations.PRELAUNCH)
            {
                SetLoadedVesselPositionAndRotation(vessel, lerpedBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude), rotation);
            }
            else
            {
                SetLoadedVesselPositionAndRotation(vessel, vessel.orbit.getPositionAtUT(TimeSyncerSystem.UniversalTime), rotation);
            }
        }

        /// <summary>
        /// Checks if we must resume the velocity of the part
        /// It's important do resume it as otherwise during docking, the orbital speeds are not displayed correctly and you won't be able to dock
        /// </summary>
        private static bool MustResumeVelocity(Vessel vessel)
        {
            if (!vessel.packed && vessel.rootPart?.rb != null)
            {
                var velBeforeCorrection = vessel.rootPart.rb.velocity;
                vessel.rootPart.ResumeVelocity();
                return velBeforeCorrection != vessel.rootPart.rb.velocity;
            }

            return false;
        }

        /// <summary>
        /// Here we set the position and the rotation of every part at once, this is much more optimized than calling SetRotation and SetPosition
        /// </summary>
        private static void SetLoadedVesselPositionAndRotation(Vessel vessel, Vector3d position, Quaternion rotation)
        {
            var mustFixVelocity = MustResumeVelocity(vessel);
            if (!vessel.packed)
            {
                foreach (var part in vessel.parts)
                {
                    if (part.physicalSignificance == Part.PhysicalSignificance.FULL)
                    {
                        //Apply rotation to part
                        part.partTransform.rotation = rotation * part.orgRot;
                        part.partTransform.position = vessel.vesselTransform.position + vessel.vesselTransform.rotation * part.orgPos;

                        part.partTransform.position += position - vessel.vesselTransform.position;
                        if (mustFixVelocity)
                        {
                            //Always run this at the end!!
                            //Otherwise during docking, the orbital speeds are not displayed correctly and you won't be able to dock
                            part.ResumeVelocity();
                        }
                    }
                }
            }
            else
            {
                foreach (var part in vessel.parts)
                {
                    part.partTransform.rotation = rotation * part.orgRot;
                    part.partTransform.position = position + vessel.vesselTransform.rotation * part.orgPos;
                    if (mustFixVelocity)
                    {
                        //Always run this at the end!!
                        //Otherwise during docking, the orbital speeds are not displayed correctly and you won't be able to dock
                        part.ResumeVelocity();
                    }
                }
            }
        }
    }
}