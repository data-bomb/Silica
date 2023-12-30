/*
Silica Admin Mod
Copyright (C) 2023 by databomb

* Description *
Provides basic admin mod system to allow additional admins beyond
the host.

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
using MelonLoader;
using SilicaAdminMod;
using Newtonsoft.Json;
using AdminExtension;
using MelonLoader.Utils;
using static SilicaAdminMod.SiAdminMod;
using System;
using System.Collections.Generic;
using System.Linq;

[assembly: MelonInfo(typeof(SiAdminMod), "Admin Mod", "1.1.6", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace SilicaAdminMod
{
    public class SiAdminMod : MelonMod
    {
        static readonly String adminFile = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "admins.json");

        static MelonPreferences_Category? _modCategory;
        static MelonPreferences_Entry<bool>? Pref_Admin_AcceptTeamChatCommands;

        public class AdminCommand
        {
            public String AdminCommandText
            {
                get;
                set;
            }
            public HelperMethods.CommandCallback AdminCallback
            {
                get;
                set;
            }
            public Power AdminPower
            {
                get;
                set;
            }
        }

        public class Admin
        {
            public String? Name
            {
                get;
                set;
            }

            public long SteamId
            {
                get;
                set;
            }
            public Power Powers
            {
                get;
                set;
            }
            public byte Level
            {
                get;
                set;
            }
            public int CreatedOn
            {
                get;
                set;
            }
            public int LastModifiedOn
            {
                get;
                set;
            }
        }

        static List<Admin> AdminList;
        static List<AdminCommand> AdminCommands;

        public override void OnInitializeMelon()
        {
            try
            {
                AdminCommands = new List<AdminCommand>();

                if (System.IO.File.Exists(adminFile))
                {
                    // Open the stream and read it back.
                    System.IO.StreamReader adminFileStream = System.IO.File.OpenText(adminFile);
                    using (adminFileStream)
                    {
                        String JsonRaw = adminFileStream.ReadToEnd();
                        if (JsonRaw == null)
                        {
                            MelonLogger.Warning("The admins.json read as empty. No admins loaded.");
                        }
                        else
                        {
                            AdminList = JsonConvert.DeserializeObject<List<Admin>>(JsonRaw);

                            if (AdminList == null)
                            {
                                MelonLogger.Warning("Encountered deserialization error in admins.json file. Ensure file is in valid format (e.g., https://jsonlint.com/)");
                            }
                            else
                            {
                                MelonLogger.Msg("Loaded admins.json with " + AdminList.Count + " admin entries.");
                            }
                        }
                    }
                }
                else
                {
                    MelonLogger.Warning("Did not find admins.json file. No admin entries loaded.");
                    AdminList = new List<Admin>();
                }

                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                Pref_Admin_AcceptTeamChatCommands ??= _modCategory.CreateEntry<bool>("Admin_AllowTeamChatCommands", false);
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to load admins");
            }
        }

        public static void UpdateAdminFile()
        {
            // convert back to json string
            String JsonRaw = JsonConvert.SerializeObject(AdminList, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(adminFile, JsonRaw);
        }

        public static void RegisterAdminCommand(String adminCommand, HelperMethods.CommandCallback adminCallback, Power adminPower)
        {
            AdminCommand thisCommand = new AdminCommand();
            thisCommand.AdminCommandText = adminCommand;
            thisCommand.AdminCallback = adminCallback;
            thisCommand.AdminPower = adminPower;

            AdminCommands.Add(thisCommand);
        }

        public static bool AddAdmin(Il2Cpp.Player player, String powerText, byte level)
        {
            Admin admin = new Admin();
            admin.SteamId = long.Parse(player.ToString().Split('_')[1]);

            // check if we have a match before adding more details
            if (AdminList.Find(i => i.SteamId == admin.SteamId) == null)
            {
                admin.Name = player.PlayerName;
                admin.Level = level;
                admin.Powers = HelperMethods.PowerTextToPower(powerText);
                admin.CreatedOn = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                admin.LastModifiedOn = admin.CreatedOn;

                AdminList.Add(admin);

                UpdateAdminFile();
                return true;
            }

            return false;
        }

        public static bool PowerInPowers(Power power, Power powers)
        {
            Power powerLess = powers & ( power | Power.Root);
            if (powerLess != Power.None)
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class Patch_MessageReceived_AdminCommands
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    // check if this even has a '!' or '/' as the command prefix
                    if (__1[0] != '!' && __1[0] != '/')
                    {
                        return;
                    }

                    // ignore team chat if preference is set
                    if (__2 && Pref_Admin_AcceptTeamChatCommands != null && !Pref_Admin_AcceptTeamChatCommands.Value)
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
                    AdminCommand? checkCommand = AdminCommands.Find(i => i.AdminCommandText == thisCommandText);

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

                    if (!PowerInPowers(checkCommand.AdminPower, callerPowers))
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

        // SendChatMessage will only fire for the local user, the host
        [HarmonyPatch(typeof(Il2Cpp.Player), nameof(Il2Cpp.Player.SendChatMessage))]
        private static class Patch_SendChatMessage_AdminCommands
        {
            public static bool Prefix(Il2Cpp.Player __instance, bool __result, string __0, bool __1)
            {
                try
                {
                    bool isAddAdminCommand = (String.Equals(__0.Split(' ')[0], "!addadmin", StringComparison.OrdinalIgnoreCase));
                    if (isAddAdminCommand)
                    {
                        // only the host is authorized to add admins for now
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

                        if (__instance != serverPlayer)
                        {
                            HelperMethods.ReplyToCommand_Player(__instance, "cannot use " + __0.Split(' ')[0]);
                            return false;
                        }

                        // validate argument count
                        int argumentCount = __0.Split(' ').Length - 1;
                        if (argumentCount > 3)
                        {
                            HelperMethods.ReplyToCommand(__0.Split(' ')[0] + ": Too many arguments");
                            return false;
                        }
                        else if (argumentCount < 3)
                        {
                            HelperMethods.ReplyToCommand(__0.Split(' ')[0] + ": Too few arguments");
                            return false;
                        }

                        // validate argument contents
                        String targetText = __0.Split(' ')[1];
                        Il2Cpp.Player? player = HelperMethods.FindTargetPlayer(targetText);
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

                        if (AddAdmin(player, powersText, (byte)level))
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
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

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

        // Extension methods for Il2Cpp.Player class
        public class PlayerAdmin : Il2Cpp.Player
        {
            public static bool CanAdminExecute(Il2Cpp.Player callerPlayer, Power power, Il2Cpp.Player? targetPlayer = null)
            {
                Admin? callerMatch = AdminList.Find(i => i.SteamId == long.Parse(callerPlayer.ToString().Split('_')[1]));
                Power callerPowers = Power.None;
                if (callerMatch != null)
                {
                    callerPowers = callerMatch.Powers;
                }

                if (targetPlayer != null)
                {
                    if (CanAdminTarget(callerPlayer, targetPlayer))
                    {
                        if (PowerInPowers(power, callerPowers))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                if (PowerInPowers(power, callerPowers))
                {
                    return true;
                }

                return false;
            }

            public static bool CanAdminTarget(Il2Cpp.Player callerPlayer, Il2Cpp.Player targetPlayer)
            {
                Admin? callerMatch = AdminList.Find(i => i.SteamId == long.Parse(callerPlayer.ToString().Split('_')[1]));
                byte callerLevel = 0;
                if (callerMatch != null)
                {
                    callerLevel = callerMatch.Level;
                }

                Admin? targetMatch = AdminList.Find(i => i.SteamId == long.Parse(targetPlayer.ToString().Split('_')[1]));
                byte targetLevel = 0;
                if (targetMatch != null)
                {
                    targetLevel = targetMatch.Level;
                }

                if (callerLevel > 0 && callerLevel >= targetLevel)
                {
                    return true;
                }

                return false;
            }

            public static Power GetAdminPowers(Il2Cpp.Player callerPlayer)
            {
                Admin? match = AdminList.Find(i => i.SteamId == long.Parse(callerPlayer.ToString().Split('_')[1]));
                if (match != null)
                {
                    return match.Powers;
                }

                return Power.None;
            }

            public static byte GetAdminLevel(Il2Cpp.Player callerPlayer)
            {
                Admin? match = AdminList.Find(i => i.SteamId == long.Parse(callerPlayer.ToString().Split('_')[1]));
                if (match != null)
                {
                    return match.Level;
                }

                return 0;
            }

            public static bool IsAdmin(Il2Cpp.Player callerPlayer)
            {
                Admin? match = AdminList.Find(i => i.SteamId == long.Parse(callerPlayer.ToString().Split('_')[1]));
                if (match != null)
                {
                    // filter out non-admin powers
                    Power powers = match.Powers;
                    powers &= ~(Power.Slot | Power.Vote | Power.Skip | Power.Custom1 | Power.Custom2 | Power.Custom3 | Power.Custom4);

                    if (powers != Power.None)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}