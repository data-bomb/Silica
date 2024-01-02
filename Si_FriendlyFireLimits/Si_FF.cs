/*
 Silica Friendly-Fire Adjustments Mod
 Copyright (C) 2024 by databomb
 
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

#if NET6_0
using Il2Cpp;
#endif

using MelonLoader;
using HarmonyLib;
using Si_FriendlyFireLimits;
using System;
using SilicaAdminMod;

[assembly: MelonInfo(typeof(FriendlyFireLimits), "Friendly Fire Limits", "1.2.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_FriendlyFireLimits
{
    public class FriendlyFireLimits : MelonMod
    {
        #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<float> _UnitOnUnitNonExplosionDamageMultipler;
        static MelonPreferences_Entry<float> _UnitOnUnitExplosionDamageMultiplier;
        static MelonPreferences_Entry<float> _UnitOnStructureExplosionDamageMultiplier;
        static MelonPreferences_Entry<float> _UnitOnStructureNonExplosionDamageMultiplier;
        static MelonPreferences_Entry<bool> _HarvesterPassthrough;
        #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _UnitOnUnitNonExplosionDamageMultipler ??= _modCategory.CreateEntry<float>("FriendlyFire_UnitAttacked_DamageMultiplier", 0.75f);
            _UnitOnUnitExplosionDamageMultiplier ??= _modCategory.CreateEntry<float>("FriendlyFire_UnitAttacked_DamageMultiplier_Exp", 0.85f);
            _UnitOnStructureExplosionDamageMultiplier ??= _modCategory.CreateEntry<float>("FriendlyFire_StructureAttacked_DamageMultiplier_Exp", 0.65f);
            _UnitOnStructureNonExplosionDamageMultiplier ??= _modCategory.CreateEntry<float>("FriendlyFire_StructureAttacked_DamageMultiplier_NonExp", 0.0f);
            _HarvesterPassthrough ??= _modCategory.CreateEntry<bool>("FriendlyFire_Passthrough_Harvester_Damage", true);
        }

        [HarmonyPatch(typeof(GameByteStreamReader), nameof(GameByteStreamReader.GetGameByteStreamReader))]
        static class GetGameByteStreamReaderPrePatch
        {
            #if NET6_0
            public static void Prefix(GameByteStreamReader __result, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> __0, int __1, bool __2)
            #else
            public static void Prefix(GameByteStreamReader __result, byte[] __0, int __1, bool __2)
            #endif
            {
                try
                {
                    // byte[0] = (2) Byte
                    // byte[1] = ENetworkPacketType
                    ENetworkPacketType packetType = (ENetworkPacketType)__0[1];
                    if (packetType == ENetworkPacketType.ObjectReceiveDamage)
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

                        NetworkComponent victimNetComp = NetworkComponent.GetNetObject(victimNetID);

                        uint attackerNetID;
                        if (__0[21] >= 241)
                        {
                            attackerNetID = (__0[21] * (uint)0x100 - (uint)0xf010) + __0[22];
                        }
                        else
                        {
                            attackerNetID = __0[21];
                        }

                        NetworkComponent attackerNetComp = NetworkComponent.GetNetObject(attackerNetID);

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
                            BaseGameObject victimBase = victimNetComp.Owner;
                            BaseGameObject attackerBase = attackerNetComp.Owner;

                            if (victimBase == null || attackerBase == null)
                            {
                                return;
                            }

                            Team victimTeam = victimBase.Team;
                            Team attackerTeam = attackerBase.Team;

                            // if they'rea on the same team but allow fall damage
                            if (victimTeam == attackerTeam && victimBase != attackerBase)
                            {
                                // Victim Object Type
                                ObjectInfoType victimType = victimBase.ObjectInfo.ObjectType;
                                // Attacker Object Type
                                ObjectInfoType attackerType = attackerBase.ObjectInfo.ObjectType;

                                EDamageType damagetype = (EDamageType)__0[13];
                                float damage = BitConverter.ToSingle(__0, 8);

                                // block units attacking friendly units
                                if (victimType == ObjectInfoType.Unit && attackerType == ObjectInfoType.Unit)
                                {
                                    // check if we should skip harvester damage
                                    if (_HarvesterPassthrough.Value && victimBase.ObjectInfo.UnitType == UnitType.Harvester)
                                    {
                                        return;
                                    }

                                    // AoE does more damage (by default)
                                    byte[] modifiedDamage;
                                    if (damagetype != EDamageType.Explosion)
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
                                if (victimType == ObjectInfoType.Structure && attackerType == ObjectInfoType.Unit)
                                {
                                    // AoE goes through with more damage (by default)
                                    byte[] modifiedDamage;
                                    if (damagetype == EDamageType.Explosion)
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
                    HelperMethods.PrintError(error, "Failed to run GameByteStreamReader::GetGameByteStreamReader");
                }
            }
        }

        // this applies to the host
        [HarmonyPatch(typeof(DamageManager), nameof(DamageManager.ApplyDamage))]
        static class ApplyPatchApplyDamage
        {
            public static bool Prefix(DamageManager __instance, ref float __result, UnityEngine.Collider __0, float __1, EDamageType __2, UnityEngine.GameObject __3, UnityEngine.Vector3 __4)
            {
                try
                {
                    // Victim Team
                    BaseGameObject victimBase = __instance.Owner;
                    Team victimTeam = __instance.Team;
                    // Attacker Team
                    BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__3);
                    Team attackerTeam = attackerBase.Team;

                    // if they'rea on the same team but allow fall damage
                    if (victimTeam == attackerTeam && victimBase != attackerBase)
                    {
                        // Victim Object Type
                        ObjectInfoType victimType = victimBase.ObjectInfo.ObjectType;
                        // Attacker Object Type
                        ObjectInfoType attackerType = attackerBase.ObjectInfo.ObjectType;

                        // block units attacking friendly units
                        if (victimType == ObjectInfoType.Unit && attackerType == ObjectInfoType.Unit)
                        {
                            // but don't block AoE and don't block if victim is a harvester
                            if (__2 != EDamageType.Explosion && victimBase.ObjectInfo.UnitType != UnitType.Harvester)
                            {
                                __result = __1 * _UnitOnUnitNonExplosionDamageMultipler.Value;
                                return false;
                            }
                        }

                        // reduce damage of units attacking friendly structures
                        if (victimType == ObjectInfoType.Structure && attackerType == ObjectInfoType.Unit)
                        {
                            // AoE goes through with more damage
                            if (__2 == EDamageType.Explosion)
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
                    HelperMethods.PrintError(error, "Failed to run DamageManager::ApplyDamage");
                }

                return true;
            }
        }
    }
}