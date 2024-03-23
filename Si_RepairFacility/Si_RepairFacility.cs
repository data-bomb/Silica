/*
Silica End Round
Copyright (C) 2024 by databomb

* Description *
Allows vehicles to repair themselves at a friendly Light Vehicle
Factory.

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

[assembly: MelonInfo(typeof(RepairFacility), "Repair Facility", "0.9.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_RepairFacility
{
    public class RepairFacility : MelonMod
    {
        static List<DamageManager> vehiclesAtRepairShop = null!;
        static float Timer_HealVehicles = 0f;
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<float> _Pref_Humans_Vehicle_HealRate = null!;
        static MelonPreferences_Entry<float> _Pref_Aliens_HealRate = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Pref_Humans_Vehicle_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_HumanVehicle_HealRate", 0.035f);
            _Pref_Aliens_HealRate ??= _modCategory.CreateEntry<float>("RepairFacility_Alien_HealRate", 0.015f);

            vehiclesAtRepairShop = new List<DamageManager>();
        }

#if NET6_0
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.Update))]
#else
        [HarmonyPatch(typeof(MusicJukeboxHandler), "Update")]
#endif
        private static class ApplyPatch_MusicJukeboxHandlerUpdate
        {
            private static void Postfix(MusicJukeboxHandler __instance)
            {
                try
                {
                    Timer_HealVehicles += Time.deltaTime;
                    if (Timer_HealVehicles > 5.0f)
                    {
                        Timer_HealVehicles = 0.0f;

                        foreach (DamageManager vehicleDamageManager in vehiclesAtRepairShop)
                        {
                            if (vehicleDamageManager == null || vehicleDamageManager.IsDestroyed || vehicleDamageManager.NetworkComponent == null)
                            {
                                // TODO: Add these to a list to remove them from the repair shop
                                continue;
                            }

                            float healAmount = vehicleDamageManager.MaxHealth * _Pref_Humans_Vehicle_HealRate.Value;
                            float newHealth = Mathf.Clamp(vehicleDamageManager.Health + healAmount, 0.0f, vehicleDamageManager.MaxHealth);
                            vehicleDamageManager.SetHealth(newHealth);
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::Update");
                }
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

                    MelonLogger.Msg("Found player's vehicle entering LVF repair zone: " + __1.ControlledBy.PlayerName + " with vehicle " + __1.ToString());

                    vehiclesAtRepairShop.Add(__1.DamageManager);
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

                    MelonLogger.Msg("Found player's vehicle exiting LVF repair zone: " + __1.ControlledBy.PlayerName + " with vehicle " + __1.ToString());

                    vehiclesAtRepairShop.RemoveAll(vehicleDM => vehicleDM == __1.DamageManager);
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
                __instance.HealAmountPct = _Pref_Aliens_HealRate.Value;
            }
        }
    }
}