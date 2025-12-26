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
using MelonLoader;
using UnityEngine;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;

#if NET6_0
using Il2Cpp;
#else
using Silica;
#endif

namespace SilicaAdminMod
{
    public static class Event_Damage
    {
        public static event EventHandler<OnPreDamageReceivedArgs> OnPrePlayerDamageReceived = delegate { };

        [HarmonyPatch(typeof(DamageManager), nameof(DamageManager.OnReceiveClientDamageHitPacket))]
        static class ApplyPatch_DamageManager_OnReceiveClientDamageHitPacket
        {
            static float Detour_BeforeOnDamageReceived(DamageManager damageManager, float damage, GameObject instigator, byte hitAngle)
            {
                OnPreDamageReceivedArgs onPreDamageReceivedArgs = FireOnPrePlayerDamageReceivedEvent(damageManager, damage, instigator, hitAngle);

                if (onPreDamageReceivedArgs.Damage != damage)
                {
                    if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                    {
                        MelonLogger.Msg("Adjusting damage of (" + damageManager.Owner.ObjectInfo.DisplayName + ") from (" + damage + ") to (" + onPreDamageReceivedArgs.Damage + ")");
                    }

                    return onPreDamageReceivedArgs.Damage;
                }

                return damage;
            }

            static int FindEntryPoint_DamageManager_OnReceiveClientDamageHitPacket(List<CodeInstruction> opCodes)
            {
                // find the last instance of calling OnDamageReceived method
                var methodOnDamageReceived = AccessTools.Method(typeof(DamageManager), "OnDamageReceived");
                if (methodOnDamageReceived == null)
                {
                    MelonLogger.Warning("Transpiler failure of DamageManager::OnReceiveClientDamageHitPacket. Cannot locate OnDamageReceived method. Damage events will not function correctly.");
                    return -1;
                }

                int lastOnDamageReceivedCall = -1;
                for (int i = 0; i < opCodes.Count; i++)
                {
                    if (opCodes[i].Calls(methodOnDamageReceived))
                    {
                        lastOnDamageReceivedCall = i;
                    }
                }
                
                MelonLogger.Msg("(DamageManager::OnReceiveClientDamageHitPacket Transpiler) Found last call of OnDamageReceived at opCode[" + lastOnDamageReceivedCall + "]");
                
                // if we couldn't find any calls then the game was updated in an unexpected way
                if (lastOnDamageReceivedCall < 0)
                {
                    MelonLogger.Warning("Transpiler failure of DamageManager::OnReceiveClientDamageHitPacket. Cannot locate OnDamageReceived call. Damage events will not function correctly.");
                    return -1;
                }
                
                // scan upwards to find the moment before the health decrement
                int insertionPoint = lastOnDamageReceivedCall;
                for (int i = lastOnDamageReceivedCall; i >= 0; i--)
                {
                    if (opCodes[i].opcode == OpCodes.Ret) // return before "this.Health -= num;"
                    {
                        insertionPoint = i + 1; // insert on the "this.Health -+ num;" line
                        break;
                    }
                }
                
                // if we're at 0 then the game was updated in an unexpected way
                if (insertionPoint == 0)
                {
                    MelonLogger.Warning("Transpiler failure of DamageManager::OnReceiveClientDamageHitPacket. Cannot locate ret. Damage events will not function correctly.");
                    return -1;
                }

                MelonLogger.Msg("(DamageManager::OnReceiveClientDamageHitPacket Transpiler) Found insertion point at opCode[" + insertionPoint + "]");

                return insertionPoint;
            }

            static List<CodeInstruction> GenerateILPatch_DamageManager_OnReceiveClientDamageHitPacket(ILGenerator generator)
            {
                var detourBeforeOnDamageReceived = AccessTools.Method(typeof(ApplyPatch_DamageManager_OnReceiveClientDamageHitPacket), nameof(ApplyPatch_DamageManager_OnReceiveClientDamageHitPacket.Detour_BeforeOnDamageReceived));

                return new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_0),                            // DamageManager this
                    new CodeInstruction(OpCodes.Ldloc_0),                            // float damage
                    new CodeInstruction(OpCodes.Ldloc_3),                            // GameObject instigator
                    new CodeInstruction(OpCodes.Ldloc_1),                            // byte hitAngle
                    new CodeInstruction(OpCodes.Call, detourBeforeOnDamageReceived), // call detour method
                    new CodeInstruction(OpCodes.Stloc_0),                            // update the damage value
                }; // continue to decrement health
            }

            // an alternative approach would be needed for il2cpp
            #if !NET6_0
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var opCodes = instructions.ToList();
                int insertionPoint = FindEntryPoint_DamageManager_OnReceiveClientDamageHitPacket(opCodes);

                // don't make any modifications without confidence in insertionPoint
                if (insertionPoint < 0)
                {
                    return instructions;
                }

                // generate the IL we need to insert before the last RequestConstructionSite call
                var inlineOpCodes = GenerateILPatch_DamageManager_OnReceiveClientDamageHitPacket(generator);

                // adjust labels to point to new code
                inlineOpCodes[0].labels.AddRange(opCodes[insertionPoint].labels);
                opCodes[insertionPoint].labels.Clear();

                // insert code before the stack adjustments start for RequestConstructionSite
                opCodes.InsertRange(insertionPoint, inlineOpCodes);

                return opCodes.AsEnumerable();
            }
            #endif
        }
        
        public static OnPreDamageReceivedArgs FireOnPrePlayerDamageReceivedEvent(DamageManager damageManager, float damage, GameObject instigator, byte hitAngle)
        {
            OnPreDamageReceivedArgs onPreDamageReceivedArgs = new OnPreDamageReceivedArgs();
            onPreDamageReceivedArgs.DamageManager = damageManager;
            onPreDamageReceivedArgs.Damage = damage;
            onPreDamageReceivedArgs.Instigator = instigator;
            onPreDamageReceivedArgs.HitAngle = hitAngle;
            EventHandler<OnPreDamageReceivedArgs> preDamageReceivedEvent = OnPrePlayerDamageReceived;
            if (preDamageReceivedEvent != null)
            {
                preDamageReceivedEvent(null, onPreDamageReceivedArgs);
            }

            return onPreDamageReceivedArgs;
        }
    }
}
