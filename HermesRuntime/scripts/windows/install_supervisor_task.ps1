param(
    [string]$WslDistro = "",
    [string]$TaskName = "Hermes Supervisor Start",
    [string]$DailyStartTime = "22:50"
)

$ErrorActionPreference = "Stop"

function New-HermesWslAction {
    param([string]$ScriptPath)

    $arguments = if ([string]::IsNullOrWhiteSpace($WslDistro)) {
        "-e bash -lc `"$ScriptPath`""
    } else {
        "-d `"$WslDistro`" -e bash -lc `"$ScriptPath`""
    }

    New-ScheduledTaskAction -Execute "wsl.exe" -Argument $arguments
}

$supervisorScript = "~/jarvis/HermesRuntime/scripts/nightly/start_supervisor.sh"
$action = New-HermesWslAction -ScriptPath $supervisorScript
$startupTrigger = New-ScheduledTaskTrigger -AtStartup
$dailyTrigger = New-ScheduledTaskTrigger -Daily -At $DailyStartTime

$settings = New-ScheduledTaskSettingsSet `
    -Hidden `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 5) `
    -StartWhenAvailable

$principal = New-ScheduledTaskPrincipal `
    -UserId $env:USERNAME `
    -LogonType S4U `
    -RunLevel Highest

try {
    $task = New-ScheduledTask -Action $action -Trigger @($startupTrigger, $dailyTrigger) -Settings $settings -Principal $principal
    Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null
} catch {
    Write-Error "Failed to register '$TaskName'. Start PowerShell as Administrator because this task uses Highest privileges and Run whether user is logged in or not. Original error: $($_.Exception.Message)"
    throw
}

Write-Host "Installed Windows Scheduled Task:"
Write-Host " - $TaskName => at startup and daily $DailyStartTime, WSL start_supervisor.sh"
Write-Host ""
Write-Host "Hermes schedules are now configured in config/schedules.json."
Write-Host "No new Windows task is required for normal Hermes job schedule changes."
Write-Host ""
Write-Host "Status:"
Get-ScheduledTask -TaskName $TaskName | Select-Object TaskName, State, TaskPath | Format-Table -AutoSize
