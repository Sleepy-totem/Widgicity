# Widgicity

A lightweight Windows desktop app for pinning web content to your screen as transparent, always-on-top overlay widgets. Point a widget at any URL — stats pages, dashboards, live counters, streaming overlays — and it sits on top of your desktop like a native window.

## Features

- Add any number of widgets, each pointing at its own URL
- Drag and resize freely while unlocked, or lock a widget in place with click-through enabled so it doesn't interfere with what's underneath
- Per-widget opacity and zoom controls
- Duplicate or reset widgets from the dashboard
- Settings are saved automatically and reloaded on the next launch

## Requirements

- Windows 10 (21H2+) or Windows 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (only needed if you're using the framework-dependent build; the self-contained build has no external requirements)
- Microsoft Edge WebView2 Runtime (preinstalled on most modern Windows systems; Windows will prompt to install it automatically if missing)

## Getting Started

1. Download the latest build from the [Releases](../../releases) page, or build it yourself:
   ```
   dotnet publish -c Release
   ```
2. Run `Widgicity.exe`.
3. Click **+ New Widget**, enter a name and a URL, and position it on screen.
4. When you're happy with the placement, check **Lock Position & Enable Click-Through** to finalize the widget and let clicks pass through to whatever's behind it.

## Usage Notes

- Widgets are only interactive (draggable, clickable through the overlay border) while unlocked. Lock them once you're done positioning.
- Configuration is stored in `WidgicityConf.json` in your local temp folder (%temp%).
- On first launch, Widgicity initializes its web engine, which can take a few seconds — a splash screen shows progress while this happens. Subsequent launches are much faster.

## Built With

- WPF (.NET 8)
- [Microsoft Edge WebView2](https://learn.microsoft.com/microsoft-edge/webview2/)

## [License](LICENSE)

Creative Commons Zero v1.0 Universal
