/*
Silica Admin Mod
Copyright (C) 2024 by databomb

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
using System.Linq;
using MelonLoader;
using UnityEngine;
using Newtonsoft.Json.Linq;
using MelonLoader.ICSharpCode.SharpZipLib.Core;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Data;

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public static class Event_Units
    {
        public static event EventHandler<OnRequestEnterUnitArgs> OnRequestEnterUnit = delegate { };

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
                        MelonLogger.Msg("Blocking player " + __0.ControlledBy.PlayerName + " from entering unit " + __instance.OwnerUnit.ToString());
                        return false
                    }

                    MelonLogger.Msg("Allowing player to enter unit's compartment");

                    return true;
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
    }
}
