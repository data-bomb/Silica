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
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Data;
using System.Collections.Generic;
using System.Reflection.Emit;

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

            // patch right before the call to SetHealth(0f)
            static void Detour_BeforeStructureDestroyed(Structure structure)
            {
                if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                {
                    MelonLogger.Msg("StrategyMode::RPC_DestroyStructure detour hit");
                }

                if (structure == null || structure.Team == null)
                {
                    return;
                }

                OnCommanderDestroyedStructureArgs onCommanderDestroyedStructureArgs = FireOnCommanderDestroyedStructure(structure, structure.Team);

                if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                {
                    MelonLogger.Msg("Structure (" + structure.name + ") destroyed by commander on team " + structure.Team.TeamShortName);
                }
            }

            static int FindEntryPoint_StrategyMode_RPC_DestroyStructure(List<CodeInstruction> opCodes)
            {
                // find the first instance of calling DamageManager.SetHealth01(0f);
                var methodSetHealth = AccessTools.Method(typeof(DamageManager), "SetHealth01");

                int firstSetHealthCall = -1;
                for (int i = 0; i < opCodes.Count; i++)
                {
                    if (opCodes[i].Calls(methodSetHealth))
                    {
                        firstSetHealthCall = i;
                        break;
                    }
                }

                if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                {
                    MelonLogger.Msg("(StrategyMode::RPC_DestroyStructure Transpiler) Found first call of SetHealth01 at opCode[" + firstSetHealthCall + "] with total opCode size: " + opCodes.Count);
                }

                // if we couldn't find any calls then the game was updated in an unexpected way
                if (firstSetHealthCall < 0)
                {
                    MelonLogger.Warning("Transpiler failure of StrategyMode::RPC_DestroyStructure. Cannot locate SetHealth01 call. Structure events will not function correctly.");
                    return -1;
                }

                // scan upwards to the beginning of the call stack for the SetHealth01 method
                int insertionPoint = firstSetHealthCall;
                for (int i = firstSetHealthCall; i >= 0; i--)
                {
                    if (opCodes[i].IsLdarg(1)) // first argument (Structure structure)
                    {
                        insertionPoint = i;
                        break;
                    }
                }

                // if we're at 0 then the game was updated in an unexpected way
                if (insertionPoint == 0)
                {
                    MelonLogger.Warning("Transpiler failure of StrategyMode::RPC_DestroyStructure. Cannot locate ldarg1. Structure events will not function correctly.");
                    return -1;
                }

                if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                {
                    MelonLogger.Msg("(StrategyMode::RPC_DestroyStructure Transpiler) Found insertion point at opCode[" + insertionPoint + "]");
                }

                return insertionPoint;
            }

            static List<CodeInstruction> GenerateILPatch_Structure_Destroy(ILGenerator generator)
            {
                Label skipExit = generator.DefineLabel();
                var detourStructureDestroyed = AccessTools.Method(typeof(ApplyPatch_StrategyMode_RPC_DestroyStructure), nameof(ApplyPatch_StrategyMode_RPC_DestroyStructure.Detour_BeforeStructureDestroyed));

                return new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_1),           // Structure structure
                    new CodeInstruction(OpCodes.Call, detourStructureDestroyed) // call detour method
                }; // continue to call SetHealth01(0f)
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var opCodes = instructions.ToList();
                int insertionPoint = FindEntryPoint_StrategyMode_RPC_DestroyStructure(opCodes);

                // don't make any modifications without confidence in insertionPoint
                if (insertionPoint < 0)
                {
                    return instructions;
                }

                // generate the IL we need to insert before the last RequestConstructionSite call
                var inlineOpCodes = GenerateILPatch_Structure_Destroy(generator);

                // insert code before the stack adjustments start for RequestConstructionSite
                opCodes.InsertRange(insertionPoint, inlineOpCodes);

                return opCodes.AsEnumerable();
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
