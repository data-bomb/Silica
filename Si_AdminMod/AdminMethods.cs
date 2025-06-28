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

#if !NET6_0
using DebugTools;
#else
using Il2Cpp;
#endif

using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SilicaAdminMod
{
    public static class AdminMethods
    {
        public static List<AdminCommand> AdminCommands = null!;

        public static void RegisterAdminCommand(String adminCommand, HelperMethods.CommandCallback adminCallback, Power adminPower, String? adminDescription = null)
        {
            AdminCommand thisCommand = new AdminCommand
            {
                AdminCommandText = adminCommand,
                AdminCallback = adminCallback,
                AdminPower = adminPower
            };

            if (adminDescription != null)
            {
                thisCommand.AdminCommandDescription = adminDescription;
            }

            AdminCommands.Add(thisCommand);

            #if !NET6_0
            FieldInfo commandField = typeof(DebugConsole).GetField("s_Commands", BindingFlags.NonPublic | BindingFlags.Static);
            string consoleCommandText = "sam_" + thisCommand.AdminCommandText;

            // TODO: Convert AdminPower to server admin system
            DebugConsole.ICommand addAdminConsoleCmd = (DebugConsole.ICommand)Activator.CreateInstance(typeof(CSAM_ConsoleCommand), consoleCommandText, thisCommand.AdminCommandDescription, EAdminLevel.STANDARD);
            if (addAdminConsoleCmd != null)
            {
                Dictionary<string, DebugConsole.ICommand> s_Commands = (Dictionary<string, DebugConsole.ICommand>)commandField.GetValue(null);
                if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
                {
                    MelonLogger.Msg(addAdminConsoleCmd.Key.ToLower() + " console command registered.");
                }
                s_Commands.Add(addAdminConsoleCmd.Key.ToLower(), addAdminConsoleCmd);
                commandField.SetValue(null, s_Commands);
            }
            #endif
        }

        public static bool UnregisterAdminCommand(String adminCommand)
        {
            AdminCommand? matchingCommand = AdminMethods.FindAdminCommandFromString(adminCommand);
            if (matchingCommand == null)
            {
                return false;
            }

            return AdminCommands.Remove(matchingCommand);
        }

        public static Admin? FindAdminFromSteamId(long steamId, bool modAdminOnly = false)
        {
            // check the mod admin list first
            foreach (Admin modAdmin in SiAdminMod.AdminList)
            {
                if (modAdmin.SteamId == steamId)
                {
                    return modAdmin;
                }
            }

            if (modAdminOnly)
            {
                return null;
            }

            // grant some default powers if they are on the game's admin list but missing from the mod admin list
            NetworkID networkID = new NetworkID((ulong)steamId);
            NetworkAdminPlayer gameAdmin = NetworkServerSettings.GetPlayerAdmin(networkID);
            if (gameAdmin != null)
            {
                Admin gameModAdminConversion = new Admin
                {
                    SteamId = steamId,
                    Level = (gameAdmin.m_Root ? (byte)100 : (byte)10),
                    Name = gameAdmin.m_Name,
                    CreatedOn = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                    LastModifiedOn = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                    Powers = (gameAdmin.m_Root ? Power.Root : (Power.Kick
                                                             | Power.Slay
                                                             | Power.Map
                                                             | Power.Commander
                                                             | Power.End
                                                             | Power.Eject
                                                             | Power.Mute
                                                             | Power.Generic
                                                             | Power.Teams))
                };

                return gameModAdminConversion;
            }

            return null;
        }

        public static AdminCommand? FindAdminCommandFromString(String commandText)
        {
            foreach (AdminCommand command in AdminCommands)
            {
                if (String.Equals(command.AdminCommandText, commandText, StringComparison.OrdinalIgnoreCase))
                {
                    return command;
                }
            }
            return null;
        }

        public static bool PowerInPowers(Power power, Power powers)
        {
            Power powerLess = powers & (power | Power.Root);
            if (powerLess != Power.None)
            {
                return true;
            }

            return false;
        }
    }
}