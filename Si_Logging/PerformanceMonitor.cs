/*
 Silica Logging Mod
 Copyright (C) 2023-2024 by databomb
 
 * Description *
 For Silica servers, creates a log file with console replication
 in the Half-Life log standard format.

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

using MelonLoader;
using SilicaAdminMod;
using System.Collections.Generic;
using System;
using UnityEngine;
using HarmonyLib;

namespace Si_Logging
{
    public class ServerPerfLogger
    {
        public static float Timer_PerfMonitorLog = HelperMethods.Timer_Inactive;

        #if NET6_0
        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.Update))]
        #else
        [HarmonyPatch(typeof(MusicJukeboxHandler), "Update")]
        #endif
        private static class ApplyPatch_MusicJukeboxHandler_Update
        {
            private static void Postfix(MusicJukeboxHandler __instance)
            {
                try
                {
                    // check if timer expired while the game is in-progress
                    Timer_PerfMonitorLog += Time.deltaTime;
                    if (Timer_PerfMonitorLog >= HL_Logging.Pref_Log_PerfMonitor_Interval.Value)
                    {
                        Timer_PerfMonitorLog = 0f;

                        // skip if there are no players on the server
                        if (Player.Players.Count <= 0)
                        {
                            return;
                        }

                        CapturePerformancePoint();
                    }
                }
                catch (Exception exception)
                {
                    HelperMethods.PrintError(exception, "Failed in MusicJukeboxHandler::Update");
                }
            }
        }

        public static void CapturePerformancePoint()
        {

        }
    }
}