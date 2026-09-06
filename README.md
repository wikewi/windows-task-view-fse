# Windows Task View - Xbox FSE

A Windows 11 task view application that replicates the Xbox Full Screen Experience (FSE) with native Xbox controller and keyboard navigation, window thumbnail capture, real-time window management, and smooth visual transitions.

![Xbox FSE Task View Banner](https://img.shields.io/badge/Platform-Windows%2011%20%7C%2010-green?logo=windows)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue?logo=dotnet)
![Architecture](https://img.shields.io/badge/Architecture-MVVM%20%2B%20WPF-purple)
![Input](https://img.shields.io/badge/Input-Xbox%20Controller%20%2B%20Keyboard%20%2B%20Mouse-darkgreen?logo=xbox)
![License](https://img.shields.io/badge/License-MIT-brightgreen)

---

## 🎮 Features

### 1. Xbox FSE Interface & Styling
- **Grid-Based Task View**: Large, high-resolution preview cards for all active desktop windows and applications.
- **Xbox Theme**: Dark background (`#0E1110`), signature Xbox green accent highlights (`#107C10`), rounded corners, and glowing ambient drop shadows.
- **Smooth Animations**: Dynamic tile scaling on focus (1.0x to 1.05x), smooth border illumination, and closing fade/shrink transitions.
- **Sound Effects**: Procedurally generated Xbox-style audio feedback for navigation, window switching, dismiss, and notifications.

### 2. Complete Input Support
- **Xbox Controller (XInput)**:
  - **D-Pad / Left Thumbstick**: 2D grid navigation with automatic repeat and deadzone handling.
  - **(A) Button**: Switch/activate focused window.
  - **(B) Button**: Dismiss/close Task View.
  - **(X) Button**: Close the selected application.
  - **(Y) Button**: Refresh running windows list.
  - **LB / RB**: Page navigation.
  - **Haptic Feedback**: Controller vibration pulse on navigation and actions.
- **Keyboard**:
  - **Arrow Keys**: 2D grid navigation with seamless wrap-around.
  - **Enter / Space**: Switch to selected window.
  - **Escape**: Close Task View.
  - **Delete / X**: Close selected window.
  - **F5**: Refresh window list.
  - **Page Up / Page Down**: Page jump navigation.
- **Mouse**:
  - Hover effects, click tile to switch window, and dedicated close button (X) on each tile.

### 3. Native Windows 11 Window Management & Interop
- **Window Enumeration**: Uses Win32 `EnumWindows`, filtering out background cloaked UWP windows, tooltips, shells, and taskbars.
- **Thumbnail Capture**: Live window capture using GDI `PrintWindow` (`PW_RENDERFULLCONTENT`) and DWM thumbnail APIs.
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
│       │   └── RelayCommand.cs             # MVVM ICommand implementations
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
│       │   ├── InputManager.cs             # Keyboard & controller dispatcher
│       │   ├── ISoundService.cs            # Sound effects contract
│       │   └── SoundService.cs             # Procedural WAV sound synthesizer
│       ├── ViewModels/
│       │   ├── ViewModelBase.cs            # INotifyPropertyChanged base
│       │   ├── WindowTileViewModel.cs      # Individual window tile ViewModel
│       │   └── MainViewModel.cs            # Main Task View ViewModel
│       ├── Views/
│       │   ├── WindowTile.xaml / .cs       # Animated window card view
│       └── WindowsTaskViewFSE.csproj
├── tests/
│   └── WindowsTaskViewFSE.Tests/
│       ├── ViewModelTests.cs               # Grid navigation, filter & commands
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
| **Page Jump** | `LB` / `RB` | `Page Up` / `Page Down` | Scroll Wheel |

---

## 📄 License
This project is licensed under the [MIT License](LICENSE).
