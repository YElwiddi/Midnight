# ============================================================
#  A Graveyard Shift - Steam upload helper
#  1) Edit the two values below.
#  2) Run in PowerShell:  powershell -ExecutionPolicy Bypass -File "<this file>"
#  First run prompts for your password + Steam Guard code, then uploads.
# ============================================================

# --- EDIT THESE TWO ---
$SteamLogin   = "YOUR_STEAM_LOGIN"   # your Steamworks account username (must have "Publish App Changes" rights)
$SteamcmdPath = "C:\Users\Nikko\steamworks_sdk_164\sdk\tools\ContentBuilder\builder\steamcmd.exe"
# ----------------------

$AppScript = "C:\Users\Nikko\Documents\Midnight\AGraveyardShift\Steam\app_build.vdf"

if ($SteamLogin -eq "YOUR_STEAM_LOGIN") { Write-Host "Edit `$SteamLogin at the top of this script first." -ForegroundColor Red; exit 1 }
if (-not (Test-Path $SteamcmdPath))     { Write-Host "steamcmd not found at: $SteamcmdPath  (fix `$SteamcmdPath)" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $AppScript))        { Write-Host "VDF not found at: $AppScript" -ForegroundColor Red; exit 1 }

Write-Host "Uploading 'A Graveyard Shift'  (App 4897200 -> Depot 4897201) ..." -ForegroundColor Cyan
& $SteamcmdPath +login $SteamLogin +run_app_build $AppScript +quit
Write-Host "Done. If you saw 'Successfully finished appID 4897200 build', go set it live:" -ForegroundColor Green
Write-Host "  https://partner.steamgames.com/apps/builds/4897200"
