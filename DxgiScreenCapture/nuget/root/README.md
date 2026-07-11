# README.md

## Build

Build the entire solution with MSBuild or Visual Studio:

```powershell
msbuild DxgiCapture.sln /p:Configuration=Release
msbuild DxgiCapture.sln /p:Configuration=Debug
```

Build a single project:

```powershell
msbuild DxgiScreenCapture\DxgiScreenCapture.csproj /p:Configuration=Release
msbuild DxgiScreenCaptureCore\DxgiScreenCaptureCore.csproj /p:Configuration=Debug
```

There are no automated tests in this repository.

## NuGet Publishing

Publishing is done via `DxgiScreenCapture\deploy_nuget.bat`. Pass `local` to build Debug and copy to `D:\myNuget` without pushing to nuget.org; omit the argument for a Release build that pushes to nuget.org. Requires `nuget.exe` on PATH and a NuGet API key stored at `D:\settings\nuget_key.txt`.

## Architecture

### Projects

- **`DxgiScreenCapture`** — Main class library targeting .NET Framework 4.6.1. Produces `DxgiScreenCapture.dll`.
- **`DxgiScreenCaptureCore`** — .NET 8.0-windows port. Does **not** duplicate source files; it links them from `DxgiScreenCapture` via `<Compile Link="...">`. Any source change in `DxgiScreenCapture\` applies to both builds automatically.
- **`sample/WindowsFormsApp1`** — WinForms demo app showing how to wire up `ScreenCapture`.

### Class responsibilities

| Class | Role |
|---|---|
| `CaptureSource` | Enum selecting which output to capture (`Monitor1`, `Monitor2`, `Monitor3`, `Desktop`) plus DXGI error constants |
| `DXGIManager` | Enumerates DXGI adapters/outputs on construction; exposes `Capture()` which acquires a frame from the selected output and calls the caller-supplied copy delegate |
| `DXGIOutputDuplication` | Wraps one monitor's `OutputDuplication`; acquires a frame, copies it into a staging `Texture2D`, maps the surface, and invokes the copy callback. Uses `#if NET8_0_OR_GREATER` to call `TryAcquireNextFrame` instead of `AcquireNextFrame` |
| `RenderTarget2D` | Creates a D3D11 swap chain + D2D1 render target bound to an HWND; exposes `Render()` for drawing and `CreateBitmap()` to wrap a raw `DataStream` into a `SharpDX.Direct2D1.Bitmap` |
| `ScreenCapture` | Public entry point. Spawns a background capture thread that blocks on `EventWaitHandle.WaitAny`. Call `SignalToCapture()` to trigger one capture; subscribe to `Captured` to receive `CapturedEventArgs` |

### Capture flow

```
SignalToCapture()
  → sets _captureEvent
  → capture thread wakes
  → DXGIManager.Capture() acquires next frame
  → copyFrameBuffer() copies BGRA pixels into _dataStream
  → RenderTarget2D.CreateBitmap() wraps _dataStream
  → bitmap queued → DrawToWindow() renders it + fires Captured event
```

### Key implementation details

- Pixel format is **BGRA** (`B8G8R8A8_UNorm`), not RGBA. The commented-out `copyFrameBufferBGRAtoRGBA` in `ScreenCapture.cs` shows how to swap channels if needed.
- `_renderTarget.Initialize()` must be called from the **capture thread** (not the UI thread) to avoid "Cross-thread operation not valid" when accessing `controlToDraw.Handle` — the window handle is therefore passed as `IntPtr` captured before the thread starts.
- On `DXGI_ERROR_ACCESS_LOST` (e.g. desktop switch via Ctrl+Shift+Alt), the capture thread recreates `DXGIManager` in a loop rather than aborting.
- On `E_ACCESS_DENIED` during output duplication setup (can occur right after a desktop restore), `DXGIManager` disposes itself and `Initialized` returns `false`, causing the thread to retry after 500 ms.
- `DxgiScreenCaptureCore` uses `#if NET8_0_OR_GREATER` guards inside the shared source to handle API differences between .NET Framework and .NET 8.
