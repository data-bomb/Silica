/*
Silica Match Settings Override
Copyright (C) 2024-2025 by databomb

* Description *
Allows overriding default server parameters with MelonPreferences.

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
using Si_MatchSettings;
using SilicaAdminMod;
using System;
using Newtonsoft.Json;

[assembly: MelonInfo(typeof(MatchSettingsOverride), "Match Settings Override", "2.0.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_MatchSettings
{
    public class MatchSettingsOverride : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> _UnitCap = null!;
        static MelonPreferences_Entry<int> _BotCount = null!;
        static MelonPreferences_Entry<int> _Alien_TechMin = null!;
        static MelonPreferences_Entry<int> _Alien_TechMax = null!;
        static MelonPreferences_Entry<int> _Human_TechMin = null!;
        static MelonPreferences_Entry<int> _Human_TechMax = null!;
        static MelonPreferences_Entry<float> _DistanceStart = null!;
        //static MelonPreferences_Entry<float> _ResourceStart = null!;
        static MelonPreferences_Entry<float> _ResourceLocations = null!;
        static MelonPreferences_Entry<float> _HiddenResourceLocations = null!;

        public override void OnInitializeMelon()
        {
            _modCategory = MelonPreferences.CreateCategory("Silica");
            _UnitCap = _modCategory.CreateEntry<int>("UnitCap", 0);
            _BotCount = _modCategory.CreateEntry<int>("BotCount", 16);
            _Alien_TechMin = _modCategory.CreateEntry<int>("TechMin_Alien", 0);
            _Alien_TechMax = _modCategory.CreateEntry<int>("TechMax_Alien", 8);
            _Human_TechMin = _modCategory.CreateEntry<int>("TechMin_Human", 0);
            _Human_TechMax = _modCategory.CreateEntry<int>("TechMax_Human", 8);
            _DistanceStart = _modCategory.CreateEntry<float>("DistanceStart", 0.5f);
            //_ResourceStart = _modCategory.CreateEntry<float>("StartingResources", 1.0f);
            _ResourceLocations = _modCategory.CreateEntry<float>("ResourceLocations", 0.8f);
            _HiddenResourceLocations = _modCategory.CreateEntry<float>("HiddenResourceLocations", 0.5f);
        }

        [HarmonyPatch(typeof(GameModeExt), nameof(GameModeExt.UpdateUnitCapMultFromSetting))]
        private static class ApplyPatch_GameModeExt_UpdateUnitCapMultFromSetting
        {
            static void Postfix(GameModeExt __instance)
            {
                try
                {
                    if (__instance == null)
                    {
                        return;
                    }

                    // "capmult"
                    int unitCap = _UnitCap.Value;
                    if (unitCap < 0)
                    {
                        unitCap = 0;
                    }

                    #if NET6_0
                    __instance.UnitCapMultiplier = unitCap;
                    #else
                    PropertyInfo unitCapProperty = typeof(GameModeExt).GetProperty("UnitCapMultiplier");
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
                    HelperMethods.PrintError(error, "Failed to run GameModeExt::UpdateUnitCapMultFromSetting");
                }
            }
        }

        [HarmonyPatch(typeof(MP_Deathmatch), nameof(MP_Deathmatch.StartGameLoop))]
        private static class ApplyPatch_MP_Deathmatch_StartGameLoop
        {
            static void Postfix(MP_Deathmatch __instance)
            {
                try
                {
                    if (__instance == null)
                    {
                        return;
                    }

                    // "bots"
                    int botCount = _BotCount.Value;
                    if (botCount < 0)
                    {
                        botCount = 0;
                    }

                    #if NET6_0
                    __instance.BotCount = botCount;
                    #else
                    FieldInfo botCountField = typeof(MP_Deathmatch).GetField("BotCount");
                    botCountField.SetValue(__instance, (int)botCount);
                    #endif

                    MelonLogger.Msg("Setting bot count to: " + botCount);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Deathmatch::StartGameLoop");
                }
            }
        }

        [HarmonyPatch(typeof(MP_Strategy), nameof(MP_Strategy.UpdateInitialStrategySettings))]
        private static class ApplyPatch_MP_Strategy_UpdateInitialStrategySettings
        {
            static void Postfix(MP_Strategy __instance)
            {
                try
                {
                    if (__instance == null)
                    {
                        return;
                    }

                    // "techmin" and "techmax"
                    foreach (Team team in Team.Teams)
                    {
                        if (team.Index == (int)SiConstants.ETeam.Alien)
                        {
                            team.TechnologyTierLimitMin = _Alien_TechMin.Value;
                            team.TechnologyTierLimitMax = _Alien_TechMax.Value;

                            MelonLogger.Msg("Setting Alien Tech Min/Max to: " + _Alien_TechMin.Value + "/" + _Alien_TechMax.Value);
                        }
                        else
                        {
                            team.TechnologyTierLimitMin = _Human_TechMin.Value;
                            team.TechnologyTierLimitMax = _Human_TechMax.Value;

                            MelonLogger.Msg("Setting Human Tech Min/Max to: " + _Human_TechMin.Value + "/" + _Human_TechMax.Value);
                        }
                    }

                    // "diststart"
                    float distanceStart = Math.Clamp(_DistanceStart.Value, 0.25f, 1.0f);
                    #if NET6_0
                    __instance.MultStartingDist = distanceStart;
                    #else
                    FieldInfo multStartDistField = typeof(MP_Strategy).GetField("MultStartingDist");
                    multStartDistField.SetValue(__instance, (float)distanceStart);
                    #endif

                    MelonLogger.Msg("Setting MultStartingDist to: " + distanceStart);

                    // "resstart"
                    /*float resourcesStart = Math.Clamp(_ResourceStart.Value, 0.25f, 5.0f);
                    #if NET6_0
                    __instance.MultStartingRes = resourcesStart;
                    #else
                    FieldInfo multStartResField = typeof(MP_Strategy).GetField("MultStartingRes");
                    multStartResField.SetValue(__instance, (float)resourcesStart);
                    #endif

                    MelonLogger.Msg("Setting MultStartingRes to: " + resourcesStart);
                    */

                    // "resloc"
                    float resourceLocations = Math.Clamp(_ResourceLocations.Value, 0.03125f, 4.0f);
                    float hiddenResourceLocations = Math.Clamp(_HiddenResourceLocations.Value, 0.0f, 1.0f);
                    #if NET6_0
                    __instance.MultLocationRes = resourceLocations;
                    __instance.MultLocationResHide = hiddenResourceLocations;
                    #else
                    FieldInfo multLocResField = typeof(MP_Strategy).GetField("MultLocationRes");
                    multLocResField.SetValue(__instance, (float)resourceLocations);
                    FieldInfo multLocResHideField = typeof(MP_Strategy).GetField("MultLocationResHide");
                    multLocResHideField.SetValue(__instance, (float)hiddenResourceLocations);
                    #endif

                    MelonLogger.Msg("Setting MultLocationRes to: " + resourceLocations);
                    MelonLogger.Msg("Setting MultLocationResHide to: " + hiddenResourceLocations);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::UpdateInitialStrategySettings");
                }
            }
        }
    }
}