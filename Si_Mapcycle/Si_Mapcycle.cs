using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Si_Mapcycle;
using UnityEngine;
using System.Timers;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(MapCycleMod), "[Si] Mapcycle", "1.0.0", "databomb", "https://github.com/data-bomb/Silica_ListenServer")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_Mapcycle
{
    public class MapCycleMod : MelonMod
    {
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

        private static void HandleTimerChangeLevel(object source, ElapsedEventArgs e)
        {
            MelonLogger.Msg("Reached timer callback");
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