param(
    [string]$WslDistro = "",
    [string]$TaskName = "Jarvis Startup Orchestrator",
    [switch]$RunElevated
)

$ErrorActionPreference = "Stop"

function New-JarvisWslAction {
    param([string]$ScriptPath)

    $arguments = if ([string]::IsNullOrWhiteSpace($WslDistro)) {
        "-e bash -lc `"$ScriptPath`""
    } else {
        "-d `"$WslDistro`" -e bash -lc `"$ScriptPath`""
    }

    New-ScheduledTaskAction -Execute "wsl.exe" -Argument $arguments
}

function Install-JarvisTaskWithSchtasks {
    param(
        [string]$TaskName,
        [string]$ScriptPath,
        [string]$WslDistro,
        [bool]$RunElevated
    )

    $taskRun = if ([string]::IsNullOrWhiteSpace($WslDistro)) {
        "wsl.exe -e bash -lc `"$ScriptPath`""
    } else {
        "wsl.exe -d `"$WslDistro`" -e bash -lc `"$ScriptPath`""
    }

    $runLevel = if ($RunElevated) { "HIGHEST" } else { "LIMITED" }
    $escapedTaskName = $TaskName.Replace('"', '\"')
    $escapedTaskRun = $taskRun.Replace('"', '\"')
    $arguments = "/Create /TN `"$escapedTaskName`" /SC ONLOGON /TR `"$escapedTaskRun`" /RL $runLevel /F"

    $process = Start-Process -FilePath "schtasks.exe" -ArgumentList $arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "schtasks.exe failed with exit code $($process.ExitCode)"
    }
}

$startupScript = "~/jarvis/scripts/startup/start_jarvis.sh"
$action = New-JarvisWslAction -ScriptPath $startupScript
$trigger = New-ScheduledTaskTrigger -AtLogOn

$settings = New-ScheduledTaskSettingsSet `
    -Hidden `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 5) `
    -StartWhenAvailable

try {
    if ($RunElevated.IsPresent) {
        $principal = New-ScheduledTaskPrincipal `
            -UserId $env:USERNAME `
            -LogonType Interactive `
            -RunLevel Highest
        $task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal
        Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null
        $runLevel = "Highest"
    } else {
        Register-ScheduledTask `
            -TaskName $TaskName `
            -Action $action `
            -Trigger $trigger `
            -Settings $settings `
            -Description "Starts the Jarvis Startup Orchestrator in WSL at user logon." `
            -Force | Out-Null
        $runLevel = "CurrentUser"
    }
} catch {
    Write-Warning "Register-ScheduledTask failed, trying schtasks.exe fallback. Original error: $($_.Exception.Message)"
    try {
        Install-JarvisTaskWithSchtasks -TaskName $TaskName -ScriptPath $startupScript -WslDistro $WslDistro -RunElevated $RunElevated.IsPresent
        $runLevel = if ($RunElevated.IsPresent) { "Highest" } else { "Limited" }
    } catch {
        Write-Error "Failed to register '$TaskName'. Start PowerShell as Administrator or check Windows Task Scheduler policy. Original fallback error: $($_.Exception.Message)"
        throw
    }
}

Write-Host "Installed Windows Scheduled Task:"
Write-Host " - $TaskName => at user logon, WSL start_jarvis.sh"
Write-Host " - RunLevel: $runLevel"
Write-Host ""
Write-Host "Windows starts one Jarvis Startup Orchestrator task only."
Write-Host "Hermes Supervisor, Scheduler, Bridge and Control Center startup are handled inside WSL."
Write-Host "Hermes job schedules remain internal in HermesRuntime/config/schedules.json."
Write-Host ""
Write-Host "Status:"
Get-ScheduledTask -TaskName $TaskName | Select-Object TaskName, State, TaskPath | Format-Table -AutoSize
