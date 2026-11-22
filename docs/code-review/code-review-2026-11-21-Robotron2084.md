# Code Review: Robotron2084 Root C# Files

## Scope

Reviewed the C# files directly under `src/Robotron2084`:

- `AssemblyInfo.cs`
- `Program.cs`
- `RobotronGame.cs`

No subdirectories or source files were changed. The review emphasizes startup, fixed-timestep update/draw behavior, lifecycle, Win32/platform assumptions, and exception paths.

## Findings

### Medium — Startup and the whole game loop have no general exception boundary

**Evidence**

- `Program.cs:5-15` catches only `PersistenceException`. The `try` contains construction and `Run()`, so a persistence error is handled, but graphics initialization, content loading, state construction, audio setup, update/draw exceptions, Win32 calls, and all other exceptions are left to MonoGame/runtime termination.
- `RobotronGame.LoadContent()` loads the font, effects, content textures, stores, and state, and calls `StartSound()`/`StartSpeech()`. Those calls can therefore fail outside the narrow persistence catch.
- `StartSound()` and `StartSpeech()` catch only `NoAudioHardwareException`; missing content, graphics/content exceptions, `OutOfMemoryException`, `DllNotFoundException`, and application bugs will propagate out of `Run()`.

**Risk**

The executable has no final log, crash screen, or controlled shutdown for most startup/runtime failures. A WinExe process can simply exit with little actionable feedback. Persistence has a dedicated modal, but this is not a general fault barrier.

**Fix guidance**

Keep the persistence-specific dialog, and add a separate outer fallback for unexpected exceptions that logs or reports the exception and performs a clean `Dispose()`. Do not catch `Exception` around individual state code unless cleanup or a known, recoverable boundary can be guaranteed. A fallback could reuse a platform-neutral console/log mechanism; if it also uses Win32, validate the OS before invoking it.

### Medium — The native persistence dialog is Win32-only and is not itself fault-tolerant

**Evidence**

- `Program.cs:21-34` P/Invokes `user32.dll!MessageBoxW`.
- `Core/DisplayInfo.cs` has separate Win32 P/Invokes for desktop geometry.
- `app.manifest` declares Windows 10/11 compatibility and per-monitor DPI awareness.
- `Robotron2084.csproj` uses `MonoGame.Framework.DesktopGL`, which is cross-platform in principle, but this application's presentation shell is explicitly Windows.

**Risk**

On a non-Windows DesktopGL host, `DllImport("user32.dll")` and `DisplayInfo` P/Invokes will fail before or during normal startup. Even on Windows, a persistence failure can occur on a path where `NativeMessageBox.Show()` should remain usable; there is currently no check or fallback for P/Invoke failure.

**Fix guidance**

Make the Windows dependency explicit if Windows is the supported target, or guard the P/Invokes and use MonoGame/platform-neutral dialogs/logging elsewhere. Validate `OperatingSystem.IsWindows()` before `NativeMessageBox.Show()` and `DisplayInfo` calls, and add a fallback error mechanism for unsupported platforms.

### Medium — Presentation resizing is dependent on a valid first backbuffer and primary-monitor geometry

**Evidence**

- `Initialize()` obtains `DisplayInfo.GetWorkArea()` and applies a windowed backbuffer using the largest integer scale that fits the primary desktop work area.
- `ToggleFullScreen()` uses `DisplayInfo.GetDesktopResolution()` and sets the backbuffer to that primary-monitor resolution.
- `ApplyBackBuffer()` calls `GraphicsDeviceManager.ApplyChanges()`, then `FitCanvas()`; `Window.ClientSizeChanged` is wired by `Initialize()`.
- `FitCanvas()` re-solves canvas placement for every draw and client-size change, so it handles resize and monitor changes only after a valid window/client area exists.

**Risk**

Primary-monitor-only sizing can center or choose the wrong scale in a multi-monitor setup. The windowed scale is fixed to the initial work area, although integer fitting is recomputed in the client area later. A monitor that cannot provide dimensions or a failed `ApplyChanges()` leaves startup/presentation without a second-stage fallback. There is also no explicit guard for a zero or invalid client dimension, although `Presentation.CanvasDestination` may be expected to handle it.

