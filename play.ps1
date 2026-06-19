#!/usr/bin/env pwsh
# Launch the Firing Solution shell in Godot 4.3 (.NET edition).
#
#   .\play.ps1          # run the game
#   .\play.ps1 -Edit    # open the Godot editor on the project instead
#
# No environment variables needed — the Godot path is resolved below.

param(
    [switch]$Edit
)

$ErrorActionPreference = 'Stop'

$godot = Join-Path $env:USERPROFILE 'Godot\4.3-mono\Godot_v4.3-stable_mono_win64\Godot_v4.3-stable_mono_win64.exe'
if (-not (Test-Path $godot)) {
    Write-Error "Godot not found at: $godot`nRe-download the Godot 4.3 .NET edition or fix the path in play.ps1."
    exit 1
}

$project = Join-Path $PSScriptRoot 'shell\godot'

$godotArgs = @('--path', $project)
if ($Edit) { $godotArgs += '--editor' }

Write-Host ("Launching Godot {0}: {1}" -f ($(if ($Edit) {'editor'} else {'game'})), $project)
& $godot @godotArgs
