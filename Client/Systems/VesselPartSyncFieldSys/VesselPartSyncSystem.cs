﻿using LunaClient.Base;
using LunaClient.Events;
using LunaClient.Systems.TimeSyncer;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace LunaClient.Systems.VesselPartSyncFieldSys
{
    /// <summary>
    /// This class sends some parts of the vessel information to other players. We do it in another system as we don't want to send this information so often as
    /// the vessel position system and also we want to send it more oftenly than the vessel proto.
    /// </summary>
    public class VesselPartSyncFieldSystem : MessageSystem<VesselPartSyncFieldSystem, VesselPartSyncFieldMessageSender, VesselPartSyncFieldMessageHandler>
    {
        #region Fields & properties

        public bool PartSyncSystemReady => Enabled && HighLogic.LoadedScene >= GameScenes.FLIGHT && Time.timeSinceLevelLoad > 1f;

        private VesselPartSyncFieldEvents VesselPartModuleSyncFieldEvents { get; } = new VesselPartSyncFieldEvents();

        public ConcurrentDictionary<Guid, VesselPartSyncFieldQueue> VesselPartsSyncs { get; } = new ConcurrentDictionary<Guid, VesselPartSyncFieldQueue>();

        #endregion

        #region Base overrides        

        protected override bool ProcessMessagesInUnityThread => false;

        public override string SystemName { get; } = nameof(VesselPartSyncFieldSystem);

        protected override void OnEnabled()
        {
            base.OnEnabled();
            PartModuleEvent.onPartModuleBoolFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleBoolFieldChanged);
            PartModuleEvent.onPartModuleIntFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleIntFieldChanged);
            PartModuleEvent.onPartModuleFloatFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleFloatFieldChanged);
            PartModuleEvent.onPartModuleDoubleFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleDoubleFieldChanged);
            PartModuleEvent.onPartModuleVectorFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleVectorFieldChanged);
            PartModuleEvent.onPartModuleQuaternionFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleQuaternionFieldChanged);
            PartModuleEvent.onPartModuleStringFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleStringFieldChanged);
            PartModuleEvent.onPartModuleObjectFieldChanged.Add(VesselPartModuleSyncFieldEvents.PartModuleObjectFieldChanged);

            SetupRoutine(new RoutineDefinition(250, RoutineExecution.Update, ProcessVesselPartSyncs));
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();
            PartModuleEvent.onPartModuleBoolFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleBoolFieldChanged);
            PartModuleEvent.onPartModuleIntFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleIntFieldChanged);
            PartModuleEvent.onPartModuleFloatFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleFloatFieldChanged);
            PartModuleEvent.onPartModuleDoubleFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleDoubleFieldChanged);
            PartModuleEvent.onPartModuleVectorFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleVectorFieldChanged);
            PartModuleEvent.onPartModuleQuaternionFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleQuaternionFieldChanged);
            PartModuleEvent.onPartModuleStringFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleStringFieldChanged);
            PartModuleEvent.onPartModuleObjectFieldChanged.Remove(VesselPartModuleSyncFieldEvents.PartModuleObjectFieldChanged);

            VesselPartsSyncs.Clear();
        }

        #endregion

        #region Update routines

        private void ProcessVesselPartSyncs()
        {
            if (HighLogic.LoadedScene < GameScenes.SPACECENTER) return;

            foreach (var keyVal in VesselPartsSyncs)
            {
                while (keyVal.Value.TryPeek(out var update) && update.GameTime <= TimeSyncerSystem.UniversalTime)
                {
                    keyVal.Value.TryDequeue(out update);
                    update.ProcessPartMethodSync();
                    keyVal.Value.Recycle(update);
                }
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Removes a vessel from the system
        /// </summary>
        public void RemoveVessel(Guid vesselId)
        {
            VesselPartsSyncs.TryRemove(vesselId, out _);
        }

        #endregion
    }
}