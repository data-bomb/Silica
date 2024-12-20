/*
 Silica Commander Management Mod
 Copyright (C) 2023-2024 by databomb
 
 * Description *
 For Silica servers, establishes a random selection for commander at the 
 start of each round and provides for admin commands to !demote a team's
 commander as well as !cmdrban a player from being commander in the
 future.

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
#endif

using MelonLoader;
using Si_CommanderManagement;
using System;
using SilicaAdminMod;
using System.Linq;
using System.Collections.Generic;

[assembly: MelonInfo(typeof(CommanderManager), "Commander Management", "1.9.3", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_CommanderManagement
{
    public class CommanderManager : MelonMod
    {
        static MelonPreferences_Category _modCategory = null!;
        public static MelonPreferences_Entry<bool> _BlockRoundStartUntilEnoughApplicants = null!;
        public static MelonPreferences_Entry<bool> _TeamOnlyResponses = null!;
        public static MelonPreferences_Entry<float> _MutinyVotePercent = null!;

        public static List<Player>[] mutineerPlayers = null!;

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory("Silica");
            _BlockRoundStartUntilEnoughApplicants ??= _modCategory.CreateEntry<bool>("BlockRoundStartUntilCommandersApplied", true);
            _TeamOnlyResponses ??= _modCategory.CreateEntry<bool>("CmdrMgr_CommanderResponses_TeamOnly", false);
            _MutinyVotePercent ??= _modCategory.CreateEntry<float>("CmdrMgr_Mutiny_Vote_PercentNeeded", 0.54f);

            try
            {
                CommanderBans.InitializeList();
                CommanderApplications.InitializeApplications();
                Mutineer.InitializeMutineerList();
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to load Silica commander banlist (OnInitializeMelon)");
            }
        }

        public override void OnLateInitializeMelon()
        {
            // register commands
            HelperMethods.CommandCallback commanderBanCallback = CommanderAdminCommands.Command_CommanderBan;
            HelperMethods.RegisterAdminCommand("cmdrban", commanderBanCallback, Power.Commander, "Prevents target player from applying or playing as a Commander. Usage: !cmdrban <player>");
            HelperMethods.RegisterAdminCommand("commanderban", commanderBanCallback, Power.Commander, "Prevents target player from applying or playing as a Commander. Usage: !commanderban <player>");
            HelperMethods.RegisterAdminCommand("cban", commanderBanCallback, Power.Commander, "Prevents target player from applying or playing as a Commander. Usage: !cban <player>");

            HelperMethods.CommandCallback commanderUnbanCallback = CommanderAdminCommands.Command_CommanderUnban;
            HelperMethods.RegisterAdminCommand("removecommanderban", commanderUnbanCallback, Power.Commander, "Allows target player to apply or play as a Commander. Usage: !removecommanderban <player>");
            HelperMethods.RegisterAdminCommand("uncban", commanderUnbanCallback, Power.Commander, "Allows target player to apply or play as a Commander. Usage: !uncban <player>");

            HelperMethods.CommandCallback commanderDemoteCallback = CommanderAdminCommands.Command_CommanderDemote;
            HelperMethods.RegisterAdminCommand("demote", commanderDemoteCallback, Power.Commander, "Demotes target player from their current Commander role. Usage: !demote <player>");

            HelperMethods.CommandCallback commanderCallback = CommanderApplications.Command_Commander;
            HelperMethods.RegisterPlayerCommand("commander", commanderCallback, true);

            HelperMethods.CommandCallback mutinyCallback = Mutineer.Command_Mutiny;
            HelperMethods.RegisterPlayerCommand("mutiny", mutinyCallback, true);

            // subscribe to the OnRequestCommander event
            Event_Roles.OnRequestCommander += OnRequestCommander;

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);
            QList.OptionTypes.BoolOption dontStartWithoutCommanders = new(_BlockRoundStartUntilEnoughApplicants, _BlockRoundStartUntilEnoughApplicants.Value);
            QList.Options.AddOption(dontStartWithoutCommanders);
            #endif
        }
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Mutineer.ClearMutineerList();
        }

        public void OnRequestCommander(object? sender, OnRequestCommanderArgs args)
        {
            if (args.Requester == null || CommanderBans.BanList == null)
            {
                return;
            }

            // check if player is allowed to be commander
            if (CommanderBans.IsBanned(args.Requester))
            {
                MelonLogger.Msg("Preventing " + args.Requester.PlayerName + " from playing as commander.");
                args.Block = true;
                return;
            }

            // check if they're trying to join before the 30 second countdown expires and the game begins
            if (GameMode.CurrentGameMode.Started && !GameMode.CurrentGameMode.GameBegun)
            {
                // check if player is already an applicant
                if (!CommanderApplications.IsApplicant(args.Requester))
                {
                    if (_TeamOnlyResponses.Value)
                    {
                        HelperMethods.SendChatMessageToTeam(args.Requester.Team, HelperMethods.chatPrefix, HelperMethods.GetTeamColor(args.Requester.Team), args.Requester.PlayerName, "</color> has applied for commander.");
                    }
                    else
                    {
                        HelperMethods.ReplyToCommand_Player(args.Requester, "has applied for commander");
                    }
                    
                    CommanderApplications.commanderApplicants[args.Requester.Team.Index].Add(args.Requester);
                }

                MelonLogger.Msg("Denied early game commander join for " + args.Requester.PlayerName);
                args.Block = true;
                args.PreventSpawnWhenBlocked = false;
                return;
            }
        }
    }
}
