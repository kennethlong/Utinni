<#
.SYNOPSIS
Polls for hidden SWGEmu error dialogs and surfaces them.

.DESCRIPTION
Some error dialogs from SWGEmu's WinMain path appear as detached top-level
windows that don't make it into Alt+Tab (likely WS_POPUP without WS_EX_APPWINDOW,
or modal to a parent the editor host obscures). This script polls every 2 seconds
for any SWGEmu-process window with title containing "Error" / "Failed" / etc.,
brings it to the foreground, and prints its title + child-control text to the
console so you don't have to find it manually.

Run this AFTER kicking off Launcher.exe; leave it running until the error appears
or you confirm the launch succeeded.

.PARAMETER TimeoutSeconds
How long to keep polling. Default: 180 (3 minutes).

.PARAMETER PollIntervalMs
Polling cadence in milliseconds. Default: 1500.
#>

[CmdletBinding()]
param(
  [int]$TimeoutSeconds = 180,
  [int]$PollIntervalMs = 1500
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
public class Win32Win {
  [DllImport("user32.dll")]
  public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll", CharSet=CharSet.Auto)]
  public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
  [DllImport("user32.dll", CharSet=CharSet.Auto)]
  public static extern int GetWindowTextLength(IntPtr hWnd);
  [DllImport("user32.dll")]
  public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
  [DllImport("user32.dll")]
  public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")]
  public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")]
  public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
}
"@ -ErrorAction SilentlyContinue

function Get-SwgemuWindows {
  $found = New-Object 'System.Collections.Generic.List[object]'
  $cb = [Win32Win+EnumWindowsProc] {
    param([IntPtr]$hWnd, [IntPtr]$_lp)
    $pid = [uint32]0
    [void][Win32Win]::GetWindowThreadProcessId($hWnd, [ref]$pid)
    try {
      $proc = Get-Process -Id $pid -ErrorAction Stop
      if ($proc.Name -match 'SWGEmu|SwgClient') {
        $len = [Win32Win]::GetWindowTextLength($hWnd)
        $sb = New-Object Text.StringBuilder ($len + 1)
        [void][Win32Win]::GetWindowText($hWnd, $sb, $sb.Capacity)
        $found.Add([pscustomobject]@{
          Handle = $hWnd
          Pid = $pid
          ProcessName = $proc.Name
          Title = $sb.ToString()
          Visible = [Win32Win]::IsWindowVisible($hWnd)
        })
      }
    } catch {}
    return $true
  }
  [void][Win32Win]::EnumWindows($cb, [IntPtr]::Zero)
  return $found
}

function Get-ChildText($parentHandle) {
  $texts = New-Object 'System.Collections.Generic.List[string]'
  $cb = [Win32Win+EnumWindowsProc] {
    param([IntPtr]$hWnd, [IntPtr]$_lp)
    $len = [Win32Win]::GetWindowTextLength($hWnd)
    if ($len -gt 0) {
      $sb = New-Object Text.StringBuilder ($len + 1)
      [void][Win32Win]::GetWindowText($hWnd, $sb, $sb.Capacity)
      $t = $sb.ToString().Trim()
      if ($t) { $texts.Add($t) }
    }
    return $true
  }
  [void][Win32Win]::EnumChildWindows($parentHandle, $cb, [IntPtr]::Zero)
  return $texts -join " | "
}

Write-Host "Polling for SWGEmu windows (timeout: ${TimeoutSeconds}s, interval: ${PollIntervalMs}ms)..." -ForegroundColor Cyan
Write-Host "Watching for titles matching: Error, Failed, Could not, Assertion, Fatal, Warning" -ForegroundColor Gray
Write-Host ""

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$seen = @{}
$reported = @{}

while ((Get-Date) -lt $deadline) {
  $wins = Get-SwgemuWindows
  foreach ($w in $wins) {
    $key = "$($w.Handle)-$($w.Title)"
    if (-not $seen.ContainsKey($key)) {
      $seen[$key] = $true
      Write-Host "[$(Get-Date -Format 'HH:mm:ss')] SWGEmu window: PID=$($w.Pid) Title='$($w.Title)' Visible=$($w.Visible)" -ForegroundColor Gray
    }
    # Flag suspicious titles
    if ($w.Title -match 'Error|Failed|Could not|Assertion|Fatal|Warning|Cannot' -and -not $reported.ContainsKey($key)) {
      $reported[$key] = $true
      Write-Host ""
      Write-Host "================================================================" -ForegroundColor Red
      Write-Host "  HIDDEN DIALOG FOUND" -ForegroundColor Red
      Write-Host "  PID:     $($w.Pid)" -ForegroundColor Red
      Write-Host "  Title:   $($w.Title)" -ForegroundColor Red
      Write-Host "  Handle:  $($w.Handle)" -ForegroundColor Red
      $childText = Get-ChildText $w.Handle
      if ($childText) {
        Write-Host "  Content: $childText" -ForegroundColor Yellow
      }
      Write-Host "================================================================" -ForegroundColor Red
      Write-Host ""
      Write-Host "Attempting to bring dialog to foreground..." -ForegroundColor Cyan
      [void][Win32Win]::SetForegroundWindow($w.Handle)
    }
  }
  Start-Sleep -Milliseconds $PollIntervalMs
}

Write-Host ""
Write-Host "Polling window expired. Total SWGEmu windows seen: $($seen.Count)" -ForegroundColor Cyan
