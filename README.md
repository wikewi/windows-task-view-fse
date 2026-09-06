# Windows Task View - Xbox FSE

A Windows 11 task view application that replicates the Xbox Full Screen Experience (FSE) with native Xbox controller and keyboard navigation, a 3-window carousel with live DWM window previews, real-time window management, and smooth visual transitions.

![Xbox FSE Task View Banner](https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-green?logo=windows)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue?logo=dotnet)
![Architecture](https://img.shields.io/badge/Architecture-MVVM%20%2B%20WPF-purple)
![Input](https://img.shields.io/badge/Input-Xbox%20Controller%20%2B%20Keyboard%20%2B%20Mouse-darkgreen?logo=xbox)
![License](https://img.shields.io/badge/License-MIT-brightgreen)

---

## 🎮 Features

### 1. Xbox FSE Interface & Styling
- **3-Window Carousel**: Exactly 3 tiles on screen at a time — previous (left), active/focused (center), and next (right) — matching the Xbox FSE task switcher.
- **Live Window Previews**: The visible tiles show live, continuously-updating window content via DWM thumbnail composition (`DwmRegisterThumbnail`), not static screenshots.
- **Xbox Theme**: Pure black background (`#000000`) with a cyan (`#00D4FF`) focus border on the center tile, rounded corners, and dimmed/scaled-down side tiles.
- **Smooth Animations**: Dynamic tile scaling/dimming on focus change and closing fade/shrink transitions.

### 2. Complete Input Support
- **Xbox Controller (XInput)**:
  - **D-Pad / Left Thumbstick**: Move to the previous/next window in the carousel, with automatic repeat and deadzone handling.
  - **(A) Button**: Switch/activate focused (center) window.
  - **(B) Button**: Dismiss/close Task View.
  - **(X) Button**: Close the focused application.
  - **(Y) Button**: Refresh running windows list.
  - **LB / RB**: Fast paging — skip multiple windows at once.
  - **Haptic Feedback**: Controller vibration pulse on navigation and actions.
- **Keyboard**:
  - **Arrow Keys**: Move to the previous/next window in the carousel, with seamless wrap-around.
  - **Enter / Space**: Switch to the focused (center) window.
  - **Escape**: Close Task View.
  - **Delete / X**: Close the focused window.
  - **F5**: Refresh window list.
  - **Page Up / Page Down**: Fast paging — skip multiple windows at once.
- **Mouse**:
  - Hover effects, click tile to switch window, and dedicated close button (X) on each tile.

### 3. Native Windows 11 Window Management & Interop
- **Window Enumeration**: Uses Win32 `EnumWindows`, filtering out background cloaked UWP windows, tooltips, shells, and taskbars.
- **Live Window Previews**: The 3 visible carousel tiles are kept live via `DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`, so the compositor renders the actual window content in real time (a static GDI `PrintWindow` capture is only used as a brief fallback before the live preview attaches).
- **Icon Extraction**: Extracts high-resolution icons from window handles (`WM_GETICON`, `GetClassLongPtr`) and application binaries (`ExtractIconEx`).
- **Foreground Switching**: Windows 11 thread-input attaching (`AttachThreadInput` & `SetForegroundWindow`) to ensure instant focus transfer.
- **Real-Time Monitoring**: Uses `SetWinEventHook` and polling to detect newly launched or closed applications.

---

## 📂 Project Structure

```
windows-task-view-fse/
├── src/
│   └── WindowsTaskViewFSE/
│       ├── App.xaml / App.xaml.cs          # Application entry point
│       ├── MainWindow.xaml / .cs           # Xbox FSE Task View window & HUD
│       ├── Helpers/
│       │   ├── NativeInterop.cs            # Win32, DWM, GDI, and Shell APIs
│       │   ├── XInputInterop.cs            # XInput 1.4 / 1.3 / 9.1.0 interop
│       │   ├── RelayCommand.cs             # MVVM ICommand implementations
│       │   └── NullToVisibilityConverter.cs # Hides empty carousel slots
│       ├── Models/
│       │   └── WindowInfo.cs               # Window metadata model
│       ├── Services/
│       │   ├── IWindowManager.cs           # Window management contract
│       │   ├── WindowManager.cs            # Window enum, switch, close & monitor
│       │   ├── IThumbnailProvider.cs       # Thumbnail capture contract
│       │   ├── ThumbnailProvider.cs        # GDI / DWM thumbnail & icon extraction
│       │   ├── IControllerInputService.cs  # Controller service contract
│       │   ├── ControllerInputService.cs   # XInput polling, deadzones & haptics
│       │   ├── IInputManager.cs            # Unified input manager contract
│       │   └── InputManager.cs             # Keyboard & controller dispatcher
│       ├── ViewModels/
│       │   ├── ViewModelBase.cs            # INotifyPropertyChanged base
│       │   ├── WindowTileViewModel.cs      # Individual window tile ViewModel
│       │   └── MainViewModel.cs            # Main Task View ViewModel
│       ├── Views/
│       │   ├── WindowTile.xaml / .cs       # Animated window card view
│       └── WindowsTaskViewFSE.csproj
├── tests/
│   └── WindowsTaskViewFSE.Tests/
│       ├── ViewModelTests.cs               # Carousel navigation, filter & commands
│       ├── WindowInfoTests.cs              # Model & display title logic
│       ├── InputManagerTests.cs            # Keyboard & controller mapping
│       ├── ControllerInputTests.cs         # XInput constants & events
│       └── WindowsTaskViewFSE.Tests.csproj
├── WindowsTaskViewFSE.sln
├── README.md
├── LICENSE
└── .gitignore
```

---

## 🚀 Building & Running

### Prerequisites
- Windows 10 (1809+) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- Visual Studio 2022 (with .NET desktop development workload) or Visual Studio Code / Rider

### Build from Command Line
```powershell
# Clone the repository
git clone https://github.com/wikewi/windows-task-view-fse.git
cd windows-task-view-fse

# Restore & Build
dotnet restore
dotnet build -c Release

# Run the Application
dotnet run --project src/WindowsTaskViewFSE/WindowsTaskViewFSE.csproj
```

### Running Tests
```powershell
dotnet test tests/WindowsTaskViewFSE.Tests/WindowsTaskViewFSE.Tests.csproj
```

---

## 🎮 Controls Reference

| Action | Controller | Keyboard | Mouse |
| :--- | :--- | :--- | :--- |
| **Navigate** | D-Pad / Left Stick | Arrow Keys | Hover / Click |
| **Switch Window** | (A) Button | `Enter` / `Space` | Click Tile |
| **Close Task View** | (B) Button | `Escape` | - |
| **Close Application** | (X) Button | `Delete` / `X` | Click (X) Button |
| **Refresh Apps** | (Y) Button | `F5` | - |
| **Fast Page (skip windows)** | `LB` / `RB` | `Page Up` / `Page Down` | - |

---

## 📄 License
This project is licensed under the [MIT License](LICENSE).
