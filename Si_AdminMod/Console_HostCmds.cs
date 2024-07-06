/*
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
using MelonLoader;

#if NET6_0
using Il2Cpp;
using Il2CppDebugTools;
#else
using DebugTools;
#endif

namespace SilicaAdminMod
{
    #if !NET6_0
    public class CSAM_AddAdmin : DebugConsole.ICommand
    {
        public string Key
        {
            get
            {
                return "sam_addadmin";
            }
        }

        public string Description
        {
            get
            {
                return "Allows a host to add an admin to SAM from the console";
            }
        }

        public EAdminLevel RequiredAdminLevel
        {
            get
            {
                return EAdminLevel.HOST;
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
            if (parameters == null || parameters.Length < 3)
            {
                DebugConsole.Log(this.Key + SiConstants.SAM_AddAdmin_Usage, DebugConsole.LogLevel.Log);
                return;
            }

            if (parameters.Length > 3)
            {
                DebugConsole.Log(this.Key + SiConstants.SAM_AddAdmin_Usage, DebugConsole.LogLevel.Log);
                return;
            }

            // validate argument contents
            String targetText = parameters[0];
            Player? player = HelperMethods.FindTargetPlayer(targetText);
            if (player == null)
            {
                DebugConsole.Log(this.Key + ": Ambiguous or invalid target", DebugConsole.LogLevel.Log);
                return;
            }

            String powersText = parameters[1];
            if (powersText.Any(char.IsDigit))
            {
                DebugConsole.Log(this.Key + ": Powers invalid", DebugConsole.LogLevel.Log);
                return;
            }

            String levelText = parameters[2];
            int level = int.Parse(levelText);
            if (level < 0)
            {
                DebugConsole.Log(this.Key + ": Level too low", DebugConsole.LogLevel.Log);
                return;
            }
            else if (level > 255)
            {
                DebugConsole.Log(this.Key + ": Level too high", DebugConsole.LogLevel.Log);
                return;
            }

            if (HostAction.AddAdmin(player, powersText, (byte)level))
            {
                DebugConsole.Log(this.Key + ": Added " + player.PlayerName + " as an admin (Level " + levelText + ")", DebugConsole.LogLevel.Log);
            }
            else
            {
                DebugConsole.Log(this.Key + ": " + player.PlayerName + " is already an admin", DebugConsole.LogLevel.Log);
            }

            DebugConsole.Log("", DebugConsole.LogLevel.Log);
        }
    }

    public class CSAM_Say : DebugConsole.ICommand
    {
        public string Key
        {
            get
            {
                return "sam_say";
            }
        }

        public string Description
        {
            get
            {
                return "Allows a host to send a SAM message from server console";
            }
        }

        public EAdminLevel RequiredAdminLevel
        {
            get
            {
                return EAdminLevel.HOST;
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
            if (parameters == null)
            {
                DebugConsole.Log(this.Key + " usage: <message>", DebugConsole.LogLevel.Log);
                DebugConsole.Log("", DebugConsole.LogLevel.Log);
                return;
            }

            HelperMethods.AlertAdminAction(null, String.Join(" ", parameters));
        }
    }

    public class CSAM_Cvar : DebugConsole.ICommand
    {
        public string Key
        {
            get
            {
                return "sam_cvar";
            }
        }

        public string Description
        {
            get
            {
                return "Allows a host to change a preference value without a restart";
            }
        }

        public EAdminLevel RequiredAdminLevel
        {
            get
            {
                return EAdminLevel.HOST;
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
            if (parameters == null || parameters.Length < 1)
            {
                DebugConsole.Log(this.Key + " usage: <preference_string> [optional: new_value]", DebugConsole.LogLevel.Log);
                DebugConsole.Log("", DebugConsole.LogLevel.Log);
                return;
            }

            MelonPreferences_Entry? preferenceEntry = SiAdminMod._modCategory.GetEntry(parameters[0]);
            if (preferenceEntry == null)
            {
                DebugConsole.Log("Could not find specified preference in the preference list: " + parameters[0], DebugConsole.LogLevel.Warning);
                DebugConsole.Log("", DebugConsole.LogLevel.Log);
                return;
            }

            // do we want to modify the value?
            if (parameters.Length > 1)
            {
                Type preferenceType = preferenceEntry.GetReflectedType();
                if (preferenceType == typeof(string))
                {
                    preferenceEntry.BoxedValue = String.Join(" ", parameters[1..]);
                    DebugConsole.Log(parameters[0] + " changed to: " + preferenceEntry.GetValueAsString(), DebugConsole.LogLevel.Log);
                    DebugConsole.Log("", DebugConsole.LogLevel.Log);
                }
                else if (preferenceType == typeof(bool))
                {
                    bool parsedBool;
                    if (bool.TryParse(parameters[1], out parsedBool))
                    {
                        preferenceEntry.BoxedValue = parsedBool;
                        DebugConsole.Log(parameters[0] + " changed to: " + preferenceEntry.GetValueAsString(), DebugConsole.LogLevel.Log);
                        DebugConsole.Log("", DebugConsole.LogLevel.Log);
                    }
                    else
                    {
                        DebugConsole.Log("Could not convert argument (" + parameters[1] + ") into preference type (" + preferenceType.ToString() + ")", DebugConsole.LogLevel.Warning);
                        DebugConsole.Log("", DebugConsole.LogLevel.Log);
                    }
                }
                else if (preferenceType == typeof(int))
                {
                    int parsedInt;
                    if (int.TryParse(parameters[1], out parsedInt))
                    {
                        preferenceEntry.BoxedValue = parsedInt;
                        DebugConsole.Log(parameters[0] + " changed to: " + preferenceEntry.GetValueAsString(), DebugConsole.LogLevel.Log);
                        DebugConsole.Log("", DebugConsole.LogLevel.Log);
                    }
                    else
                    {
                        DebugConsole.Log("Could not convert argument (" + parameters[1] + ") into preference type (" + preferenceType.ToString() + ")", DebugConsole.LogLevel.Warning);
                        DebugConsole.Log("", DebugConsole.LogLevel.Log);
                    }
                }
                else if (preferenceType == typeof(float))
                {
                    float parsedFloat;
                    if (float.TryParse(parameters[1], out parsedFloat))
                    {
                        preferenceEntry.BoxedValue = parsedFloat;
                        DebugConsole.Log(parameters[0] + " changed to: " + preferenceEntry.GetValueAsString(), DebugConsole.LogLevel.Log);
                        DebugConsole.Log("", DebugConsole.LogLevel.Log);
                    }
                    else
                    {
                        DebugConsole.Log("Could not convert argument (" + parameters[1] + ") into preference type (" + preferenceType.ToString() + ")", DebugConsole.LogLevel.Warning);
                        DebugConsole.Log("", DebugConsole.LogLevel.Log);
                    }
                }
                else
                {
                    DebugConsole.Log("Could not handle preference type (" + preferenceType.ToString() + ")", DebugConsole.LogLevel.Warning);
                    DebugConsole.Log("", DebugConsole.LogLevel.Log);
                }
            }
            // or just see the value?
            else
            {
                DebugConsole.Log(parameters[0] + " is set to: " + preferenceEntry.GetValueAsString(), DebugConsole.LogLevel.Log);
                DebugConsole.Log("", DebugConsole.LogLevel.Log);
                return;
            }
        }
    }
    #endif
}
