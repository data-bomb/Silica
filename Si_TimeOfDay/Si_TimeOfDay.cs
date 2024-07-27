/*
Silica Time of Day Mod
Copyright (C) 2024 by databomb

* Description *
Sets the default time of day when rounds begin to the local server
time instead of the default hard-coded value of 11:00 and exposes 
several configuration options for the day/night cycle as well as
an admin command to force the time to a given value.

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

using SilicaAdminMod;
using HarmonyLib;
using MelonLoader;
using System;
using Si_TimeOfDay;
using System.Text;

[assembly: MelonInfo(typeof(ServerTime), "Time of Day", "0.9.9", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_TimeOfDay
{
    public class ServerTime : MelonMod
    {
        // 11am is the hard-coded start time in the game for the beginning of each round
        const float startTime = 11f;

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<float> Pref_DayTime_Ratio = null!;
        static MelonPreferences_Entry<int> Pref_DayTime_Length = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            Pref_DayTime_Ratio ??= _modCategory.CreateEntry<float>("TimeOfDay_DayLight_Ratio", 0.75f);
            Pref_DayTime_Length ??= _modCategory.CreateEntry<int>("TimeOfDay_LengthOfDay_InRealMinutes", 120);
        }

        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback setTimeCallback = Command_SetTime;
            HelperMethods.RegisterAdminCommand("time", setTimeCallback, Power.Cheat, "Sets the current game time. Usage: !time [0.0 - 23.99]");
        }

        public static void Command_SetTime(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand("Current time is " + WorldLight.GetDayTime().ToString());
                return;
            }

            // validate argument contents
            if (!float.TryParse(args.Split(' ')[1], out float time))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Ambiguous time value provided");
                return;
            }

            WorldLight.SetDayTime(time);
            HelperMethods.AlertAdminAction(callerPlayer, "adjusted time to " + time.ToString());
        }

        [HarmonyPatch(typeof(WorldLight), nameof(WorldLight.OnSendNetInit))]
        private static class TimeOfDay_Patch_WordLight_OnSendNetInit
        {
            public static void Postfix(WorldLight __instance, GameByteStreamWriter __0)
            {
                try
                {
                    WorldLight.CurrentWorldLight.DayFraction = Pref_DayTime_Ratio.Value;
                    float dayLength = (float)Pref_DayTime_Length.Value / 60f;
                    WorldLight.CurrentWorldLight.DayLength = dayLength;

                    //MelonLogger.Msg("Updated WorldLight values.");
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run WorldLight::OnSendNetInit");
                }
            }
        }

        [HarmonyPatch(typeof(WorldLight), nameof(WorldLight.SetDayTime))]
        private static class TimeOfDay_Patch_WorldLight_SetDayTime
        {
            public static void Prefix(ref float __0)
            {
                try
                {
                    //MelonLogger.Msg("Entering SetDayTime with: " + __0.ToString());
                    // are we "equal" to 11am?
                    if (Math.Abs(__0 - startTime) < 0.1)
                    {
                        // grab current hour
                        float currentTime = (float)DateTime.Now.Hour;
                        __0 = currentTime;
                        //MelonLogger.Msg("Overriding time to: " + __0.ToString());
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run WorldLight::SetDayTime");
                }
            }
        }
    }
}