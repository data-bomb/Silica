@ECHO OFF
SETLOCAL EnableExtensions
SET "EXE_PATH=Silica.exe"
SET "ADDITIONAL_ARGS=--nogpu --batchmode --nographics --render-offscreen --screen-width 0 --screen-height 0 --fullscreen 0 --assetbundle-compression LZ4 --target-frame-rate 48 --noaudio"
SET "MELON_ARGS=--melonloader.hideconsole --melonloader.disablestartscreen"
:RESTART_LOOP
start "" /B /HIGH /AFFINITY 0x0000000000000FFF %EXE_PATH% %MELON_ARGS% %ADDITIONAL_ARGS%
@ECHO OFF
timeout /t 86400 /nobreak > NUL
taskkill /IM "Silica.exe" /F
goto RESTART_LOOP
ENDLOCAL
