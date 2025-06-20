﻿/*
Silica Repair Facility
Copyright (C) 2024-2025 by databomb

* Description *
Allows vehicles to repair themselves at a friendly Light Vehicle
Factory and allows modifying the default heal rates in the game.

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

using HarmonyLib;
using MelonLoader;
using SilicaAdminMod;
using System;
using System.Linq;
using UnityEngine;

using Si_RepairFacility;
using System.Collections.Generic;
using System.Text;

[assembly: MelonInfo(typeof(RepairFacility), "Repair Facility", "1.2.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_RepairFacility
{
    public class RepairFacility : MelonMod
    {
        static List<Unit> vehiclesAtRepairShop = null!;
        static float Timer_HealVehicles = 0f;
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<float> _Pref_Humans_Vehicle_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Humans_Aircraft_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Aliens_SmallUnit_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Aliens_MediumUnit_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Aliens_LargeUnit_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Aliens_Structure_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Aliens_Queen_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Humans_Infantry_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_SiegeDefenders_Structure_HealRate = null!;
        static MelonPreferences_Entry<bool> _Pref_Repair_Notification = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Pref_Humans_Vehicle_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_HumanVehicle_HealRate", 0.035f);
            _Pref_Humans_Aircraft_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_HumanAircraft_HealRate", 0.015f);
            _Pref_Humans_Infantry_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_HumanInfantry_HealRate", 0.015f);

            _Pref_Aliens_SmallUnit_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_Alien_SmallUnit_HealRate", 0.02f);
            _Pref_Aliens_MediumUnit_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_Alien_MediumUnit_HealRate", 0.012f);
            _Pref_Aliens_LargeUnit_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_Alien_LargeUnit_HealRate", 0.01f);
            _Pref_Aliens_Queen_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_Alien_Queen_HealRate", 0.01f);

            _Pref_Aliens_Structure_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_Alien_Structure_HealRate", 0.01f);
            _Pref_SiegeDefenders_Structure_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_SiegeDefender_Structure_HealRate", 0.025f);

            _Pref_Repair_Notification ??= _modCategory.CreateEntry<bool>("RepairFacility_ChatNotifications", false);

            vehiclesAtRepairShop = new List<Unit>();
        }

        public override void OnUpdate()
        {
            try
            {
                Timer_HealVehicles += Time.deltaTime;
                if (Timer_HealVehicles > 5.0f)
                {
                    Timer_HealVehicles = 0.0f;

                    CleanRepairList();

                    // repair vehicles for all gamemodes
                    foreach (Unit vehicle in vehiclesAtRepairShop)
                    {
                        float healAmount = vehicle.DamageManager.MaxHealth * (vehicle.IsFlyingType ? _Pref_Humans_Aircraft_HealRate.Value : _Pref_Humans_Vehicle_HealRate.Value);

                        if (vehicle.DamageManager.Health == vehicle.DamageManager.MaxHealth)
                        {
                            if (_Pref_Repair_Notification.Value && vehicle.ControlledBy != null)
                            {
                                //HelperMethods.SendChatMessageToPlayer(vehicle.ControlledBy, HelperMethods.chatPrefix, " Debug Info: (Skipping) Health[" + vehicle.DamageManager.Health + "] MaxHP[" + vehicle.DamageManager.MaxHealth + "] HealAmt[" + healAmount + "]");
                            }

                            continue;
                        }

                        float newHealthUnclamped = vehicle.DamageManager.Health + healAmount;

                        if (newHealthUnclamped >= vehicle.DamageManager.MaxHealth)
                        {
                            vehicle.DamageManager.SetHealth01(1f);

                            if (_Pref_Repair_Notification.Value && vehicle.ControlledBy != null)
                            {
                                //HelperMethods.SendChatMessageToPlayer(vehicle.ControlledBy, HelperMethods.chatPrefix, " Debug Info: (Max) Health[" + vehicle.DamageManager.Health + "] MaxHP[" + vehicle.DamageManager.MaxHealth + "] HealAmt[" + healAmount + "]");
                            }
                        }
                        else
                        {
                            float newHealth = Mathf.Clamp(newHealthUnclamped, 0.0f, vehicle.DamageManager.MaxHealth);

                            vehicle.DamageManager.SetHealth(newHealth);

                            if (_Pref_Repair_Notification.Value && vehicle.ControlledBy != null)
                            {
                                //HelperMethods.SendChatMessageToPlayer(vehicle.ControlledBy, HelperMethods.chatPrefix, " Debug Info: (Incremental) Health[" + vehicle.DamageManager.Health + "] MaxHP[" + vehicle.DamageManager.MaxHealth + "] HealAmt[" + healAmount + "]");
                            }
                        }
                    }

                    // repair defending structures for Siege
                    if (GameMode.CurrentGameMode is MP_TowerDefense)
                    {
                        foreach (Structure structure in Team.Teams[(int)SiConstants.ETeam.Sol].Structures)
                        {
                            if (!structure.DamageManager || structure.DamageManager.IsDestroyed)
                            {
                                continue;
                            }

                            float healAmount = structure.DamageManager.MaxHealth * _Pref_SiegeDefenders_Structure_HealRate.Value;

                            if (structure.DamageManager.Health == structure.DamageManager.MaxHealth)
                            {
                                continue;
                            }

                            float newHealthUnclamped = structure.DamageManager.Health + healAmount;

                            if (newHealthUnclamped >= structure.DamageManager.MaxHealth)
                            {
                                structure.DamageManager.SetHealth01(1f);
                            }
                            else
                            {
                                float newHealth = Mathf.Clamp(newHealthUnclamped, 0.0f, structure.DamageManager.MaxHealth);

                                structure.DamageManager.SetHealth(newHealth);
                            }
                        }    
                    }
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnUpdate");
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(OpenableBase), nameof(OpenableBase.OnUnitEnterZone))]
        #else
        [HarmonyPatch(typeof(OpenableBase), "OnUnitEnterZone")]
        #endif
        private static class RepairFacility_Patch_OpenableBase_OnUnitEnterZone
        {
            public static void Postfix(OpenableBase __instance, Zone __0, Unit __1)
            {
                try
                {
                    if (__instance == null || __instance.NetworkComponent == null || __1 == null ||
                        __instance.NetworkComponent.Owner == null || __instance.NetworkComponent.Owner.Team == null)
                    {
                        return;
                    }

                    // avoid repairing units from enemy base
                    if (__instance.NetworkComponent.Owner.Team != __1.Team)
                    {
                        return;
                    }

                    // is the unit a player-controlled vehicle?
                    if (__1.DriverCompartment == null || __1.NetworkComponent == null || __1.ControlledBy == null)
                    {
                        return;
                    }

                    // there are two openablebases in a LightVehicleFactory:
                    // HousingUnit07_MainDoor and LVF_EntryDoor_Part01
                    if (!__instance.NetworkComponent.ToString().StartsWith("LightVehicleF"))
                    {
                        return;
                    }

                    MelonLogger.Msg("Found player's " + (__1.IsFlyingType ? "aircraft" : "vehicle") + " entering LVF repair zone: " + __1.ControlledBy.PlayerName + " with vehicle " + __1.ToString());

                    vehiclesAtRepairShop.Add(__1);

                    if (_Pref_Repair_Notification.Value)
                    {
                        HelperMethods.SendChatMessageToPlayer(__1.ControlledBy, HelperMethods.chatPrefix, " Entered vehicle repair zone.");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OpenableBase::OnUnitEnterZone");
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(OpenableBase), nameof(OpenableBase.OnUnitExitZone))]
        #else
        [HarmonyPatch(typeof(OpenableBase), "OnUnitExitZone")]
        #endif
        private static class RepairFacility_Patch_OpenableBase_OnUnitExitZone
        {
            public static void Postfix(OpenableBase __instance, Zone __0, Unit __1)
            {
                try
                {
                    if (__instance == null || __instance.NetworkComponent == null || __1 == null ||
                        __instance.NetworkComponent.Owner == null || __instance.NetworkComponent.Owner.Team == null)
                    {
                        return;
                    }

                    // avoid repairing units from enemy base
                    if (__instance.NetworkComponent.Owner.Team != __1.Team)
                    {
                        return;
                    }

                    // there are two openablebases in a LightVehicleFactory:
                    // HousingUnit07_MainDoor and LVF_EntryDoor_Part01
                    if (!__instance.NetworkComponent.ToString().StartsWith("LightVehicleF"))
                    {
                        return;
                    }

                    // is the unit a player-controlled vehicle?
                    if (__1.DriverCompartment == null || __1.NetworkComponent == null || __1.ControlledBy == null)
                    {
                        if (vehiclesAtRepairShop.Contains(__1))
                        {
                            MelonLogger.Msg("Found " + (__1.IsFlyingType ? "aircraft" : "vehicle") + " exiting LVF repair zone: " + __1.ToString());
                        }
                    }
                    else
                    {
                        MelonLogger.Msg("Found player's " + (__1.IsFlyingType ? "aircraft" : "vehicle") + " exiting LVF repair zone: " + __1.ControlledBy.PlayerName + " with vehicle " + __1.ToString());
                    }
                    
                    vehiclesAtRepairShop.RemoveAll(vehicle => vehicle == __1);

                    if (_Pref_Repair_Notification.Value)
                    {
                        HelperMethods.SendChatMessageToPlayer(__1.ControlledBy, HelperMethods.chatPrefix, " Left vehicle repair zone.");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OpenableBase::OnUnitExitZone");
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(AutoHeal), nameof(AutoHeal.OnEnable))]
        #else
        [HarmonyPatch(typeof(AutoHeal), "OnEnable")]
        #endif
        private static class RepairFacility_Patch_AutoHeal_OnEnable
        {
            public static void Postfix(AutoHeal __instance)
            {
                if (__instance == null || __instance.Data == null)
                {
                    return;
                }

                string healDataType = __instance.Data.name;
                healDataType = healDataType.Substring(healDataType.LastIndexOf("_") + 1);

                switch (healDataType)
                {
                    case "Infantry":
                        __instance.Data.HealAmountPct = _Pref_Humans_Infantry_HealRate.Value;
                        break;
                    case "AlienSmall":
                        __instance.Data.HealAmountPct = _Pref_Aliens_SmallUnit_HealRate.Value;
                        break;
                    case "AlienStructure":
                        __instance.Data.HealAmountPct = _Pref_Aliens_Structure_HealRate.Value;
                        break;
                    case "AlienMedium":
                        __instance.Data.HealAmountPct = _Pref_Aliens_MediumUnit_HealRate.Value;
                        break;
                    case "AlienLarge":
                        __instance.Data.HealAmountPct = _Pref_Aliens_LargeUnit_HealRate.Value;
                        break;
                    case "Queen":
                        __instance.Data.HealAmountPct = _Pref_Aliens_Queen_HealRate.Value;
                        break;
                    default:
                        MelonLogger.Warning("Received unknown AutoHealData type: " + __instance.name);
                        __instance.Data.HealAmountPct = _Pref_Aliens_LargeUnit_HealRate.Value;
                        break;
                }
            }
        }

        public static void CleanRepairList()
        {
            // remove any null elements first
            if (vehiclesAtRepairShop.RemoveAll(vehicle => vehicle == null) > 0)
            {
                MelonLogger.Warning("Removed null element(s) from Repair List");
            }

            // remove anything that has been destroyed
            if (vehiclesAtRepairShop.RemoveAll(vehicle => vehicle.DamageManager.IsDestroyed) > 0)
            {
                MelonLogger.Warning("Removed destroyed units from Repair List");
            }

            // and anything that has a null network component
            if (vehiclesAtRepairShop.RemoveAll(vehicle => vehicle.NetworkComponent == null) > 0)
            {
                MelonLogger.Warning("Removed units with null network components from Repair List");
            }
        }
    }
}