/*
Silica Admin Mod
Copyright (C) 2023 by databomb

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
using System;

namespace SilicaAdminMod
{
        #if NET6_0
        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        #else
        [HarmonyPatch(typeof(Silica.UI.Chat), "MessageReceived")]
        #endif
        static class Patch_MessageReceived_AdminCommands
        {
            #if NET6_0
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            #else
            public static void Postfix(Silica.UI.Chat __instance, Player __0, string __1, bool __2)
            #endif
            {
                try
                {
                    // check if this even has a '!' or '/' as the command prefix
                    if (__1[0] != '!' && __1[0] != '/')
                    {
                        return;
                    }

                    // ignore team chat if preference is set
                    if (__2 && SiAdminMod.Pref_Admin_AcceptTeamChatCommands != null && !SiAdminMod.Pref_Admin_AcceptTeamChatCommands.Value)
                    {
                        return;
                    }

                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (!__instance.ToString().Contains("alien"))
                    {
                        return;
                    }

                    // check if the first portion matches an admin command
                    String thisCommandText = __1.Split(' ')[0];
                    AdminCommand? checkCommand = AdminMethods.FindAdminCommandFromString(thisCommandText);

                    if (checkCommand == null)
                    {
                        return;
                    }

                    // are they an admin?
                    if (!__0.IsAdmin())
                    {
                        HelperMethods.ReplyToCommand_Player(__0, "is not an admin");
                        return;
                    }

                    // do they have the matching power?
                    Power callerPowers = __0.GetAdminPowers();

                    if (!AdminMethods.PowerInPowers(checkCommand.AdminPower, callerPowers))
                    {
                        HelperMethods.ReplyToCommand_Player(__0, "unauthorized command");
                        return;
                    }

                    // run the callback
                    checkCommand.AdminCallback(__0, __1);
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MessageReceived");
                }

                return;
        }
    }
}