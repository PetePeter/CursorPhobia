# Detail Questions

## Q6: Should the app use C# with WinForms/WPF for native Windows integration and easy compilation?
**Default if unknown:** Yes (provides excellent Windows API access, system tray support, and single-click compilation)

## Q7: Should the app require administrator privileges to install the global mouse hook?
**Default if unknown:** Yes (Windows requires elevated permissions for system-wide hooks, but we can request elevation only when needed)

## Q8: Should the configuration interface be accessible only through the system tray context menu?
**Default if unknown:** Yes (keeps the app truly background-focused with minimal UI footprint)

## Q9: Should the app detect always-on-top windows by checking the WS_EX_TOPMOST flag rather than trying to identify specific applications?
**Default if unknown:** Yes (more reliable than maintaining application lists, works with any always-on-top window regardless of source)

## Q10: Should the 5-second timer reset each time the mouse moves outside and back inside the window boundary?
**Default if unknown:** No (timer should only start when mouse first exits the window, preventing infinite postponement)