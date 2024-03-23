/*
Silica Admin Mod
Copyright (C) 2024 by databomb

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
using Il2CppDebugTools;
#else
using DebugTools;
#endif

using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace SilicaAdminMod
{
    #if !NET6_0
    public class CSAM_ConsoleCommand : DebugConsole.ICommand
    {
        private string _key = null!;
        private string _description = null!;
        private EAdminLevel? _level = null!;

        public CSAM_ConsoleCommand(string keyName, string description, EAdminLevel? level)
        {
            _key = keyName;
            _description = description;
            _level = level;
        }
        
        public string Key
        {
            get
            {
                return _key;
            }
        }

        public string Description
        {
            get
            {
                return _description;
            }
        }

        public EAdminLevel RequiredAdminLevel
        {
            get
            {
                if (_level == null)
                {
                    return EAdminLevel.STANDARD;
                }

                return (EAdminLevel)_level;
            }
        }

        public bool ServerSide
        {
            get
            {
                return true;
            }
        }

        public void Execute(params string[] parameters)
        {
            // generate single command string
            string fullCommand = this.Key + " " + string.Join(" ", parameters);
            DebugConsole.Log("Processing command: " + fullCommand, DebugConsole.LogLevel.Log);

            // trim "sam_"
            string commandText = this.Key[4..];
            
            AdminCommand? adminCommand = AdminMethods.FindAdminCommandFromString(commandText);
            if (adminCommand != null)
            {
                DebugConsole.Log("Valid admin command found. Running callback.", DebugConsole.LogLevel.Log);
                adminCommand.AdminCallback(null, fullCommand);
            }

            DebugConsole.Log("", DebugConsole.LogLevel.Log);
        }
    }

    [HarmonyPatch(typeof(DebugConsole), nameof(DebugConsole.TryExecuteCommand))]
    static class Patch_DebugConsole_TryExecuteCommand
    {
        public static bool Prefix(string __0, bool __1, Player __2)
        {
            try
            {
                // if remoteCaller Player isn't there
                if (__2 == null)
                {
                    return true;
                }

                MelonLogger.Msg("Received remote command string: " + __0 + " from " + __2.PlayerName);

                // are they trying to run a command registered by SAM?
                if (IsValidConsoleCommand(__0))
                {
                    string commandText = __0.Split(' ')[0];
                    // trim "sam_"
                    commandText = commandText[4..];

                    AdminCommand? adminCommand = AdminMethods.FindAdminCommandFromString(commandText);
                    if (adminCommand != null)
                    {
                        // are they an admin?
                        if (!__2.IsAdmin())
                        {
                            SendNetworkResponseWarning(__2, string.Format("Caller \"{0}\" is not an admin and cannot remotely execute \"{1}\"", __2.PlayerName, commandText));
                            return false;
                        }

                        // do they have the matching power?
                        Power callerPowers = __2.GetAdminPowers();

                        if (!AdminMethods.PowerInPowers(adminCommand.AdminPower, callerPowers))
                        {
                            SendNetworkResponseWarning(__2, string.Format("Caller \"{0}\" does not have sufficient admin privileges to remotely execute \"{1}\"", __2.PlayerName, commandText));
                            return false;
                        }

                        // run the callback
                        adminCommand.AdminCallback(__2, __0);
                        return false;
                    }

                    SendNetworkResponseWarning(__2, string.Format("Caller \"{0}\" has tried to use an invalid remote SAM command: \"{1}\"", __2.PlayerName, commandText));
                    return false;
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to run DebugConsole::TryExecuteCommand");
            }

            return true;
        }

        static void SendNetworkResponseWarning(Player callerPlayer, string responseLine)
        {
            DebugConsole.Log(responseLine, DebugConsole.LogLevel.Warning);
            #if NET6_0
            Il2CppSystem.Collections.Generic.List<string> responseText = new Il2CppSystem.Collections.Generic.List<string>();
            responseText.Add(responseLine);
            Il2CppSystem.Collections.Generic.List<DebugConsole.LogLevel> responseLevel = new Il2CppSystem.Collections.Generic.List<DebugConsole.LogLevel>();
            responseLevel.Add(DebugConsole.LogLevel.Warning);
            NetworkLayer.SendRemoteCommandResult(callerPlayer, responseText, responseLevel);
            #else
            List<string> responseText = new List<string>
            {
                responseLine
            };
            List<DebugConsole.LogLevel> responseLevel = new List<DebugConsole.LogLevel>
            {
                DebugConsole.LogLevel.Warning
            };
            NetworkLayer.SendRemoteCommandResult(callerPlayer, responseText, responseLevel);
            #endif
        }

        static bool IsValidConsoleCommand(string consoleCommand)
        {
            if (consoleCommand.Length < 5)
            {
                return false;
            }

            return consoleCommand.StartsWith("sam_");
        }
    }
    #endif
}