/*
Silica Admin Mod
Copyright (C) 2025 by databomb

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
    public static class Event_Structures
    {
        public static event EventHandler<OnRequestDestroyStructureArgs> OnRequestDestroyStructure = delegate { };
        public static event EventHandler<OnCommanderDestroyedStructureArgs> OnCommanderDestroyedStructure = delegate { };

        #if NET6_0
        [HarmonyPatch(typeof(StrategyMode), nameof(StrategyMode.RPC_DestroyStructure))]
        #else
        [HarmonyPatch(typeof(StrategyMode), "RPC_DestroyStructure")]
        #endif
        static class ApplyPatch_StrategyMode_RPC_DestroyStructure
        {
            public static bool Prefix(StrategyMode __instance, Structure __0)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return true;
                    }

                    // only broadcast valid events (e.g., structure not already destroyed)
                    if (__0.IsDestroyed)
                    {
                        return true;
                    }

                    OnRequestDestroyStructureArgs onRequestDestroyStructureArgs = FireOnRequestDestroyStructureEvent(__0, __0.Team);

                    if (onRequestDestroyStructureArgs.Block)
                    {
                        if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                        {
                            MelonLogger.Msg("Blocking structure (" + __0.name + ") from being destroyed on team " + __0.Team.TeamShortName);
                        }

                        return false;
                    }

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Allowing structure (" + __0.name + ") to be destroyed on team " + __0.Team.TeamShortName);
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run StrategyMode::RPC_DestroyStructure(Prefix)");
                }

                return true;
            }

            public static void Postfix(StrategyMode __instance, Structure __0)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return;
                    }

                    OnCommanderDestroyedStructureArgs onCommanderDestroyedStructureArgs = FireOnCommanderDestroyedStructure(__0, __0.Team);

                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Structure (" + __0.name + ") destroyed by commander on team " + __0.Team.TeamShortName);
                    }

                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run StrategyMode::RPC_DestroyStructure(Postfix)");
                }
            }
        }

        public static OnRequestDestroyStructureArgs FireOnRequestDestroyStructureEvent(Structure structure, Team team)
        {
            OnRequestDestroyStructureArgs onRequestDestroyStructureArgs = new OnRequestDestroyStructureArgs();
            onRequestDestroyStructureArgs.Structure = structure;
            onRequestDestroyStructureArgs.Team = team;
            EventHandler<OnRequestDestroyStructureArgs> requestDestroyStructureEvent = OnRequestDestroyStructure;
            if (requestDestroyStructureEvent != null)
            {
                requestDestroyStructureEvent(null, onRequestDestroyStructureArgs);
            }

            return onRequestDestroyStructureArgs;
        }

        public static OnCommanderDestroyedStructureArgs FireOnCommanderDestroyedStructure(Structure structure, Team team)
        {
            OnCommanderDestroyedStructureArgs onCommanderDestroyedStructureArgs = new OnCommanderDestroyedStructureArgs();
            onCommanderDestroyedStructureArgs.Structure = structure;
            onCommanderDestroyedStructureArgs.Team = team;
            EventHandler<OnCommanderDestroyedStructureArgs> commanderDestroyedStructureEvent = OnCommanderDestroyedStructure;
            if (commanderDestroyedStructureEvent != null)
            {
                commanderDestroyedStructureEvent(null, onCommanderDestroyedStructureArgs);
            }

            return onCommanderDestroyedStructureArgs;
        }
    }
}
