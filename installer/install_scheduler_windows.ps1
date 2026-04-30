<#
Jarvis Windows Task Scheduler Installer

Creates two weekday tasks:
- Jarvis Morning Briefing at 06:30 Europe/Zurich local Windows time
- Jarvis Midday Briefing at 13:15 Europe/Zurich local Windows time

Run from an elevated or normal PowerShell session on Windows.
The tasks call WSL and execute ~/jarvis/scripts/run_briefing_autopilot.sh.
#>

param(
    [string]$WslDistro = "Ubuntu",
    [string]$JarvisHome = "~/jarvis",
    [string]$MorningTime = "06:30",
    [string]$MiddayTime = "13:15"
)

$ErrorActionPreference = "Stop"

function Register-JarvisTask {
    param(
        [string]$TaskName,
        [string]$Mode,
        [string]$Time
    )

    $Action = New-ScheduledTaskAction `
        -Execute "wsl.exe" `
        -Argument "-d $WslDistro -- bash -lc 'cd $JarvisHome && ./scripts/run_briefing_autopilot.sh $Mode >> logs/scheduler_$Mode.log 2>&1'"

    $Trigger = New-ScheduledTaskTrigger `
        -Weekly `
        -DaysOfWeek Monday,Tuesday,Wednesday,Thursday,Friday `
        -At $Time

    $Settings = New-ScheduledTaskSettingsSet `
        -StartWhenAvailable `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 20)

    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $Action `
        -Trigger $Trigger `
        -Settings $Settings `
        -Description "Jarvis automated $Mode market briefing" `
        -Force | Out-Null

    Write-Host "Registered task: $TaskName at $Time"
}

Register-JarvisTask `
    -TaskName "Jarvis Morning Briefing" `
    -Mode "morning" `
    -Time $MorningTime

Register-JarvisTask `
    -TaskName "Jarvis Midday Briefing" `
    -Mode "midday" `
    -Time $MiddayTime

Write-Host "Jarvis scheduler installation completed."
Write-Host "Morning: $MorningTime, Midday: $MiddayTime, Distro: $WslDistro"
