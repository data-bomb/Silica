/*
 Silica Announcements Mod
 Copyright (C) 2023-2025 by databomb
 
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

#if NET6_0
using Il2Cpp;
#endif

using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Si_Announcements;
using System;
using System.IO;
using System.Collections.Generic;
using SilicaAdminMod;
using System.Linq;
using UnityEngine;
using static MelonLoader.MelonLogger;

[assembly: MelonInfo(typeof(Announcements), "Server Announcements", "1.1.11", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Announcements
{
    public class Announcements : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> _Announcements_SecondsBetweenMessages = null!;
        static MelonPreferences_Entry<bool> _Announcements_ShowIfLastChatWasAnnouncement = null!;

        static float Timer_Announcement = HelperMethods.Timer_Inactive;
        static int announcementCount;
        static string[]? announcementsText;
        static string? lastChatMessage;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            // default of 7 minutes between announcements
            _Announcements_SecondsBetweenMessages ??= _modCategory.CreateEntry<int>("Announcements_SecondsBetweenMessages", 420);
            _Announcements_ShowIfLastChatWasAnnouncement ??= _modCategory.CreateEntry<bool>("Announcements_ShowAnnouncementWhenLastChatWasAnnouncement", true);

            String announcementsFile = MelonEnvironment.UserDataDirectory + "\\announcements.txt";

            try
            {
                if (!File.Exists(announcementsFile))
                {
                    CreateStarterAnnouncementsFile(announcementsFile);
                }

                // Open the stream and read it back
                using (StreamReader announcementsFileStream = File.OpenText(announcementsFile))
                {
                    List<string> announcementFileLine = new List<string>();
                    string? announcement = "";
                    while ((announcement = announcementsFileStream.ReadLine()) != null)
                    {
                        if (!IsValidAnnouncement(announcement))
                        {
                            continue;
                        }

                        announcementFileLine.Add(announcement);
                    }

                    announcementsText = announcementFileLine.ToArray();
                }

                MelonLogger.Msg("Loaded announcements.txt file with " + announcementsText.Length + " announcements.");
            }
            catch (Exception exception)
            {
                HelperMethods.PrintError(exception, "Failed in OnInitializeMelon");
            }
        }
        public override void OnLateInitializeMelon()
        {
            HelperMethods.StartTimer(ref Timer_Announcement);

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.IntOption secondsBeforeAnnouncing = new(_Announcements_SecondsBetweenMessages, true, _Announcements_SecondsBetweenMessages.Value, 60, 1200, 30);
            QList.OptionTypes.BoolOption showDoubleAnnouncements = new(_Announcements_ShowIfLastChatWasAnnouncement, _Announcements_ShowIfLastChatWasAnnouncement.Value);

            QList.Options.AddOption(secondsBeforeAnnouncing);
            QList.Options.AddOption(showDoubleAnnouncements);
            #endif
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
                    Timer_Announcement += Time.deltaTime;

                    if (Timer_Announcement >= _Announcements_SecondsBetweenMessages.Value)
                    {
                        Timer_Announcement = 0.0f;

                        if (announcementsText == null)
                        {
                            return;
                        }

                        // skip if game is not ongoign
                        if (!GameMode.CurrentGameMode.GameOngoing)
                        {
                            return;
                        }

                        if (!_Announcements_ShowIfLastChatWasAnnouncement.Value)
                        {
                            // check if the last chat message was an announcement
                            if (IsPreviousChatMessageAnnouncement(lastChatMessage))
                            {
                                MelonLogger.Msg("Skipping Announcement - Repeated Message");
                                return;
                            }
                        }

                        string nextAnnouncement = GetNextAnnouncement();
                        HelperMethods.SendChatMessageToAll(nextAnnouncement);
                    }
                }
                catch (Exception exception)
                {
                    HelperMethods.PrintError(exception, "Failed in MusicJukeboxHandler::Update");
                }
            }
        }
        public void OnRequestPlayerChat(object? sender, OnRequestPlayerChatArgs args)
        {
            if (args.Player == null)
            {
                return;
            }

            try
            {
                if (_Announcements_ShowIfLastChatWasAnnouncement != null && _Announcements_ShowIfLastChatWasAnnouncement.Value)
                {
                    return;
                }

                // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                if (args.TeamOnly == false)
                {
                    lastChatMessage = args.Text;
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRequestPlayerChat");
            }
        }

        private static bool IsPreviousChatMessageAnnouncement(string? previousChat)
        {
            if (previousChat == null || announcementsText == null)
            {
                return false;
            }

            return announcementsText.Any(m => m == previousChat);
        }

        private static void CreateStarterAnnouncementsFile(string filePath)
        {
            File.WriteAllText(filePath, "<color=#cc33ff>Server mods by databomb. Report issues in Discord or on GitHub.</color>\n");
        }

        private static string GetNextAnnouncement()
        {
            if (announcementsText == null)
            {
                return "";
            }

            announcementCount++;
            string nextAnnouncement = announcementsText[announcementCount % announcementsText.Length];

            MelonLogger.Msg("Announcement: " + nextAnnouncement);
            return nextAnnouncement;
        }

        private static bool IsValidAnnouncement(string announcementText)
        {
            // ignore a line with only LF/CR
            if (announcementText.Length <= 2)
            {
                return false;
            }

            return true;
        }
    }
}