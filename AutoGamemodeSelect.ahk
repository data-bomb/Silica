; Silica Listen Server Mode Select AutoHotKey Script
; Copyright (C) 2023 by databomb
; 
; * Description *
; For Silica listen servers, automatically detects the round winners
; and losers and allows server to automatically select the next 
; gamemode without manual operator intervention. This is only for use
; where the host is not actively playing them game and is staring at
; the team selection screen.
;
; * Version History *
; v1.0.0 22 May 2023 Initial public release
; 
; * License *
; This program is free software: you can redistribute it and/or modify
; it under the terms of the GNU General Public License as published by
; the Free Software Foundation, either version 3 of the License, or
; (at your option) any later version.
; 
; This program is distributed in the hope that it will be useful,
; but WITHOUT ANY WARRANTY; without even the implied warranty of
; MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
; GNU General Public License for more details.
; 
; You should have received a copy of the GNU General Public License
; along with this program.  If not, see <http://www.gnu.org/licenses/>.


#Requires AutoHotkey v2.0

Logging := 1

; Validate config setup hotkey
; Test setup instructions: 
; 1) Launch Server in Humans vs. Aliens mode
; 2) Activate console
; 3) Type `cheats` in console
; 4) Type `delete structure` in console
; 5) Type `destroy` in console
;    Both Human and Alien should now have the red loss scratches
; 6) Ctrl + Alt + Z
^!z::
{
	GrabSilicaColors(&HumanSolColor, &AlienColor, &HumanCentauriColor, 1)
	
	if (HumanSolColor = 0xc00000 and AlienColor = 0xc00000)
	{
		MsgBox "Setup Validated"
	}
	else
	{
		MsgBox "Adjustments Needed! Found Alien Color: " AlienColor " Human Sol Color: " HumanSolColor
	}
}

; Main Loop to identify a losing team
Loop
{
	GrabSilicaColors(&HumanSolColor, &AlienColor, &HumanCentauriColor)
	
	if (AlienColor = 0xc00000 and HumanSolColor = 0xc00000)
	{
		MsgBox "Error. Found multiple victory conditions."
	}
	else if (AlienColor = 0xc00000)
	{
		; wait for endgame screen to disappear
		while (AlienColor = 0xc00000)
		{
			Sleep(1000)
			GrabSilicaColors(&HumanSolColor, &AlienColor, &HumanCentauriColor)
		}
		Sleep(5000)
		SelectNextGameMode()
		LogWinner(1)
	}
	else if (HumanSolColor = 0xc00000)
	{
		; wait for endgame screen to disappear
		while (HumanSolColor = 0xc00000)
		{
			Sleep(1000)
			GrabSilicaColors(&HumanSolColor, &AlienColor, &HumanCentauriColor)
		}
		Sleep(5000)
		SelectNextGameMode()
		LogWinner(2)
	}
	
	Sleep(4000)
}

; Selects from the 3 availble Silica gamemmodes
; mode 0 = "Humans vs. Humans"
; mode 1 = "Humans vs. Aliens"
; mode 2 = "Humans vs. Humans vs. Aliens"
SelectNextGameMode(mode := 1)
{
	; switch to the game's window
	FindSilicaScreen(&GameMidPosX, &GameMidPosY, &topX, &topY, 1)
	CoordMode "Mouse", "Screen"
	
	GameModeMidAdjustX := (GameMidPosX // 3)
	
	; adjust X coordinate to select one of the three gamemodes
	Switch mode
	{
		; mode 0 = "Humans vs. Humans"
		Case 0: ClickPosX := topX + GameMidPosX - GameModeMidAdjustX
		; mode 1 = "Humans vs. Aliens"
		Case 1: ClickPosX := topX + GameMidPosX
		; mode 2 = "Humans vs. Humans vs. Aliens"
		Case 2: ClickPosX := topX + GameMidPosX + GameModeMidAdjustX
	}
	
	ClickPosY := topY + GameMidPosY
	MouseMove ClickPosX, ClickPosY
	MouseClick "left"
}

; Find the color in the middle of the Human and Alien team banners
GrabSilicaColors(&HumanSolColor, &AlienColor, &HumanCentauriColor, debug:=0)
{
	; TODO: Fix later to support HvHvA
	HumanCentauriColor := 0
	
	FindSilicaScreen(&GameMidPosX, &GameMidPosY, &topX, &topY)
	
	; Find if we have 2 teams or 3 teams by looking for Alien in middle
	CheckAlienMidAdjustY := (GameMidPosY // 6)
	CheckAlienMidPosX := topX + GameMidPosX
	CheckAlienMidPosY := topY + GameMidPosY + CheckAlienMidAdjustY
	CoordMode "Pixel", "Screen"
	CheckAlienMidColor := PixelGetColor(CheckAlienMidPosX, CheckAlienMidPosY)
	if (CheckAlienMidColor = 0xFFFFFF)
	{
		; TODO: Fix later to support HvHvA
		MsgBox "Alien Team is Mid"
	}
	
	GameMidAdjustX := (GameMidPosX // 5)
	GameMidAdjustY := (GameMidPosY // 72)
	
	HumanPosX := topX + GameMidPosX - GameMidAdjustX
	AlienPosX := topX + GameMidPosX + GameMidAdjustX
	HumanPosY := topY + GameMidPosY - GameMidAdjustY
	AlienPosY := topY + GameMidPosY - GameMidAdjustY
	
	if (debug = 1)
	{
		MsgBox "Human Position Silica is at " HumanPosX "," HumanPosY " and Silica starts at " topX "," topY
	}
	
	CoordMode "Pixel", "Screen"
	HumanSolColor := PixelGetColor(HumanPosX, HumanPosY)
	AlienColor := PixelGetColor(AlienPosX, AlienPosY)
}

FindSilicaScreen(&GameMidPosX, &GameMidPosY, &topX, &topY, switch_context := 0)
{
	UniqueID := WinExist("ahk_exe Silica.exe")
	
	if (switch_context = 1)
	{
		WinActivate
	}
	
	WinGetPos(&topX, &topY, &Width, &Height, UniqueID)
	
	GameMidPosX := (Width // 2)
	GameMidPosY := (Height // 2)
}

LogWinner(Winner)
{
	if (Logging = 1)
	{
		Switch Winner
		{
			Case 1: WriteLogEntry("Human (Sol) Victory")
			Case 2: WriteLogEntry("Alien Victory")
			Case 3: WriteLogEntry("Human (Centauri) Victory")
		}
	}
}

WriteLogEntry(text)
{
	FileAppend A_NowUTC ": " text "`n", A_ScriptDir "\WinnerLog.txt"
}