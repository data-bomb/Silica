/*
 Silica Friendly-Fire Adjustments Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, adjust the amount of friendly fire damage 
 based on the unit and damage type.

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

using UnityEngine;
using MelonLoader;
using Il2Cpp;
using HarmonyLib;
using Si_FriendlyFireLimits;
using static MelonLoader.MelonLogger;
using System.Diagnostics;

[assembly: MelonInfo(typeof(FriendlyFireLimits), "Friendly Fire Limits", "1.1.5", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_FriendlyFireLimits
{
    public class FriendlyFireLimits : MelonMod
    {
        public static void PrintError(Exception exception, string? message = null)
        {
            if (message != null)
            {
                MelonLogger.Msg(message);
            }
            string error = exception.Message;
            error += "\n" + exception.TargetSite;
            error += "\n" + exception.StackTrace;
            Exception? inner = exception.InnerException;
            if (inner != null)
            {
                error += "\n" + inner.Message;
                error += "\n" + inner.TargetSite;
                error += "\n" + inner.StackTrace;
            }
            MelonLogger.Error(error);
        }

        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<float> _UnitOnUnitNonExplosionDamageMultipler;
        static MelonPreferences_Entry<float> _UnitOnUnitExplosionDamageMultiplier;
        static MelonPreferences_Entry<float> _UnitOnStructureExplosionDamageMultiplier;
        static MelonPreferences_Entry<float> _UnitOnStructureNonExplosionDamageMultiplier;
        static MelonPreferences_Entry<bool> _HarvesterPassthrough;


        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory(ModCategory);
            }
            if (_UnitOnUnitNonExplosionDamageMultipler == null)
            {
                _UnitOnUnitNonExplosionDamageMultipler = _modCategory.CreateEntry<float>("FriendlyFire_UnitAttacked_DamageMultiplier", 0.05f);
            }
            if (_UnitOnUnitExplosionDamageMultiplier == null)
            {
                _UnitOnUnitExplosionDamageMultiplier = _modCategory.CreateEntry<float>("FriendlyFire_UnitAttacked_DamageMultiplier_Exp", 0.8f);
            }
            if (_UnitOnStructureExplosionDamageMultiplier == null)
            {
                _UnitOnStructureExplosionDamageMultiplier = _modCategory.CreateEntry<float>("FriendlyFire_StructureAttacked_DamageMultiplier_Exp", 0.65f);
            }
            if (_UnitOnStructureNonExplosionDamageMultiplier == null)
            {
                _UnitOnStructureNonExplosionDamageMultiplier = _modCategory.CreateEntry<float>("FriendlyFire_StructureAttacked_DamageMultiplier_NonExp", 0.15f);
            }
            if (_HarvesterPassthrough == null)
            {
                _HarvesterPassthrough = _modCategory.CreateEntry<bool>("FriendlyFire_Passthrough_Harvester_Damage", true);
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.GameByteStreamReader), nameof(Il2Cpp.GameByteStreamReader.GetGameByteStreamReader))]
        static class GetGameByteStreamReaderPrePatch
        {
            public static void Prefix(Il2Cpp.GameByteStreamReader __result, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> __0, int __1, bool __2)
            {
                try
                {
                    // byte[0] = (2) Byte
                    // byte[1] = ENetworkPacketType
                    Il2Cpp.ENetworkPacketType packetType = (Il2Cpp.ENetworkPacketType)__0[1];
                    if (packetType == Il2Cpp.ENetworkPacketType.ObjectReceiveDamage)
                    {
                        // byte[2] = (8) PackedUInt32
                        // byte[3:4] = NetID
                        uint victimNetID;
                        if (__0[3] >= 241)
                        {
                            victimNetID = (__0[3] * (uint)0x100 - (uint)0xf010) + __0[4];
                        }
                        else
                        {
                            victimNetID = __0[3];
                        }

                        Il2Cpp.NetworkComponent victimNetComp = Il2Cpp.NetworkComponent.GetNetObject(victimNetID);

                        uint attackerNetID;
                        if (__0[21] >= 241)
                        {
                            attackerNetID = (__0[21] * (uint)0x100 - (uint)0xf010) + __0[22];
                        }
                        else
                        {
                            attackerNetID = __0[21];
                        }

                        Il2Cpp.NetworkComponent attackerNetComp = Il2Cpp.NetworkComponent.GetNetObject(attackerNetID);

                        // byte[5] = (8) PackedUInt32
                        // byte[6:7] = colliderIndex
                        uint colliderIndex;
                        if (__0[6] >= 241)
                        {
                            colliderIndex = (__0[6] * (uint)0x100 - (uint)0xf010) + __0[7];
                        }
                        else
                        {
                            colliderIndex = __0[6];
                        }

                        if (victimNetComp != null && attackerNetComp != null && colliderIndex >= 1)
                        {
                            Il2Cpp.BaseGameObject victimBase = victimNetComp.Owner;
                            Il2Cpp.BaseGameObject attackerBase = attackerNetComp.Owner;

                            if (victimBase == null || attackerBase == null)
                            {
                                return;
                            }

                            Il2Cpp.Team victimTeam = victimBase.Team;
                            Il2Cpp.Team attackerTeam = attackerBase.Team;

                            // if they'rea on the same team but allow fall damage
                            if (victimTeam == attackerTeam && victimBase != attackerBase)
                            {
                                // Victim Object Type
                                Il2Cpp.ObjectInfoType victimType = victimBase.ObjectInfo.ObjectType;
                                // Attacker Object Type
                                Il2Cpp.ObjectInfoType attackerType = attackerBase.ObjectInfo.ObjectType;

                                Il2Cpp.EDamageType damagetype = (Il2Cpp.EDamageType)__0[13];
                                float damage = BitConverter.ToSingle(__0, 8);

                                // block units attacking friendly units
                                if (victimType == Il2Cpp.ObjectInfoType.Unit && attackerType == Il2Cpp.ObjectInfoType.Unit)
                                {
                                    // check if we should skip harvester damage
                                    if (_HarvesterPassthrough.Value && victimBase.ObjectInfo.UnitType == Il2Cpp.UnitType.Harvester)
                                    {
                                        return;
                                    }

                                    // AoE does more damage (by default)
                                    byte[] modifiedDamage;
                                    if (damagetype != Il2Cpp.EDamageType.Explosion)
                                    {
                                        modifiedDamage = BitConverter.GetBytes(damage * _UnitOnUnitExplosionDamageMultiplier.Value);
                                    }
                                    else
                                    {
                                        modifiedDamage = BitConverter.GetBytes(damage * _UnitOnUnitNonExplosionDamageMultipler.Value);
                                    }

                                    for (int i = 0; i < modifiedDamage.Length; i++)
                                    {
                                        __0[8 + i] = modifiedDamage[i];
                                    }

                                    return;
                                }

                                // reduce damage of units attacking friendly structures
                                if (victimType == Il2Cpp.ObjectInfoType.Structure && attackerType == Il2Cpp.ObjectInfoType.Unit)
                                {
                                    // AoE goes through with more damage (by default)
                                    byte[] modifiedDamage;
                                    if (damagetype == Il2Cpp.EDamageType.Explosion)
                                    {
                                        modifiedDamage = BitConverter.GetBytes(damage * _UnitOnStructureExplosionDamageMultiplier.Value);
                                    }
                                    else
                                    {
                                        modifiedDamage = BitConverter.GetBytes(damage * _UnitOnStructureNonExplosionDamageMultiplier.Value);
                                    }

                                    for (int i = 0; i < modifiedDamage.Length; i++)
                                    {
                                        __0[8 + i] = modifiedDamage[i];
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run GameByteStreamReader::GetGameByteStreamReader");
                }
            }
        }

        // this applies to the host
        [HarmonyPatch(typeof(Il2Cpp.DamageManager), nameof(Il2Cpp.DamageManager.ApplyDamage))]
        static class ApplyPatchApplyDamage
        {
            public static bool Prefix(Il2Cpp.DamageManager __instance, ref float __result, UnityEngine.Collider __0, float __1, Il2Cpp.EDamageType __2, UnityEngine.GameObject __3, UnityEngine.Vector3 __4)
            {
                try
                {
                    // Victim Team
                    Il2Cpp.BaseGameObject victimBase = __instance.Owner;
                    Il2Cpp.Team victimTeam = __instance.Team;
                    // Attacker Team
                    Il2Cpp.BaseGameObject attackerBase = Il2Cpp.GameFuncs.GetBaseGameObject(__3);
                    Il2Cpp.Team attackerTeam = attackerBase.Team;

                    // if they'rea on the same team but allow fall damage
                    if (victimTeam == attackerTeam && victimBase != attackerBase)
                    {
                        // Victim Object Type
                        Il2Cpp.ObjectInfoType victimType = victimBase.ObjectInfo.ObjectType;
                        // Attacker Object Type
                        Il2Cpp.ObjectInfoType attackerType = attackerBase.ObjectInfo.ObjectType;

                        // block units attacking friendly units
                        if (victimType == Il2Cpp.ObjectInfoType.Unit && attackerType == Il2Cpp.ObjectInfoType.Unit)
                        {
                            // but don't block AoE and don't block if victim is a harvester
                            if (__2 != Il2Cpp.EDamageType.Explosion && victimBase.ObjectInfo.UnitType != Il2Cpp.UnitType.Harvester)
                            {
                                __result = __1 * _UnitOnUnitNonExplosionDamageMultipler.Value;
                                return false;
                            }
                        }

                        // reduce damage of units attacking friendly structures
                        if (victimType == Il2Cpp.ObjectInfoType.Structure && attackerType == Il2Cpp.ObjectInfoType.Unit)
                        {
                            // AoE goes through with more damage
                            if (__2 == Il2Cpp.EDamageType.Explosion)
                            {
                                __result = __1 * _UnitOnStructureExplosionDamageMultiplier.Value;
                            }
                            else
                            {
                                __result = __1 * _UnitOnStructureNonExplosionDamageMultiplier.Value;
                            }

                            return true;
                        }
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run DamageManager::ApplyDamage");
                }

                return true;
            }
        }
    }
}