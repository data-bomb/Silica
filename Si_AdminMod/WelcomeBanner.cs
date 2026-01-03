/*
Silica Admin Mod
Copyright (C) 2025 by databomb

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

#if NET6_0
using Il2Cpp;
#endif

using HarmonyLib;
using System;
using System.Collections.Generic;

namespace SilicaAdminMod
{
	public partial class SiAdminMod
	{
		[HarmonyPatch(typeof(GameMode), nameof(GameMode.OnPlayerJoinedBase))]
		private static class ApplyPatch_GameMode_OnPlayerJoinedBase
		{
			public static void Postfix(GameMode __instance, Player __0)
			{
				try
				{
					if (__0 == null)
					{
						return;
					}

					string[] welcomeBanner = GenerateWelcomeBanner(__0);
					HelperMethods.SendConsoleMessageToPlayer(__0, welcomeBanner);
				}
				catch (Exception error)
				{
					HelperMethods.PrintError(error, "Failed to run GameMode::OnPlayerJoinedBase");
				}
			}
		}

		public static string[] GenerateWelcomeBanner(Player player)
		{
			List<string> banner = new List<string>();

			// TODO: add server ASCII art, if present

			// TODO: add server banner message, if present

			// divider
			banner.Add(Divider());

			// add player commands
			banner.Add(CommandsHeader());

			int commandNumber = 1;

			foreach (PlayerCommand command in PlayerMethods.PlayerCommands)
			{
				banner.Add("<color=#FFFFFF>" + commandNumber + ". <indent=6.3em>!" + command.CommandName + "</indent></color>");
				commandNumber++;
			}

			foreach (PlayerCommand command in PlayerMethods.PlayerPhrases)
			{
				banner.Add("<color=#FFFFFF>" + commandNumber + ". <indent=6.2em>" + command.CommandName + "</indent></color>");
				commandNumber++;
			}
			
			banner.Add(CommandsFooter());

			return banner.ToArray();
		}

		public static string Divider()
		{
			return " ";
		}

		public static string CommandsHeader()
		{
			return "<mspace=0.66em><color=#FFFFFF>------------- <i>Player Commands</i> -------------</color></mspace>";
		}

		public static string CommandsFooter()
		{
			return "<mspace=0.66em><color=#FFFFFF>-------------------------------------------</color></mspace>";
		}
	}
}