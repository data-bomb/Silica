/*
 Silica Mapcycle Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, automatically generates a mapcycle.txt file
 and switches to the next map in the cycle at the end of each round.

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

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_Mapcycle;
using UnityEngine;
using System.Timers;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(MapCycleMod), "[Si] Mapcycle", "1.0.2", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_Mapcycle
{
    public class MapCycleMod : MelonMod
    {
        static String mapName = "";
        static GameMode gameModeInstance;
        static bool bEndRound;
        static bool bTimerExpired;
        static int iMapLoadCount;
        static string[] sMapCycle;

        private static System.Timers.Timer DelayTimer;

        public override void OnInitializeMelon()
        {
            String mapCycleFile = MelonEnvironment.UserDataDirectory + "\\mapcycle.txt";

            try
            {
                if (File.Exists(mapCycleFile))
                {
                    // Open the stream and read it back.
                    using (StreamReader mapFileStream = File.OpenText(mapCycleFile))
                    {
                        List<string> sMapList = new List<string>();
                        string sMap = "";
                        while ((sMap = mapFileStream.ReadLine()) != null)
                        {
                            sMapList.Add(sMap);
                        }
                        sMapCycle = sMapList.ToArray();
                    }
                }
                else
                {
                    // Create simple mapcycle.txt file
                    using (FileStream fs = File.Create(mapCycleFile))
                    {
                        fs.Close();
                        System.IO.File.WriteAllText(mapCycleFile, "RiftBasin\nGreatErg\nBadlands\nNarakaCity\n");
                    }
                }

            }
            catch (Exception exception)
            {
                MelonLogger.Msg(exception.ToString());
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            mapName = sceneName;
        }
        public const string defaultColor = "<color=#DDE98C>";
        public const string chatPrefix = "<b>" + defaultColor + "[<color=#DFA725>SAM" + defaultColor + "]</b> ";

        //TODO change to use the admin helper methods instead
        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class ApplyChatReceiveCurrentMatchInfo
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    if (__instance.ToString().Contains("alien") && __2 == false)
                    {

                        bool isCurrMapCommand = String.Equals(__1, "!currentmap", StringComparison.OrdinalIgnoreCase);
                        if (isCurrMapCommand)
                        {
                            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                            serverPlayer.SendChatMessage(chatPrefix + defaultColor + " Current map is " + String.Concat(mapName), false);

                        }
                    }
                }
                catch (Exception exception)
                {
                    string error = exception.Message;
                    error += "\n" + exception.TargetSite;
                    error += "\n" + exception.StackTrace;
                    MelonLogger.Error(error);
                }
            }
        }
    private static void HandleTimerChangeLevel(object source, ElapsedEventArgs e)
        {
            MapCycleMod.bTimerExpired = true;
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.Update))]
        private static class ApplyPatch_MusicJukeboxHandlerUpdate
        {
            private static void Postfix(Il2Cpp.MusicJukeboxHandler __instance)
            {
                // check if timer expired
                if (MapCycleMod.bEndRound == true && MapCycleMod.bTimerExpired == true)
                {
                    MapCycleMod.bEndRound = false;

                    MapCycleMod.iMapLoadCount++;

                    String sNextMap = sMapCycle[iMapLoadCount % sMapCycle.Length];

                    String sCurrentMap = Il2Cpp.NetworkGameServer.GetServerMapName();
                    MelonLogger.Msg("Changing map to " + sNextMap + "...");

                    Il2Cpp.NetworkGameServer.LoadLevel(sNextMap, MapCycleMod.gameModeInstance.GameModeInfo);
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_OnGameEnded
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0, Il2Cpp.Team __1)
            {
                MapCycleMod.gameModeInstance = __0;
                MapCycleMod.bEndRound = true;
                MapCycleMod.bTimerExpired = false;

                double interval = 20000.0;
                MapCycleMod.DelayTimer = new System.Timers.Timer(interval);
                MapCycleMod.DelayTimer.Elapsed += new ElapsedEventHandler(MapCycleMod.HandleTimerChangeLevel);
                MapCycleMod.DelayTimer.AutoReset = false;
                MapCycleMod.DelayTimer.Enabled = true;
            }
        }
    }
}
