# SwimStats Troubleshooting Guide

## Application doesn't start / Nothing happens when clicking SwimStats.App.exe

### Quick Fix: Use the batch file launcher
1. Double-click `Launch-SwimStats.bat` instead of the exe
2. This will show any error messages
3. The window will stay open so you can read the errors

### 1. Unblock the executable (Windows Security) **[MOST COMMON SOLUTION]**
Windows may block downloaded files for security:

1. Right-click on `SwimStats.App.exe`
2. Select **Properties**
3. At the bottom, look for "Security: This file came from another computer..."
4. Check the **Unblock** checkbox
5. Click **Apply** then **OK**
6. Try running the application again

### 2. Check antivirus software
Some antivirus programs silently block executables:
- Temporarily disable your antivirus
- Or add `SwimStats.App.exe` to the antivirus exceptions/whitelist

### 3. Check the error log
The application creates a log file that may contain error details:

1. Press `Windows + R`
2. Type: `%LOCALAPPDATA%\SwimStats`
3. Press Enter
4. Open `startup.log` (if it exists) with Notepad
5. Check for error messages

### 4. Run from Command Prompt to see errors
1. Open Command Prompt (cmd.exe)
2. Navigate to the folder containing SwimStats.App.exe
3. Type: `SwimStats.App.exe`
4. Press Enter
5. Look for any error messages

### 5. Install Visual C++ Redistributable (if needed)
Some native libraries may require Visual C++ runtime:
- Download: https://aka.ms/vs/17/release/vc_redist.x64.exe
- Install and restart

### 6. Check Windows Event Viewer
1. Press `Windows + R`
2. Type: `eventvwr.msc`
3. Press Enter
4. Navigate to: **Windows Logs** > **Application**
5. Look for recent errors related to SwimStats

## Still not working?

Please report the issue at: https://github.com/HotWingHeist/SwimStats/issues

Include:
- Windows version (e.g., Windows 10, Windows 11)
- Contents of `%LOCALAPPDATA%\SwimStats\startup.log` (if exists)
- Any error messages from Event Viewer
- Whether antivirus is running
