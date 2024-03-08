﻿/*
Silica Admin Mod
Copyright (C) 2023-2024 by databomb

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
using System.Linq;

#if NET6_0
using Il2Cpp;
#endif

namespace SilicaAdminMod
{
    // SendChatMessage will only fire for the local user, the host
    [HarmonyPatch(typeof(Player), nameof(Player.SendChatMessage))]
    static class Patch_SendChatMessage_AdminCommands
    {
        public static bool Prefix(Player __instance, bool __result, string __0, bool __1)
        {
            try
            {
                // ignore team chat if preference is set
                if (__1 && SiAdminMod.Pref_Admin_AcceptTeamChatCommands != null && !SiAdminMod.Pref_Admin_AcceptTeamChatCommands.Value)
                {
                    return true;
                }

                bool isAddAdminCommand = (String.Equals(__0.Split(' ')[0], "!addadmin", StringComparison.OrdinalIgnoreCase));
                if (isAddAdminCommand)
                {
                    // only the host is authorized to add admins for now
                    Player serverPlayer = NetworkGameServer.GetServerPlayer();

                    if (__instance != serverPlayer)
                    {
                        HelperMethods.ReplyToCommand_Player(__instance, "cannot use " + __0.Split(' ')[0]);
                        return false;
                    }

                    // validate argument count
                    int argumentCount = __0.Split(' ').Length - 1;
                    if (argumentCount > 3)
                    {
                        HelperMethods.ReplyToCommand(__0.Split(' ')[0] + SiConstants.SAM_AddAdmin_Usage);
                        return false;
                    }
                    else if (argumentCount < 3)
                    {
                        HelperMethods.ReplyToCommand(__0.Split(' ')[0] + SiConstants.SAM_AddAdmin_Usage);
                        return false;
                    }

                    // validate argument contents
                    String targetText = __0.Split(' ')[1];
                    Player? player = HelperMethods.FindTargetPlayer(targetText);
                    if (player == null)
                    {
                        HelperMethods.ReplyToCommand(__0.Split(' ')[0] + ": Ambiguous or invalid target");
                        return false;
                    }

                    String powersText = __0.Split(' ')[2];
                    if (powersText.Any(char.IsDigit))
                    {
                        HelperMethods.ReplyToCommand(__0.Split(' ')[0] + ": Powers invalid");
                        return false;
                    }

                    String levelText = __0.Split(' ')[3];
                    int level = int.Parse(levelText);
                    if (level < 0)
                    {
                        HelperMethods.ReplyToCommand(__0.Split(' ')[0] + ": Level too low");
                        return false;
                    }
                    else if (level > 255)
                    {
                        HelperMethods.ReplyToCommand(__0.Split(' ')[0] + ": Level too high");
                        return false;
                    }

                    if (HostAction.AddAdmin(player, powersText, (byte)level))
                    {
                        HelperMethods.ReplyToCommand_Player(player, "added as admin (Level " + levelText + ")");
                    }
                    else
                    {
                        HelperMethods.ReplyToCommand_Player(player, "is already an admin");
                    }

                    return false;
                }

                bool isRemoveAdminCommand = (String.Equals(__0.Split(' ')[0], "!removeadmin", StringComparison.OrdinalIgnoreCase) ||
                                                String.Equals(__0.Split(' ')[0], "!deladmin", StringComparison.OrdinalIgnoreCase));
                if (isRemoveAdminCommand)
                {
                    // only the host is authorized to add admins for now
                    Player serverPlayer = NetworkGameServer.GetServerPlayer();

                    if (__instance != serverPlayer)
                    {
                        HelperMethods.ReplyToCommand_Player(__instance, "cannot use " + __0.Split(' ')[0]);
                        return false;
                    }

                    // TODO

                    return false;
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run SendChatMessage");
            }

            return true;
        }
    }
}
