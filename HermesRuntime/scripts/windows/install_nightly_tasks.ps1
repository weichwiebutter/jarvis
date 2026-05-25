param(
    [string]$WslDistro = "",
    [string]$StartTaskName = "Hermes Nightly Beta3 Start",
    [string]$StopTaskName = "Hermes Nightly Beta3 Stop"
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

$startScript = "~/jarvis/HermesRuntime/scripts/nightly/start_beta3.sh"
$stopScript = "~/jarvis/HermesRuntime/scripts/nightly/stop_beta3.sh"

$startAction = New-HermesWslAction -ScriptPath $startScript
$stopAction = New-HermesWslAction -ScriptPath $stopScript

$startTrigger = New-ScheduledTaskTrigger -Daily -At "22:55"
$stopTrigger = New-ScheduledTaskTrigger -Daily -At "05:05"

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

function Register-HermesTask {
    param(
        [string]$TaskName,
        [Microsoft.Management.Infrastructure.CimInstance]$Task
    )

    try {
        Register-ScheduledTask -TaskName $TaskName -InputObject $Task -Force | Out-Null
    } catch {
        Write-Error "Failed to register '$TaskName'. Start PowerShell as Administrator because these tasks use Highest privileges and Run whether user is logged in or not. Original error: $($_.Exception.Message)"
        throw
    }
}

$startTask = New-ScheduledTask -Action $startAction -Trigger $startTrigger -Settings $settings -Principal $principal
$stopTask = New-ScheduledTask -Action $stopAction -Trigger $stopTrigger -Settings $settings -Principal $principal

Register-HermesTask -TaskName $StartTaskName -Task $startTask
Register-HermesTask -TaskName $StopTaskName -Task $stopTask

Write-Host "Installed Windows Scheduled Tasks:"
Write-Host " - $StartTaskName => 22:55 daily, WSL start_beta3.sh"
Write-Host " - $StopTaskName => 05:05 daily, WSL stop_beta3.sh"
Write-Host ""
Write-Host "Status:"
Get-ScheduledTask -TaskName $StartTaskName, $StopTaskName | Select-Object TaskName, State, TaskPath | Format-Table -AutoSize
