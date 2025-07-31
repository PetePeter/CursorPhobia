# Initial Request

**Date:** 2025-07-31 18:15:39 AUSEST

## User Request

I am creating a new app. it will run on windows. it needs to be compiled easily.

The app will run in the background, with a toolbar notification.
THe app will track mouse cursor movements globally. When the mouse cursor approaches a window that is set to be "always on top" (by some other application) then  it will push the window away from it. IF the window reaches the end of the window it moves to another position. perhaps the other side of the monitor (like it warps over)

IF the CTRL key is pressed while the mouse approaches the always on top window then it does NOT push it away. Once the mouse is over the always on top window then it does not push it away unless it moves outside of the window for say .. 5 seconds

## Initial Analysis
- Windows desktop application
- Background service with system tray
- Global mouse tracking
- Window manipulation of "always on top" windows
- Keyboard modifier (CTRL) detection
- Timer-based logic for window interactions