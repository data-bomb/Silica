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

[assembly: MelonInfo(typeof(NoUnitLimits), "No Unit Limits", "1.0.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_NoUnitLimits
{
    public class NoUnitLimits : MelonMod
    {
        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.UpdateUnitCapMultFromSetting))]
        private static class ApplyPatch_Strategy_UpdateUnitCapMultFromSetting
        {
            static void Postfix(MP_Strategy __instance)
            {
                try
                {
                    if (__instance == null)
                    {
                        return;
                    }

                    #if NET6_0
                    __instance.UnitCapMultiplier = 0;
                    #else
                    PropertyInfo unitCapProperty = typeof(MP_Strategy).GetProperty("UnitCapMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
                    unitCapProperty.SetValue(__instance, (int)0);
                    #endif

                    MelonLogger.Msg("Setting unit cap to unlimited.");
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error);
                }
            }
        }
    }
}