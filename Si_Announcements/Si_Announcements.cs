/*
 Silica Announcements Mod
 Copyright (C) 2024 by databomb
 
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
using System.Timers;
using System;
using System.IO;
using System.Collections.Generic;
using SilicaAdminMod;
using System.Linq;

[assembly: MelonInfo(typeof(Announcements), "Server Announcements", "1.1.5", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Announcements
{
    public class Announcements : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<int> _Announcements_SecondsBetweenMessages = null!;
        static MelonPreferences_Entry<bool> _Announcements_ShowIfLastChatWasAnnouncement = null!;

        static Timer announcementTimer = null!;
        static int announcementCount;
        static string[]? announcementsText;
        static string? lastChatMessage;
        static bool timerExpired;

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
                    // Create simple announcements.txt file
                    using FileStream fs = File.Create(announcementsFile);
                    fs.Close();
                    System.IO.File.WriteAllText(announcementsFile, "<color=#cc33ff>Server mods by databomb. Report issues in Discord or on GitHub.</color>\n");
                }

                // Open the stream and read it back
                using (StreamReader announcementsFileStream = File.OpenText(announcementsFile))
                {
                    List<string> announcementFileLine = new List<string>();
                    string? announcement = "";
                    while ((announcement = announcementsFileStream.ReadLine()) != null)
                    {
                        // ignore a line with only LF/CR
                        if (announcement.Length > 2)
                        {
                            announcementFileLine.Add(announcement);
                        }
                    }
                    announcementsText = announcementFileLine.ToArray();
                }

                MelonLogger.Msg("Loaded announcements.txt file with " + announcementsText.Length + " announcements");

                double interval = _Announcements_SecondsBetweenMessages.Value * 1000.0f;
                announcementTimer = new System.Timers.Timer(interval);
                announcementTimer.Elapsed += new ElapsedEventHandler(TimerCallbackAnnouncement);
                announcementTimer.AutoReset = true;
                announcementTimer.Enabled = true;

            }
            catch (Exception exception)
            {
                HelperMethods.PrintError(exception, "Failed in OnInitializeMelon");
            }
        }

        #if NET6_0
        public override void OnLateInitializeMelon()
        {
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
        }
        #endif

        private static void TimerCallbackAnnouncement(object? source, ElapsedEventArgs e)
        {
            timerExpired = true;
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
                    // check if timer expired while the game is in-progress
                    if (GameMode.CurrentGameMode.GameOngoing == true && timerExpired == true)
                    {
                        timerExpired = false;

                        if (_Announcements_ShowIfLastChatWasAnnouncement != null && !_Announcements_ShowIfLastChatWasAnnouncement.Value && lastChatMessage != null && announcementsText != null)
                        {
                            // check if the last chat message was an announcement
                            bool lastMessageWasAnnouncement = announcementsText.Any(m => m == lastChatMessage);
                            if (lastMessageWasAnnouncement)
                            {
                                MelonLogger.Msg("Skipping Announcement - Repeated Message");
                                return;
                            }
                        }
                        
                        announcementCount++;

                        if (announcementsText == null)
                        {
                            return;
                        }

                        String thisAnnouncement = announcementsText[announcementCount % announcementsText.Length];
                        MelonLogger.Msg("Announcement: " + thisAnnouncement);

                        Player broadcastPlayer = HelperMethods.FindBroadcastPlayer();
                        broadcastPlayer.SendChatMessage(thisAnnouncement);
                    }
                }
                catch (Exception exception)
                {
                    HelperMethods.PrintError(exception, "Failed in MusicJukeboxHandler::Update");
                }
            }
        }

        #if NET6_0
        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        #else
        [HarmonyPatch(typeof(Silica.UI.Chat), "MessageReceived")]
        #endif
        private static class Announcements_Patch_Chat_MessageReceived
        {
            #if NET6_0
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Player __0, string __1, bool __2)
            #else
            public static void Postfix(Silica.UI.Chat __instance, Player __0, string __1, bool __2)
            #endif
            {
                try
                {
                    if (_Announcements_ShowIfLastChatWasAnnouncement != null && _Announcements_ShowIfLastChatWasAnnouncement.Value)
                    {
                        return;
                    }

                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (__instance.ToString().Contains("alien") && __2 == false)
                    {
                        lastChatMessage = __1;
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }
    }
}