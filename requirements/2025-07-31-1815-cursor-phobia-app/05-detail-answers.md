# Detail Answers

## Q6: Should the app use C# with WinForms/WPF for native Windows integration and easy compilation?
**Answer:** Yes - C# provides excellent Windows API access, system tray support, and easy compilation

## Q7: Should the app require administrator privileges to install the global mouse hook?
**Answer:** Run as regular user but prompt for elevation when installing hooks

## Q8: Should the configuration interface be accessible only through the system tray context menu?
**Answer:** Yes - purely background utility with system tray-only interface

## Q9: Should the app detect always-on-top windows by checking the WS_EX_TOPMOST flag rather than trying to identify specific applications?
**Answer:** Yes - use WS_EX_TOPMOST flag as primary detection, but also support specific app matching and exclusion lists

## Q10: Should the 5-second timer reset each time the mouse moves outside and back inside the window boundary?
**Answer:** No - timer starts when mouse first exits window and doesn't reset on re-entry