/*
Silica Dedicated Server Optimizer
Copyright (C) 2026 by databomb

* Description *
Optimizes performance of a dedicated server use case by preserving 
CPU cycles that are spent on unneeded tasks (e.g., GPU/rendering, 
reviewing user input, etc.)

* License *
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using MelonLoader;
using Si_ServerOptimizer;
using System;  
using Silica;
using HarmonyLib;
using System.Reflection;
using InstancedLODBatching;
using Silica.UI;
using Silica.Voting;
using DebugTools;
using Silica.UI.Menus;
using Sandbox.Flow.Gizmos;
using Baltarus.Blueprints;

[assembly: MelonInfo(typeof(Optimizer), "Server Optimizer", "0.7.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_ServerOptimizer
{
    public partial class Optimizer : MelonMod
    {
        // save CPU cycles on the server by patching out calls related to strictly UI matters

        // skip graphics settings for servers
        [HarmonyPatch(typeof(GameSettings), nameof(GameSettings.Load), new Type[] { })]
        internal class Patch_Disable_GameSettings
        {
            static bool Prefix(GameSettings? __instance) { MelonLogger.Msg("Disable user settings..."); __instance = null; return false; }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.UpdateCameraFOV))]
        internal class Patch_Disable_Game_UpdateGameraFOV { static bool Prefix() { return false; } }

        [HarmonyPatch(typeof(FreeFlyCameraController), nameof(FreeFlyCameraController.Awake))]
        internal class Patch_Disable_FreeFlyCameraController1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(FreeFlyCameraController), "OnEnable")]
        internal class Patch_Disable_FreeFlyCameraController2 { static bool Prefix() { return false; } }

        // FogOfWar needed for path finding to work correctly on server
        /*[HarmonyPatch(typeof(FogOfWar), nameof(FogOfWar.OnUpdate))]
        internal class Patch_Disable_FogOfWar
        {
            static bool Prefix() { return false; }
        }*/

        [HarmonyPatch(typeof(Silica.UI.UIManager), "LateUpdate")]
        internal class Patch_Disable_UIUpdates { static bool Prefix() { return false; } }

        // decals
        [HarmonyPatch(typeof(UnityEngine.Rendering.HighDefinition.DecalProjector), "OnEnable")]
        internal class Patch_Disable_DecalProjector1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(UnityEngine.Rendering.HighDefinition.DecalProjector), "Awake")]
        internal class Patch_Disable_DecalProjector2 { static bool Prefix() { return false; } }

        // distance culling unncessary
        [HarmonyPatch(typeof(DisableBasedOnDistance), "Awake")]
        internal class Patch_Disable_DistanceCulling1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(CullingComponent), "OnEnable")]
        internal class Patch_Disable_DistanceCulling2 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(CullingComponent), "OnDisable")]
        internal class Patch_Disable_DistanceCulling3 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(DisableBasedOnDistance), nameof(DisableBasedOnDistance.UpdateBasedOnDistance))]
        internal class Patch_Disable_DistanceCulling4 { static bool Prefix() { return false; } }

        // loading screen Silica.UI.Loading.LoadingScreenController
        [HarmonyPatch(typeof(Silica.UI.Loading.LoadingScreenController), "Awake")]
        internal class Patch_Disable_LoadingScreenController1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(Silica.UI.Loading.LoadingScreenController), "Update")]
        internal class Patch_Disable_LoadingScreenController2 { static bool Prefix() { return false; } }

        // LOD groups aren't needed on the server
        [HarmonyPatch(typeof(InstancedLODBatch), nameof(InstancedLODBatch.AddObject))]
        internal class Patch_Disable_LOD1
        {
            static bool Prefix(bool __result) { __result = false; return false; }
        }
        [HarmonyPatch(typeof(InstancedLODBatcher), nameof(InstancedLODBatcher.Update))]
        internal class Patch_Disable_LOD2 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(InstancedLODBatcher), nameof(InstancedLODBatcher.ResetAll))]
        internal class Patch_Disable_LOD3 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(InstancedLODBatcher), "Start")]
        internal class Patch_Disable_LOD4 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(InstancedLODBatcher), "Init")]
        internal class Patch_Disable_LOD5 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(InstancedLODBatch), "Init")]
        internal class Patch_Disable_LOD6 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(InstancedLODBatcher), "EnableAll")]
        internal class Patch_Disable_LOD7 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(InstancedLODBatcher), "EnableInstanced")]
        internal class Patch_Disable_LOD8 { static bool Prefix() { return false; } }

        // animation
        [HarmonyPatch(typeof(UnitAnimator), "Awake")]
        internal class Patch_Disable_Animator_Unit1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(UnitAnimator), nameof(UnitAnimator.OnAnimatorUpdate))]
        internal class Patch_Disable_Animator_Unit2 { static bool Prefix() { return false; } }

        // structure UI not necessary
        [HarmonyPatch(typeof(StructureTaskUI), "OnEnable")]
        internal class Patch_Disable_UI_StructureTask1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(StructureTaskUI), "Awake")]
        internal class Patch_Disable_UI_StructureTask2 { static bool Prefix() { return false; } }

        // audio not necessary
        [HarmonyPatch(typeof(AudioEffectHandler), "OnEnable")]
        internal class Patch_Disable_Audio_EffectHandler1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(AudioEffectHandler), "OrderedUpdate")]
        internal class Patch_Disable_Audio_EffectHandler2 { static bool Prefix() { return false; } }

        // camera not needed
        [HarmonyPatch(typeof(CameraCentric), "LateUpdate")]
        internal class Patch_Disable_Camera1 { static bool Prefix() { return false; } }

        // disable game controls and then ignore user input
        [HarmonyPatch(typeof(Game), nameof(Game.GetControlsEnabled))]
        internal class Patch_Disable_Input_Controls { static bool Prefix(bool __result) { __result = false; return false; } }
        [HarmonyPatch(typeof(GameInputAsset), nameof(GameInputAsset.Initialize))]
        internal class Patch_Disable_Input_Keyboard1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(ActionMapContainer.ActionContainer), nameof(ActionMapContainer.ActionContainer.Process))]
        internal class Patch_Disable_Input_Keyboard2 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(ActionMapContainer), nameof(ActionMapContainer.ProcessInput))]
        internal class Patch_Disable_Input_Keyboard3 { static bool Prefix() { return false; } }

        // no jiggling of bones necessary on the server
        [HarmonyPatch(typeof(JiggleBone), "Awake")]
        internal class Patch_Disable_Jiggling1 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(JiggleBone), "OnEnable")]
        internal class Patch_Disable_Jiggling2 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(JiggleBone), "Init")]
        internal class Patch_Disable_Jiggling3 { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(JiggleBone), "OrderedLateUpdate")]
        internal class Patch_Disable_Jiggling4 { static bool Prefix() { return false; } }

        // voting minus the UI
        [HarmonyPatch(typeof(VoteSystem), "OnEnable")]
        internal class Patch_Modify_VoteSystem1
        {
            static bool Prefix(VoteSystem __instance)
            {
                // skip all menu and UI related code
                MethodInfo playerLeftCallback = AccessTools.Method(typeof(VoteSystem), "OnPlayerLeft");

                Action<Player> playerLeftAction = (Action<Player>)Delegate.CreateDelegate
                    (typeof(Action<Player>), __instance, playerLeftCallback);

                GameEvents.OnPlayerLeft += playerLeftAction;
                return false;
            }
        }
        [HarmonyPatch(typeof(VoteSystem), "OnDisable")]
        internal class Patch_Modify_VoteSystem2
        {
            static bool Prefix(VoteSystem __instance)
            {
                // skip all menu and UI related code
                MethodInfo playerLeftCallback = AccessTools.Method(typeof(VoteSystem), "OnPlayerLeft");

                Action<Player> playerLeftAction = (Action<Player>)Delegate.CreateDelegate
                    (typeof(Action<Player>), __instance, playerLeftCallback);

                GameEvents.OnPlayerLeft -= playerLeftAction;
                return false;
            }
        }

        // time scale minus the UI
        [HarmonyPatch(typeof(TimeManager), "OnEnable")]
        internal class Patch_Modify_TimeManager1
        {
            static bool Prefix(TimeManager __instance)
            {
                // skip all menu and UI related code
                MethodInfo gameSpeedChangedCallback = AccessTools.Method(typeof(TimeManager), "OnGameSpeedChanged");

                Action changedSpeedAction = (Action)Delegate.CreateDelegate
                    (typeof(Action), __instance, gameSpeedChangedCallback);

                TimeManager.GameSpeedChanged += changedSpeedAction;
                return false;
            }
        }
        [HarmonyPatch(typeof(TimeManager), "OnDisable")]
        internal class Patch_Modify_TimeManager2
        {
            static bool Prefix(TimeManager __instance)
            {
                // skip all menu and UI related code
                MethodInfo gameSpeedChangedCallback = AccessTools.Method(typeof(TimeManager), "OnGameSpeedChanged");

                Action changedSpeedAction = (Action)Delegate.CreateDelegate
                    (typeof(Action), __instance, gameSpeedChangedCallback);
                
                TimeManager.GameSpeedChanged -= changedSpeedAction;
                return false;
            }
        }

        // text mesh pro not needed
        [HarmonyPatch(typeof(TMPro.TextMeshProUGUI), "Awake")]
        internal class Patch_Disable_TextMeshProUGUI { static bool Prefix() { return false; } }

        // TODO: everything that looks at Game.CurrentCameraPosition

        // review GameManager.Update for anything not relevant to servers
        [HarmonyPatch(typeof(GameInput), nameof(GameInput.ProcessInput))]
        internal class Patch_Disable_GameManager_ProcessInput { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(GameInput), nameof(GameInput.GetButtonDown))]
        internal class Patch_Disable_GameManager_Input_GetButtonDown { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(Game), nameof(Game.UpdateFading))]
        internal class Patch_Disable_GameManager_UpdateFading { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(DebugConsole), nameof(DebugConsole.HandleToggling))]
        internal class Patch_Disable_GameManager_DebugHandleToggling { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(GameInput), nameof(GameInput.UpdateInput))]
        internal class Patch_Disable_GameManager_UpdateInput { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(Game), nameof(Game.UpdateCameraFOV))]
        internal class Patch_Disable_GameManager_UpdateCameraFOV { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(VisionModeObject), nameof(VisionModeObject.UpdateNewlyAddedObjects))]
        internal class Patch_Disable_GameManager_VisionUpdateNewlyAddedObjects { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(CinematicCamera), nameof(CinematicCamera.Update))]
        internal class Patch_Disable_GameManager_CinematicCameraUpdate { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(Unit), nameof(Unit.UpdateRigidBodyInterpolationAll))]
        internal class Patch_Disable_GameManager_UnitUpdateRigidBodyInterpolationAll { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(VehicleParticleFX), nameof(VehicleParticleFX.TickVehicleParticleFX))]
        internal class Patch_Disable_GameManager_TickVehicleParticleFX { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(DisableBasedOnDistance), nameof(DisableBasedOnDistance.TickDisableBasedOnDistance))]
        internal class Patch_Disable_GameManager_DistanceCullingTick { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(AudioAmbientSource), nameof(AudioAmbientSource.UpdateAmbients))]
        internal class Patch_Disable_GameManager_AudioUpdateAmbients { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(CursorWaiting), nameof(CursorWaiting.UpdateShow))]
        internal class Patch_Disable_GameManager_CursorWaitingUpdateShow { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(LightGrid), nameof(LightGrid.Update))]
        internal class Patch_Disable_GameManager_LightGridUpdate { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(MenuManager), "Awake")]
        internal class Patch_Disable_GameManager_MenuManagerAwake { static bool Prefix() { return false; } }
        // TODO: can skip everything after LightGrid.Awake() in GameManager::Update for dedicated servers via transpiler patch

        // fixing GameManager::Update cascades and needs these fix to avoid exceptions during launch
        [HarmonyPatch(typeof(ScreenPauseHint), "Update")]
        internal class Patch_Disable_GameManager_ScreenPauseHintUpdate { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(GizmoManager), "OnEnable")]
        internal class Patch_Disable_GameManager_GizmoManager_OnEnable { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(GizmoManager), "OnDisable")]
        internal class Patch_Disable_GameManager_GizmoManager_OnDisable { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(BlueprintEditor), "OnEnable")]
        internal class Patch_Disable_GameManager_BlueprintEditor_OnEnable { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(BlueprintEditor), "OnDisable")]
        internal class Patch_Disable_GameManager_BlueprintEditor_OnDisable { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(BlueprintSpawner), "OnEnable")]
        internal class Patch_Disable_GameManager_BlueprintSpawner_OnEnable { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(BlueprintSpawner), "OnDisable")]
        internal class Patch_Disable_GameManager_BlueprintSpawner_OnDisable { static bool Prefix() { return false; } }
        [HarmonyPatch(typeof(FPSCommanding), "Start")]
        internal class Patch_Modify_FPSCommandingStart
        {
            static bool Prefix(FPSCommanding __instance)
            {
                // skip all menu and UI related code
                MethodInfo fpsOnPlayerChangedTeamCallback = AccessTools.Method(typeof(FPSCommanding), "OnPlayerChangedTeam");
                MethodInfo fpsOnPlayerChangedUnitCallback = AccessTools.Method(typeof(FPSCommanding), "OnPlayerChangedUnit");

                Action<Player, Unit, Unit> changedUnitCallback = (Action<Player, Unit, Unit>)Delegate.CreateDelegate
                    (typeof(Action<Player, Unit, Unit>), __instance, fpsOnPlayerChangedUnitCallback);

                Action<Player, Team, Team> changedTeamCallback = (Action<Player, Team, Team>)Delegate.CreateDelegate
                    (typeof(Action<Player, Team, Team>), __instance, fpsOnPlayerChangedTeamCallback);

                GameEvents.OnPlayerChangedTeam += changedTeamCallback;
                GameEvents.OnPlayerChangedUnit += changedUnitCallback;

                FieldInfo networkComponentField = AccessTools.Field(typeof(FPSCommanding), "networkComponent");
                networkComponentField.SetValue(__instance, __instance.gameObject.GetComponent<NetworkComponent>());

                return false;
            }
        }
        [HarmonyPatch(typeof(FPSCommanding), "OnDestroy")]
        internal class Patch_Modify_FPSCommandingOnDestroy
        {
            static bool Prefix(FPSCommanding __instance)
            {
                // skip all menu and UI related code
                MethodInfo fpsOnPlayerChangedTeamCallback = AccessTools.Method(typeof(FPSCommanding), "OnPlayerChangedTeam");
                MethodInfo fpsOnPlayerChangedUnitCallback = AccessTools.Method(typeof(FPSCommanding), "OnPlayerChangedUnit");

                Action<Player, Unit, Unit> changedUnitCallback = (Action<Player, Unit, Unit>)Delegate.CreateDelegate
                    (typeof(Action<Player, Unit, Unit>), __instance, fpsOnPlayerChangedUnitCallback);

                Action<Player, Team, Team> changedTeamCallback = (Action<Player, Team, Team>)Delegate.CreateDelegate
                    (typeof(Action<Player, Team, Team>), __instance, fpsOnPlayerChangedTeamCallback);

                GameEvents.OnPlayerChangedTeam -= changedTeamCallback;
                GameEvents.OnPlayerChangedUnit -= changedUnitCallback;

                return false;
            }
        }
    }
}