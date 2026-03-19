/*
 Silica Logging Mod
 Copyright (C) 2023-2025 by databomb
 
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

#if NET6_0
using Il2Cpp;
#endif

using MelonLoader;
using SilicaAdminMod;
using System.Collections.Generic;
using System;
using UnityEngine;
using HarmonyLib;
using MelonLoader.Utils;

namespace Si_Logging
{
    public class ServerPerfLogger
    {
        public class PerfMonitorData
        {
            public int UnixTime
            {
                get;
                set;
            }
            public int ServerFPS
            {
                get;
                set;
            }
            public int Players
            {
                get;
                set;
            }
            public int Structures
            {
                get;
                set;
            }
            public int ConstructionSites
            {
                get;
                set;
            }
            public int NetworkComponents
            {
                get;
                set;
            }
            public int Units
            {
                get;
                set;
            }
            public int LightsOn
            {
                get;
                set;
            }
            public float UploadRate
            {
                get;
                set;
            }
            public float DownloadRate
            {
                get;
                set;
            }

            public PerfMonitorData()
            {
                UnixTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                ServerFPS = Game.FPS;
                Players = (Player.Players != null ? Player.Players.Count : -1);
                Structures = (Structure.Structures != null ? Structure.Structures.Count : -1);
                ConstructionSites = (ConstructionSite.ConstructionSites != null ? ConstructionSite.ConstructionSites.Count : -1);
                NetworkComponents = (NetworkComponent.NetworkComponents != null ? NetworkComponent.NetworkComponents.Count : -1);
                Units = (Unit.Units != null ? Unit.Units.Count : -1);
                LightsOn = GetNumberOfLightsOn();

                UploadRate = (float)Mathf.RoundToInt(NetworkLayer.NetBitsAvgUpload * 0.01f) * 0.1f;
                DownloadRate = (float)Mathf.RoundToInt(NetworkLayer.NetBitsAvgDownload * 0.01f) * 0.1f;
            }
        }


        static readonly string perfMonitorLogFile = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "PerfMonitorData.csv");

        private static int GetNumberOfLightsOn()
        {
            if (Unit.Units == null)
            {
                return -1;
            }

            int numLightsOn = 0;

            foreach (Unit unit in Unit.Units)
            {
                if (unit.UnitLights == null)
                {
                    continue;
                }

                if (unit.UnitLights.LightsOn)
                {
                    numLightsOn++;
                }
            }

            return numLightsOn;
        }

        public static void CapturePerformancePoint()
        {
            if (!System.IO.File.Exists(perfMonitorLogFile))
            {
                AddColumnInfo();
            }

            AddPerformanceEntry();
        }

        public static void AddColumnInfo()
        {
            string columnLine = "Unix Time,Server FPS,Players,Structures,Construction Sites,Network Components,Units,Lights On,Upload Rate,Download Rate";
            System.IO.File.AppendAllText(perfMonitorLogFile, columnLine + Environment.NewLine);
        }

        public static void AddPerformanceEntry()
        {
            PerfMonitorData dataPoint = new PerfMonitorData();
            string performanceEntryLine = $"{dataPoint.UnixTime}," +
                $"{dataPoint.ServerFPS}," +
                $"{dataPoint.Players}," +
                $"{dataPoint.Structures}," +
                $"{dataPoint.ConstructionSites}," +
                $"{dataPoint.NetworkComponents}," +
                $"{dataPoint.Units}," +
                $"{dataPoint.LightsOn}," +
                $"{dataPoint.UploadRate}," +
                $"{dataPoint.DownloadRate}";

            System.IO.File.AppendAllText(perfMonitorLogFile, performanceEntryLine + Environment.NewLine);
        }
    }

    public partial class HL_Logging
    {
        public static float Timer_PerfMonitorLog = HelperMethods.Timer_Inactive;

        public static void TrackPerformanceMonitor()
        {
            // is the feature enabled?
            if (!HL_Logging.Pref_Log_PerfMonitor_Enable.Value)
            {
                return;
            }

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

                ServerPerfLogger.CapturePerformancePoint();
            }
        }
    }
}