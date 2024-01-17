/*
 Silica Webhooks Mod
 Copyright (C) 2024 by databomb
 
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

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using Steamworks;
#endif

using HarmonyLib;
using MelonLoader;
using Si_Webhooks;
using SilicaAdminMod;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;

[assembly: MelonInfo(typeof(Webhooks), "Webhooks", "1.2.2", "databomb", "https://github.com/data-bomb/Silica")]
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

        static CallResult<HTTPRequestCompleted_t> OnHTTPRequestCompletedCallResult = null!;

        static Dictionary<ulong, string> CacheAvatarURLs = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Webhooks_URL ??= _modCategory.CreateEntry<string>("Webhooks_URL", "");
            _Server_Shortname ??= _modCategory.CreateEntry<string>("Webhooks_Server_Shortname", "Server");
            _RoleToMentionForReports ??= _modCategory.CreateEntry<string>("Webhooks_Report_To_Role", "");
            _SteamAPI_Key ??= _modCategory.CreateEntry<string>("Webhooks_Steam_API_Key", "");
            _Server_Avatar_URL ??= _modCategory.CreateEntry<string>("Webhooks_Server_Avatar_URL", "https://cdn.discordapp.com/icons/824561906277810187/d2f40915db72206f36abb46975655434.webp");
            _Default_Avatar_URL ??= _modCategory.CreateEntry<string>("Webhooks_Default_Avatar_URL", "https://cdn.discordapp.com/icons/663449315876012052/9e482a81d84ee8e750d07c8dbe5b78e4.webp");

            OnHTTPRequestCompletedCallResult = CallResult<HTTPRequestCompleted_t>.Create((CallResult<HTTPRequestCompleted_t>.APIDispatchDelegate)OnHTTPRequestCompleted);

            CacheAvatarURLs = new Dictionary<ulong, string>();
        }

        #if NET6_0
        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        #else
        [HarmonyPatch(typeof(Silica.UI.Chat), "MessageReceived")]
        #endif
        private static class TechGlitch_Chat_MessageReceived
        {
            #if NET6_0
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Player __0, string __1, bool __2)
            #else
            public static void Postfix(Silica.UI.Chat __instance, Player __0, string __1, bool __2)
            #endif
            {
                try
                {
                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (!__instance.ToString().Contains("alien") || __2)
                    {
                        return;
                    }

                    if (__0 == null)
                    {
                        return;
                    }

                    string rawMessage = ConvertHTML(__1);
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

                    string username = __0.PlayerName;
                    string? avatarURL = string.Empty;
                    // cache the Steam avatar, if it's needed
                    if (!CacheAvatarURLs.ContainsKey(__0.PlayerID.m_SteamID))
                    {
                        //MelonLogger.Msg("Missing Avatar URL for " + username + ". Grabbing it...");
                        RequestSteamAvatar(__0);
                    }
                    else
                    {
                        // use the cached avatar
                        CacheAvatarURLs.TryGetValue(__0.PlayerID.m_SteamID, out avatarURL);
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
                            reportMessage = __0.PlayerName + " (" + __0.PlayerID.ToString() + ") is requesting an admin in the game. <@&" + _RoleToMentionForReports.Value + ">";
                        }
                        else
                        {
                            reportMessage = __0.PlayerName + " (" + __0.PlayerID.ToString() + ") is requesting an admin in the game. Report:" + rawMessage.Substring(spaceCharacter) + " <@&" + _RoleToMentionForReports.Value + ">";
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
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }

        static void SendMessageToWebhook(string message, string username, string avatar, bool mentionsAllowed = false)
        {
            if (_Webhooks_URL.Value == string.Empty || message == string.Empty)
            {
                return;
            }

            HTTPRequestHandle request = SteamHTTP.CreateHTTPRequest(EHTTPMethod.k_EHTTPMethodPOST, _Webhooks_URL.Value);
            string payload = "{\"content\": \"" + message + "\", \"username\": \"" + username + "\", \"avatar_url\": \"" + avatar + "\", \"allowed_mentions\": { \"parse\": [] } }";

            if (mentionsAllowed)
            {
                payload = "{\"content\": \"" + message + "\", \"username\": \"" + username + "\", \"avatar_url\": \"" + avatar + "\"}";
            }

            //MelonLogger.Msg("Payload: " + payload);

            byte[] bytes = Encoding.ASCII.GetBytes(payload);

            SteamHTTP.SetHTTPRequestRawPostBody(request, "application/json", bytes, (uint)bytes.Length);
            SteamAPICall_t webhookCall = new SteamAPICall_t();
            SteamHTTP.SendHTTPRequest(request, out webhookCall);
        }

        static void RequestSteamAvatar(Player player)
        {
            if (_SteamAPI_Key.Value == string.Empty || player == null)
            {
                return;
            }

            string avatarRequestURL = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=" + _SteamAPI_Key.Value + "&steamids=" + player.PlayerID.ToString();
            //MelonLogger.Msg(avatarRequestURL);
            HTTPRequestHandle avatarRequest = SteamHTTP.CreateHTTPRequest(EHTTPMethod.k_EHTTPMethodGET, avatarRequestURL);
            SteamAPICall_t avatarCall = new SteamAPICall_t();
            SteamHTTP.SendHTTPRequest(avatarRequest, out avatarCall);
            OnHTTPRequestCompletedCallResult.Set(avatarCall);
        }

        public void OnHTTPRequestCompleted(HTTPRequestCompleted_t pCallback, bool bIOFailure)
        {
            HTTPRequestHandle request = pCallback.m_hRequest;

            uint size = 0;
            SteamHTTP.GetHTTPResponseBodySize(request, out size);

            if (size <= 0)
            {
                return;
            }

            byte[] dataBuffer = new byte[size];
            SteamHTTP.GetHTTPResponseBodyData(request, dataBuffer, size);

            //MelonLogger.Msg("Data: " + System.Text.Encoding.UTF8.GetString(dataBuffer));

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
            //MelonLogger.Msg("Adding avatar URL: " + avatarURL);

            if (avatarURL == null || avatarURL == string.Empty)
            {
                return;
            }

            CacheAvatarURLs.Add(rootResponse.response.players[0].SteamID, avatarURL);
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