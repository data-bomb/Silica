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

[assembly: MelonInfo(typeof(Webhooks), "Webhooks", "1.1.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Webhooks
{
    public class Webhooks : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<string> _Webhooks_URL = null!;
        static MelonPreferences_Entry<string> _Server_Shortname = null!;
        static MelonPreferences_Entry<string> _RoleToMentionForReports = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Webhooks_URL ??= _modCategory.CreateEntry<string>("Webhooks_URL", "");
            _Server_Shortname ??= _modCategory.CreateEntry<string>("Webhooks_Server_Shortname", "Server");
            _RoleToMentionForReports ??= _modCategory.CreateEntry<string>("Webhooks_Report_To_Role", "@Moderator");
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

                    string username = __0.PlayerName;
                    if (rawMessage.StartsWith("**[SAM"))
                    {
                        username = _Server_Shortname.Value;
                    }

                    // is this a user report?
                    bool isUserReport = String.Equals(rawMessage, "!report", StringComparison.OrdinalIgnoreCase);
                    if (isUserReport)
                    {
                        rawMessage = rawMessage + " " + _RoleToMentionForReports.Value;
                        SendMessageToWebhook(rawMessage, username, true);
                        return;
                    }

                    SendMessageToWebhook(rawMessage, username);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }

        static void SendMessageToWebhook(string message, string username, bool mentionsAllowed = false)
        {
            if (_Webhooks_URL.Value == string.Empty || message == string.Empty)
            {
                return;
            }

            var SuccessWebHook = new
            {
                username = username,
                content = message
            };

            HTTPRequestHandle request = SteamHTTP.CreateHTTPRequest(EHTTPMethod.k_EHTTPMethodPOST, _Webhooks_URL.Value);
            string payload = "{\"content\": \"" + message + "\", \"username\": \"" + username + "\"}";

            if (mentionsAllowed)
            {
                payload = "{\"content\": \"" + message + "\", \"username\": \"" + username + "\", \"allowed_mentions\": { \"parse\": [\"roles\"] } }";
            }

            byte[] bytes = Encoding.ASCII.GetBytes(payload);

            SteamHTTP.SetHTTPRequestRawPostBody(request, "application/json", bytes, (uint)bytes.Length);
            SteamAPICall_t steamAPICall_T = new SteamAPICall_t();
            SteamHTTP.SendHTTPRequest(request, out steamAPICall_T);
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
            text.Replace("<b>", "**");
            text.Replace("</b>", "**");
        }
        static void HandleUnderlines(ref string text)
        {
            text.Replace("<u>", "__");
            text.Replace("</u>", "__");
        }
        static void HandleItalics(ref string text)
        {
            text.Replace("<i>", "_");
            text.Replace("</i>", "_");
        }
    }
}