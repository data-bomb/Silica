/*
 Silica Announcements Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, periodically sends a pre-set announcement
 in the game's chat.

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
using MelonLoader.Utils;
using Si_Announcements;
using UnityEngine;
using AdminExtension;
using System.Timers;
using Il2CppSystem.Runtime.Remoting.Messaging;

[assembly: MelonInfo(typeof(Announcements), "Server Announcements", "1.0.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]


namespace Si_Announcements
{
    public class Announcements : MelonMod
    {
        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<int> _Announcements_SecondsBetweenMessages;

        static System.Timers.Timer announcementTimer;
        static int announcementCount;
        static string[] announcementsText;
        static bool timerExpired;

        public override void OnInitializeMelon()
        {
            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory("Silica");
            }
            if (_Announcements_SecondsBetweenMessages == null)
            {
                // default of 7 minutes between announcements
                _Announcements_SecondsBetweenMessages = _modCategory.CreateEntry<int>("Announcements_SecondsBetweenMessages", 420);
            }

            String announcementsFile = MelonEnvironment.UserDataDirectory + "\\announcements.txt";

            try
            {
                if (!File.Exists(announcementsFile))
                {
                    // Create simple announcements.txt file
                    using (FileStream fs = File.Create(announcementsFile))
                    {
                        fs.Close();
                        System.IO.File.WriteAllText(announcementsFile, "Join discord at..\n");
                    }
                }

                // Open the stream and read it back
                using (StreamReader announcementsFileStream = File.OpenText(announcementsFile))
                {
                    List<string> announcementFileLine = new List<string>();
                    string announcement = "";
                    while ((announcement = announcementsFileStream.ReadLine()) != null)
                    {
                        announcementFileLine.Add(announcement);
                    }
                    announcementsText = announcementFileLine.ToArray();
                }

                double interval = _Announcements_SecondsBetweenMessages.Value * 1000.0f;
                announcementTimer = new System.Timers.Timer(interval);
                announcementTimer.Elapsed += new ElapsedEventHandler(timerCallbackAnnouncement);
                announcementTimer.AutoReset = true;
                announcementTimer.Enabled = true;

            }
            catch (Exception exception)
            {
                HelperMethods.PrintError(exception, "Failed in OnInitializeMelon");
            }
        }

        private static void timerCallbackAnnouncement(object source, ElapsedEventArgs e)
        {
            timerExpired = true;
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.Update))]
        private static class ApplyPatch_MusicJukeboxHandlerUpdate
        {
            private static void Postfix(Il2Cpp.MusicJukeboxHandler __instance)
            {
                try
                {
                    // check if timer expired while the game is in-progress
                    if (Il2Cpp.GameMode.CurrentGameMode.GameOngoing == true && timerExpired == true)
                    {
                        timerExpired = false;
                        announcementCount++;

                        String thisAnnouncement = announcementsText[announcementCount % announcementsText.Length];
                        MelonLogger.Msg("Announcement: " + thisAnnouncement);

                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                        Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, thisAnnouncement, false);
                    }
                }
                catch (Exception exception)
                {
                    HelperMethods.PrintError(exception, "Failed in MusicJukeboxHandler::Update");
                }
            }
        }
    }
}