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

using HarmonyLib;
using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Newtonsoft.Json.Linq;
using MelonLoader.ICSharpCode.SharpZipLib.Core;
using System.Runtime.CompilerServices;

#if NET6_0
using Il2Cpp;
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace SilicaAdminMod
{
    public class OnRequestEnterUnitArgs : EventArgs
    {
        private Player _player = null!;
        private Unit _unit = null!;

        public Player Player
        {
            get => _player;
            set => _player = value ?? throw new ArgumentNullException("Player is required.");
        }

        public Unit Unit
        {
            get => _unit;
            set => _unit = value ?? throw new ArgumentNullException("Unit is required.");
        }

        public bool AsDriver
        {
            get;
            set;
        }

        public bool Block
        {
            get;
            set;
        }
    }

    public class OnRequestCommanderArgs : EventArgs
    {
        private Player _requester = null!;

        public Player Requester
        {
            get => _requester;
            set => _requester = value ?? throw new ArgumentNullException("Requesting player is required.");
        }
        public bool Block
        {
            get;
            set;
        }

        public bool PreventSpawnWhenBlocked
        {
            get;
            set;
        }
    }

    public class OnRoleChangedArgs : EventArgs
    {
        private Player _player = null!;

        public Player Player
        {
            get => _player;
            set => _player = value ?? throw new ArgumentNullException("Player is required.");
        }
        public MP_Strategy.ETeamRole Role
        {
            get;
            set;
        }
    }

    public class  OnRequestPlayerChatArgs : EventArgs
    {
        private Player _player = null!;
        private string _text = null!;

        public Player Player
        {
            get => _player;
            set => _player = value ?? throw new ArgumentNullException("Player is required.");
        }

        public string Text
        { 
            get => _text;
            set => _text = value ?? throw new ArgumentNullException("Text cannot be empty.");
        }

        public bool TeamOnly
        {
            get;
            set;
        }

        public bool Block
        {
            get;
            set;
        }
    }
}
