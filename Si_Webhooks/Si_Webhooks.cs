/*
 Silica Webhooks Mod
 Copyright (C) 2024-2025 by databomb
 
 * Description *
 Adds webhook integrations for Silica servers.

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

using Steamworks;

using HarmonyLib;
using MelonLoader;
using Si_Webhooks;
using SilicaAdminMod;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;
using MelonLoader.Utils;
using System.Linq;

[assembly: MelonInfo(typeof(Webhooks), "Webhooks", "1.4.2", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Webhooks
{
    public class Webhooks : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<string> _Webhooks_URL = null!;
        static MelonPreferences_Entry<string> _Server_Shortname = null!;

        // https://www.itgeared.com/how-to-get-role-id-on-discord/
        static MelonPreferences_Entry<string> _RoleToMentionForReports = null!;

        // https://steamcommunity.com/dev/apikey
        static MelonPreferences_Entry<string> _SteamAPI_Key = null!;

        static MelonPreferences_Entry<string> _Server_Avatar_URL = null!;
        static MelonPreferences_Entry<string> _Default_Avatar_URL = null!;

        static CallResult<HTTPRequestCompleted_t> OnHTTPRequestCompletedCallResultAvatar = null!;
        static CallResult<HTTPRequestCompleted_t> OnHTTPRequestCompletedCallResultDiscord = null!;

        static Dictionary<ulong, string> CacheAvatarURLs = null!;

        static float Timer_CheckForVideo = HelperMethods.Timer_Inactive;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Webhooks_URL ??= _modCategory.CreateEntry<string>("Webhooks_URL", "");
            _Server_Shortname ??= _modCategory.CreateEntry<string>("Webhooks_Server_Shortname", "Server");
            _RoleToMentionForReports ??= _modCategory.CreateEntry<string>("Webhooks_Report_To_Role", "");
            _SteamAPI_Key ??= _modCategory.CreateEntry<string>("Webhooks_Steam_API_Key", "");
            _Server_Avatar_URL ??= _modCategory.CreateEntry<string>("Webhooks_Server_Avatar_URL", "https://cdn.discordapp.com/icons/824561906277810187/d2f40915db72206f36abb46975655434.webp");
            _Default_Avatar_URL ??= _modCategory.CreateEntry<string>("Webhooks_Default_Avatar_URL", "https://cdn.discordapp.com/icons/663449315876012052/9e482a81d84ee8e750d07c8dbe5b78e4.webp");

            OnHTTPRequestCompletedCallResultAvatar = CallResult<HTTPRequestCompleted_t>.Create((CallResult<HTTPRequestCompleted_t>.APIDispatchDelegate)OnHTTPRequestCompletedAvatar);
            OnHTTPRequestCompletedCallResultDiscord = CallResult<HTTPRequestCompleted_t>.Create((CallResult<HTTPRequestCompleted_t>.APIDispatchDelegate)OnHTTPRequestCompletedDiscord);

            CacheAvatarURLs = new Dictionary<ulong, string>();

            try
            {
                //SteamAPI.Init();
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to call SteamAPI.Init");
            }
        }

        public override void OnLateInitializeMelon()
        {
            // subscribe to the OnRequestPlayerChat event
            Event_Chat.OnRequestPlayerChat += OnRequestPlayerChat;
        }

        public void OnRequestPlayerChat(object? sender, OnRequestPlayerChatArgs args)
        {
            if (args.Player == null)
            {
                return;
            }

            try
            {
                string rawMessage = ConvertHTML(args.Text);
                if (rawMessage == string.Empty)
                {
                    return;
                }

                if (rawMessage.StartsWith("**[SAM"))
                {
                    rawMessage = rawMessage.Replace("**[SAM]** ", "");
                    SendMessageToWebhook(rawMessage, _Server_Shortname.Value, _Server_Avatar_URL.Value);
                    return;
                }

                string username = args.Player.PlayerName;
                string? avatarURL = string.Empty;
                // cache the Steam avatar, if it's needed
                if (!CacheAvatarURLs.ContainsKey(args.Player.PlayerID.SteamID.m_SteamID))
                {
                    MelonLogger.Msg("Missing Avatar URL for " + username + ". Grabbing it...");
                    RequestSteamAvatar(args.Player);
                }
                else
                {
                    // use the cached avatar
                    CacheAvatarURLs.TryGetValue(args.Player.PlayerID.SteamID.m_SteamID, out avatarURL);
                }

                // is this a user report?
                int spaceCharacter = rawMessage.IndexOf(" ");
                string commandText = (spaceCharacter == -1) ? rawMessage : rawMessage.Substring(0, spaceCharacter);

                bool isUserReport = String.Equals(commandText, "!report", StringComparison.OrdinalIgnoreCase);
                if (isUserReport)
                {
                    string reportMessage;
                    if (spaceCharacter == -1)
                    {
                        reportMessage = args.Player.PlayerName + " (" + args.Player.PlayerID.ToString() + ") is requesting an admin in the game. <@&" + _RoleToMentionForReports.Value + ">";
                    }
                    else
                    {
                        reportMessage = args.Player.PlayerName + " (" + args.Player.PlayerID.ToString() + ") is requesting an admin in the game. Report:" + rawMessage.Substring(spaceCharacter) + " <@&" + _RoleToMentionForReports.Value + ">";
                    }

                    SendMessageToWebhook(reportMessage, _Server_Shortname.Value, _Server_Avatar_URL.Value);
                    return;
                }

                if (avatarURL == null || avatarURL == string.Empty)
                {
                    avatarURL = _Default_Avatar_URL.Value;
                }

                SendMessageToWebhook(rawMessage, username, avatarURL, isUserReport);
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnRequestPlayerChat");
            }
        }

        [HarmonyPatch(typeof(MusicJukeboxHandler), nameof(MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatchOnGameEnded
        {
            public static void Postfix(MusicJukeboxHandler __instance, GameMode __0, Team __1)
            {
                try
                {
                    // start a 15 second timer to wait for video to be ready
                    MelonLogger.Msg("Starting 15 second timer.");
                    HelperMethods.StartTimer(ref Timer_CheckForVideo);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameEnded");
                }
            }
        }

        public override void OnUpdate()
        {
            try
            {
                if (HelperMethods.IsTimerActive(Timer_CheckForVideo))
                {
                    Timer_CheckForVideo += Time.deltaTime;

                    if (Timer_CheckForVideo >= 15f)
                    {
                        MelonLogger.Msg("Searching for a new video file.");
                        Timer_CheckForVideo = HelperMethods.Timer_Inactive;

                        // check for new video in the logs directory
                        string recentVideoFile = CheckForRecentVideoFile(Path.Combine(MelonEnvironment.UserDataDirectory, @"logs\"));

                        if (recentVideoFile == string.Empty)
                        {
                            MelonLogger.Msg("No recent video files found. Skipping.");
                            return;
                        }
                            
                        MelonLogger.Msg("Found recent video file: " +  recentVideoFile);

                        SendVideoToWebhook(recentVideoFile, "see latest match");
                    }
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run OnUpdate");
            }
        }

        public static string CheckForRecentVideoFile(string directoryPath)
        {
            // only check against the latest mp4 file
            var recentFile = Directory.EnumerateFiles(directoryPath, "*.mp4")
                .Select(file => new FileInfo(file))
                .OrderByDescending(fileInfo => fileInfo.LastWriteTime)
                .FirstOrDefault();

            if (recentFile == null)
            {
                return string.Empty;
            }

            // was the most recent file updated within the last few minutes?
            DateTime currentTime = DateTime.Now;
            if ((currentTime - recentFile.LastWriteTime).TotalMinutes <= 3)
            {
                return recentFile.FullName;
            }

            return string.Empty;
        }

        static void SendMessageToWebhook(string message, string username, string avatar, bool mentionsAllowed = false)
        {
            if (_Webhooks_URL.Value == string.Empty || message == string.Empty)
            {
                return;
            }

            HTTPRequestHandle request = SteamGameServerHTTP.CreateHTTPRequest(EHTTPMethod.k_EHTTPMethodPOST, _Webhooks_URL.Value);
            string payload = "{\"content\": \"" + message + "\", \"username\": \"" + username + "\", \"avatar_url\": \"" + avatar + "\", \"allowed_mentions\": { \"parse\": [] } }";

            if (mentionsAllowed)
            {
                payload = "{\"content\": \"" + message + "\", \"username\": \"" + username + "\", \"avatar_url\": \"" + avatar + "\"}";
            }

            MelonLogger.Msg("Request: " + request + " " + _Webhooks_URL.Value);
            MelonLogger.Msg("Payload: " + payload);

            byte[] bytes = Encoding.ASCII.GetBytes(payload);

            SteamGameServerHTTP.SetHTTPRequestRawPostBody(request, "application/json", bytes, (uint)bytes.Length);
            SteamAPICall_t webhookCall = new SteamAPICall_t();
            SteamGameServerHTTP.SendHTTPRequest(request, out webhookCall);
            OnHTTPRequestCompletedCallResultDiscord.Set(webhookCall);
        }

        static void SendVideoToWebhook(string filePath, string message)
        {
            if (_Webhooks_URL.Value == string.Empty || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            MelonLogger.Msg("Received request for filepath: " + filePath);

            // Create the HTTP request
            HTTPRequestHandle request = SteamGameServerHTTP.CreateHTTPRequest(EHTTPMethod.k_EHTTPMethodPOST, _Webhooks_URL.Value);

            // Define the boundary for the multipart/form-data request
            string boundary = "----------------------------24e78000bd32";
            string fileName = Path.GetFileName(filePath);

            MelonLogger.Msg("Found filename: " + fileName);
            // Build the multipart form-data payload
            string header = $"--{boundary}\r\n" +
                            $"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"\r\n" +
                            "Content-Type: application/octet-stream\r\n\r\n";
            string footer = $"\r\n--{boundary}--";

            // Read the video file into a byte array
            byte[] videoData = File.ReadAllBytes(filePath);

            // Combine all parts into a single byte array
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            byte[] footerBytes = Encoding.UTF8.GetBytes(footer);

            byte[] requestBody = new byte[headerBytes.Length + videoData.Length + footerBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, requestBody, 0, headerBytes.Length);
            Buffer.BlockCopy(videoData, 0, requestBody, headerBytes.Length, videoData.Length);
            Buffer.BlockCopy(footerBytes, 0, requestBody, headerBytes.Length + videoData.Length, footerBytes.Length);
            
            SteamGameServerHTTP.SetHTTPRequestRawPostBody(request, $"multipart/form-data; boundary={boundary}", requestBody, (uint)requestBody.Length);

            // Send the request
            SteamAPICall_t webhookCall = new SteamAPICall_t();
            SteamGameServerHTTP.SendHTTPRequest(request, out webhookCall);
            OnHTTPRequestCompletedCallResultDiscord.Set(webhookCall);
        }

        static void RequestSteamAvatar(Player player)
        {
            if (_SteamAPI_Key.Value == string.Empty || player == null)
            {
                return;
            }

            string avatarRequestURL = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=" + _SteamAPI_Key.Value + "&steamids=" + player.PlayerID.ToString();
            MelonLogger.Msg(avatarRequestURL);
            HTTPRequestHandle avatarRequest = SteamGameServerHTTP.CreateHTTPRequest(EHTTPMethod.k_EHTTPMethodGET, avatarRequestURL);
            SteamAPICall_t avatarCall = new SteamAPICall_t();
            SteamGameServerHTTP.SendHTTPRequest(avatarRequest, out avatarCall);
            OnHTTPRequestCompletedCallResultAvatar.Set(avatarCall);
        }

        public void OnHTTPRequestCompletedAvatar(HTTPRequestCompleted_t pCallback, bool bIOFailure)
        {
            MelonLogger.Msg("[" + HTTPRequestCompleted_t.k_iCallback + " - HTTPRequestCompleted] - " + pCallback.m_hRequest + " -- " + pCallback.m_ulContextValue + " -- " + pCallback.m_bRequestSuccessful + " -- " + pCallback.m_eStatusCode + " -- " + pCallback.m_unBodySize);

            HTTPRequestHandle request = pCallback.m_hRequest;

            uint size = 0;
            SteamGameServerHTTP.GetHTTPResponseBodySize(request, out size);

            if (size <= 0)
            {
                return;
            }

            byte[] dataBuffer = new byte[size];
            SteamGameServerHTTP.GetHTTPResponseBodyData(request, dataBuffer, size);

            MelonLogger.Msg("Data: " + System.Text.Encoding.UTF8.GetString(dataBuffer));

            GetPlayerSummaries_Root? rootResponse = JsonConvert.DeserializeObject<GetPlayerSummaries_Root>(Encoding.UTF8.GetString(dataBuffer));
            if (rootResponse == null || rootResponse.response == null || rootResponse.response.players == null)
            {
                return;
            }

            if (rootResponse.response.players.Count != 1)
            {
                MelonLogger.Warning("Unexpected player count: " + rootResponse.response.players.Count);
                return;
            }

            string? avatarURL = rootResponse.response.players[0].AvatarMedium;
            MelonLogger.Msg("Adding avatar URL: " + avatarURL);

            if (avatarURL == null || avatarURL == string.Empty)
            {
                return;
            }

            CacheAvatarURLs.Add(rootResponse.response.players[0].SteamID, avatarURL);
        }

        public void OnHTTPRequestCompletedDiscord(HTTPRequestCompleted_t pCallback, bool bIOFailure)
        {
            MelonLogger.Msg("[" + HTTPRequestCompleted_t.k_iCallback + " - HTTPRequestCompleted] - " + pCallback.m_hRequest + " -- " + 
              pCallback.m_ulContextValue + " -- " +
              pCallback.m_bRequestSuccessful + " -- " + 
              pCallback.m_eStatusCode + " -- " + 
              pCallback.m_unBodySize);

            HTTPRequestHandle request = pCallback.m_hRequest;

            uint size = 0;
            SteamGameServerHTTP.GetHTTPResponseHeaderSize(request, "host", out size);

            if (size <= 0)
            {
                return;
            }

            byte[] dataBuffer = new byte[size];
            SteamGameServerHTTP.GetHTTPResponseHeaderValue(request, "host", dataBuffer, size);

            MelonLogger.Msg("Data: " + System.Text.Encoding.UTF8.GetString(dataBuffer));
        }

        static string ConvertHTML(string html)
        {
            ConvertFormatting(ref html);
            return Regex.Replace(html, "<.*?>", String.Empty);
        }

        static void ConvertFormatting(ref string text)
        {
            HandleBolds(ref text);
            HandleUnderlines(ref text);
            HandleItalics(ref text);
        }

        static void HandleBolds(ref string text)
        {
            text = text.Replace("<b>", "**");
            text = text.Replace("</b>", "**");
        }
        static void HandleUnderlines(ref string text)
        {
            text = text.Replace("<u>", "__");
            text = text.Replace("</u>", "__");
        }
        static void HandleItalics(ref string text)
        {
            text = text.Replace("<i>", "_");
            text = text.Replace("</i>", "_");
        }
    }

    public class GetPlayerSummaries_Player
    {
        public ulong SteamID { get; set; }
        public int CommunityVisibilityState { get; set; }
        public int ProfileState { get; set; }
        public string? PersonaName { get; set; }
        public string? ProfileUrl { get; set; }
        public string? Avatar { get; set; }
        public string? AvatarMedium { get; set; }
        public string? AvatarFull { get; set; }
        public string? AvatarHash { get; set; }
        public int LastLogoff { get; set; }
        public int PersonaState { get; set; }
        public string? PrimaryClanId { get; set; }
        public int TimeCreated { get; set; }
        public int PersonaStateFlags { get; set; }
        public string? LocCountryCode { get; set; }
    }

    public class GetPlayerSummaries_Response
    {
        public List<GetPlayerSummaries_Player>? players { get; set; }
    }

    public class GetPlayerSummaries_Root
    {
        public GetPlayerSummaries_Response? response { get; set; }
    }
}
