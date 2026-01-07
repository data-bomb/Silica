/*
Silica Admin Mod
Copyright (C) 2024-2025 by databomb

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

using HarmonyLib;
using System;
using MelonLoader;
using System.Reflection;

#if NET6_0
using Il2Cpp;
#endif

namespace SilicaAdminMod
{
    public static class Event_Units
    {
        public static event EventHandler<OnRequestEnterUnitArgs> OnRequestEnterUnit = delegate { };
        public static event EventHandler<OnRequestInviteToGroupArgs> OnRequestInviteToGroup = delegate { };

        // Aliens will go through OnUse for OnRequestEnterUnit
        [HarmonyPatch(typeof(UseAlienTakeOver), nameof(UseAlienTakeOver.OnUse))]
        static class ApplyPatch_UseAlienTakeOver_OnUse
        {
            public static bool Prefix(UseAlienTakeOver __instance, Unit __0)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return true;
                    }

                    // only broadcast player-controlled events with valid units
                    if (__0.ControlledBy == null || __instance.OwnerUnit == null)
                    {
                        return true;
                    }

                    OnRequestEnterUnitArgs onRequestEnterUnitArgs = FireOnRequestEnterUnitEvent(__0.ControlledBy, __instance.OwnerUnit, true);

                    if (onRequestEnterUnitArgs.Block)
                    {
                        if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                        {
                            MelonLogger.Msg("Blocking player " + __0.ControlledBy.PlayerName + " from entering alien " + __instance.OwnerUnit.ToString());
                        }

                        return false;
                    }

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Allowing player to enter alien");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run UseAlienTakeOver::OnUse");
                }

                return true;
            }
        }

        // Commanders will go through GetCanSwitchToUnit
        [HarmonyPatch(typeof(Player), nameof(Player.GetCanSwitchToUnit))]
        static class ApplyPatch_Player_GetCanSwitchToUnit
        {
            public static bool Prefix(Player __instance, Unit __0)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return true;
                    }

                    // only interested in commanders
                    if (!__instance.IsCommander)
                    {
                        return true;
                    }

                    OnRequestEnterUnitArgs onRequestEnterUnitArgs = FireOnRequestEnterUnitEvent(__instance, __0, true);

                    if (onRequestEnterUnitArgs.Block)
                    {
                        // kick the commander out of this unit, this will put the commander into a freecam mode
                        NetworkLayer.SendPlayerSelectUnit(__instance, null);

                        if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                        {
                            MelonLogger.Msg("Blocking player " + __instance.PlayerName + " from entering unit " + __0.ToString());
                        }

                        // take commander out of freecam
                        HelperMethods.SetCommander(__instance.Team, null);
                        HelperMethods.SetCommander(__instance.Team, __instance);

                        return false;
                    }

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Allowing player to direct control unit");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run NetworkLayer::SendPlayerSelectUnit");
                }

                return true;
            }
        }

        // Humans will go through AddUnit for OnRequestEnterUnit
        #if NET6_0
        [HarmonyPatch(typeof(UnitCompartment), nameof(UnitCompartment.AddUnit))]
        #else
        [HarmonyPatch(typeof(UnitCompartment), "AddUnit")]
        #endif
        static class ApplyPatch_UnitCompartment_AddUnit
        {
            public static bool Prefix(UnitCompartment __instance, Unit __0)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return true;
                    }

                    // only broadcast player-controlled events with valid units
                    if (__0.ControlledBy == null || __instance.OwnerUnit == null)
                    {
                        return true;
                    }

                    OnRequestEnterUnitArgs onRequestEnterUnitArgs = FireOnRequestEnterUnitEvent(__0.ControlledBy, __instance.OwnerUnit, __instance.IsDriver);

                    if (onRequestEnterUnitArgs.Block)
                    {
                        if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                        {
                            MelonLogger.Msg("Blocking player " + __0.ControlledBy.PlayerName + " from entering unit " + __instance.OwnerUnit.ToString());
                        }

                        return false;
                    }

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Allowing player to enter unit's compartment");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run UnitCompartment::AddUnit");
                }

                return true;
            }
        }

        public static OnRequestEnterUnitArgs FireOnRequestEnterUnitEvent(Player player, Unit unit, bool asDriver)
        {
            OnRequestEnterUnitArgs onRequestEnterUnitArgs = new OnRequestEnterUnitArgs();
            onRequestEnterUnitArgs.Player = player;
            onRequestEnterUnitArgs.Unit = unit;
            onRequestEnterUnitArgs.AsDriver = asDriver;
            EventHandler<OnRequestEnterUnitArgs> requestEnterUnitEvent = OnRequestEnterUnit;
            if (requestEnterUnitEvent != null)
            {
                requestEnterUnitEvent(null, onRequestEnterUnitArgs);
            }

            return onRequestEnterUnitArgs;
        }

        // OnRequestInviteToGroup
        #if NET6_0
        [HarmonyPatch(typeof(FPSCommanding), nameof(FPSCommanding.InviteToGroupServer))]
        #else
        [HarmonyPatch(typeof(FPSCommanding), "InviteToGroupServer")]
        #endif
        static class ApplyPatch_FPSCommanding_InviteToGroupServer
        {
            public static bool Prefix(FPSCommanding __instance, Player __0, Target __1)
            {
                try
                {
                    if (__1 == null || __0 == null || !__1.OwnerUnit || __0.Group == null || __0.Group.UnitCount >= __instance.GroupUnitLimit)
                    {
                        return false;
                    }

                    OnRequestInviteToGroupArgs onRequestInviteToGroupArgs = FireOnRequestInviteToGroupEvent(__0, __1);

                    if (onRequestInviteToGroupArgs.Block)
                    {
                        if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                        {
                            MelonLogger.Msg("Blocking player " + __0.PlayerName + " from inviting target to group " + __1.ObjectInfo.DisplayName);
                        }

                        return false;
                    }

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Allowing player to invite target to join FPS commanding group");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run FPSCommanding::InviteToGroupServer");
                }

                return true;
            }
        }

        public static OnRequestInviteToGroupArgs FireOnRequestInviteToGroupEvent(Player player, Target target)
        {
            OnRequestInviteToGroupArgs onRequestInviteToGroupArgs = new OnRequestInviteToGroupArgs();
            onRequestInviteToGroupArgs.Player = player;
            onRequestInviteToGroupArgs.Target = target;
            EventHandler<OnRequestInviteToGroupArgs> requestInviteToGroupEvent = OnRequestInviteToGroup;
            if (requestInviteToGroupEvent != null)
            {
                requestInviteToGroupEvent(null, onRequestInviteToGroupArgs);
            }

            return onRequestInviteToGroupArgs;
        }
    }
}
