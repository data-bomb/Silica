/*
Silica No Unit Limits
Copyright (C) 2024 by databomb

* Description *
Automatically set servers for no unit limits when hosting.

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
#else
using System.Reflection;
#endif

using HarmonyLib;
using MelonLoader;
using Si_NoUnitLimits;
using SilicaAdminMod;
using System;
using Newtonsoft.Json;

[assembly: MelonInfo(typeof(NoUnitLimits), "No Unit Limits", "1.1.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_NoUnitLimits
{
    public class NoUnitLimits : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> _UnitCap = null!;

        public override void OnInitializeMelon()
        {
            _modCategory = MelonPreferences.CreateCategory("Silica");
            _UnitCap = _modCategory.CreateEntry<int>("UnitCap", 0);
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.UpdateUnitCapMultFromSetting))]
        private static class ApplyPatch_Strategy_UpdateUnitCapMultFromSetting
        {
            static void Postfix(MP_Strategy __instance)
            {
                try
                {
                    if (__instance == null || _UnitCap == null)
                    {
                        return;
                    }

                    int unitCap = _UnitCap.Value;
                    if (unitCap < 0)
                    {
                        unitCap = 0;
                    }

#if NET6_0
                    __instance.UnitCapMultiplier = unitCap;
                    #else
                    PropertyInfo unitCapProperty = typeof(MP_Strategy).GetProperty("UnitCapMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
                    unitCapProperty.SetValue(__instance, (int)unitCap);
                    #endif

                    if (unitCap == 0)
                    {
                        MelonLogger.Msg("Setting unit cap to unlimited.");
                    }
                    else
                    {
                        MelonLogger.Msg("Setting unit cap to: " + unitCap);
                    }
                    
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error);
                }
            }
        }
    }
}