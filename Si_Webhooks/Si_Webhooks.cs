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

[assembly: MelonInfo(typeof(Webhooks), "Webhooks", "1.0.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Webhooks
{
    public class Webhooks : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<string> _Webhooks_URL = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _Webhooks_URL ??= _modCategory.CreateEntry<string>("Webhooks_URL", "");
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

                    string rawMessage = StripHTML(__1);
                    if (rawMessage == string.Empty)
                    {
                        return;
                    }

                    SendMessageToWebhook(rawMessage, __0.PlayerName);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Chat::MessageReceived");
                }
            }
        }

        static void SendMessageToWebhook(string message, string username)
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
            byte[] bytes = Encoding.ASCII.GetBytes(payload);

            SteamHTTP.SetHTTPRequestRawPostBody(request, "application/json", bytes, (uint)bytes.Length);
            SteamAPICall_t steamAPICall_T = new SteamAPICall_t();
            SteamHTTP.SendHTTPRequest(request, out steamAPICall_T);
        }

        static string StripHTML(string html)
        {
            return Regex.Replace(html, "<.*?>", String.Empty);
        }
    }
}