**Fix guidance**

If multi-monitor support is required, size against the monitor containing the window and store/window-dimension state explicitly. Define and test behavior for an immediate zero-sized client area, failed backbuffer application, unsupported fullscreen resolutions, and switching monitors while fullscreen.

### Low — State transitions can leave cleanup and exception semantics incomplete

**Evidence**

- `GameStateManager.TransitionTo()` calls `Close()` on an `IClosableGameState` before replacing `CurrentGameState`.
- `RobotronGame.HandleDeveloperToolsKey()`, `HandleAttractDeveloperKeys()`, `HandleStartKeys()`, and full-screen/navigation methods transition directly to new states.
- Closing states may save settings or restore shared palette/audio state.

**Risk**

If `Close()` throws, the transition aborts but the previous state remains current, so the game loop may then continue while shared state is partially closed or cleaned up. Exception cleanup is not defined by `RobotronGame`; outside the persistence catch, it becomes a runtime termination.

**Fix guidance**

Define transition rollback/cleanup semantics for closable states. At least ensure palette/audio/settings cleanup is exception-safe, and consider replacing the state only after close succeeds or unifying close/switch into a state-manager transaction with cleanup and error reporting.

### Low — Press-edge state is updated globally after presentation and developer handlers

**Evidence**

- `_previousKeyboardState` is overwritten each update after all global key handlers.
- `_previousPadOneState` and `_previousPadTwoState` are overwritten after navigation handlers.
- `WasPressed()` and `WasPadPressed()` use those fields to detect a rising edge.
- Escape calls `Exit()` but does not set or clear a pressed-state field first.

**Risk**

The shared design gives one global view of key/button edges, and state-specific input code may use its own sources independently. It appears intentional for the global handlers, but it makes pause and edge timing rely on every update receiving one snapshot. Rapid transitions in `Close()` or state replacement do not change edge state until the bottom of the update.

**Fix guidance**

Keep the state snapshot update in one place and document it as a whole-frame contract. If edge detection is moved into more state-specific code or input sources, centralize the timing rule and add tests for repeated same-frame transitions. No functional defect is established by this review.

### Low — Fixed-timestep configuration matches 60 Hz but has no elapsed-time fallback

**Evidence**

- `IsFixedTimeStep = true`.
- `TargetElapsedTime` is computed by rounding `1/60` second to `TimeSpan` ticks: `(10,000,000 + 30) / 60 = 166667` ticks.
- MonoGame calls `Update(GameTime)`/`Draw(GameTime)` in the fixed-timestep loop.

**Assessment**

The target matches MonoGame's nominal 60 Hz within one tick. It is reasonable for arcade-60 gameplay, and per-tick integer math can remain stable. MonoGame's catch-up/suppressed frame behavior is still platform-dependent at low frame rates. This file provides no explicit elapsed-time compensation or watchdog.

**Fix guidance**

No change is required for nominal operation. If frame stalls are being debugged, capture `IsRunningSlowly`, update elapsed behavior, and test 30/60/120 Hz host scheduling rather than changing the target without data.

## Lifecycle Summary

- `Program.cs` creates `RobotronGame` in a `using` declaration, runs MonoGame, and disposes it after `Run()` returns or throws.
- `RobotronGame` owns the graphics manager, canvas, sprite batch/font, palette, texture set, input, settings, high-score store, and state manager.
- Content and shared services are initialized once during `LoadContent()`, with title state created immediately afterward.
- `Escape` exits the MonoGame loop; state transitions replace the active state and optionally close the old state.
- Sound and speech initialize separately and degrade only for `NoAudioHardwareException`.
- Persistence failures are wrapped as `PersistenceException` and displayed through a modal; missing files intentionally use factory values.

## Overall Assessment

The startup and loop are compact and the service ownership is clear. The primary hardening gap is that only persistence has a deliberate exception path: all other startup, content, graphics, Win32, state, and runtime exceptions can terminate the WinExe without a useful fallback. The application is also explicitly Windows-dependent despite its DesktopGL package and should either document/validate that boundary or isolate Win32 calls behind platform-neutral fallbacks.
