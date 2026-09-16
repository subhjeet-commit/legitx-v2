# Architecture

LegitX V2 is a .NET Framework 4.8.1 WinForms desktop application. `Application/Program.cs` verifies the runtime, enforces one running instance, checks elevation, and starts the main `LegitX` form. The form coordinates settings, timers, hotkeys, overlays, and user actions.

The input path begins in `Hooks/EnhancedMouseHook.cs` or `Hooks/MouseHook.cs`. Windows sends low-level mouse events to the hook callback, which forwards movement into `Input/SensitivityProcessor.cs` and `Input/MovementPredictor.cs`. The processed values are used by the UI features that need normalized movement. Hook registration and cleanup must remain paired so the process can exit without leaving a system hook behind.

`Services/OverlaySystem.cs` owns the transparent topmost overlay and foreground-window tracking. `Services/RegistryManager.cs` stores application settings and applies explicitly selected registry presets. Registry presets are embedded resources; they are temporary files passed to `regedit.exe` only after the user confirms the operation.

The project intentionally has no service, scheduled task, startup entry, browser integration, credential store reader, or telemetry client. User-triggered external links are kept in the UI because some optional downloads are maintained outside this repository.
