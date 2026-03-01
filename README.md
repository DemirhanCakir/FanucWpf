# FanucWpf

A WPF desktop application for communicating with Fanuc robots using the FANUC Robot Interface (FRRJIf) library. Built with C# and the MVVM pattern targeting .NET Framework 4.8.

## Features

- **Connect / Disconnect** to a Fanuc robot over a network connection using an IP address
- **Read registers** – retrieve numeric and position register values from the robot
- **Write registers** – set numeric register values on the robot
- **Communication log** – timestamped log panel showing all interactions with the robot
- **Status indicator** – visual green/red indicator showing the current connection state

## Prerequisites

- Windows OS
- [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
- FANUC Robot Interface DLL (`Interop.FRRJIf.dll`) – provided with the FANUC ROBOGUIDE or robot controller software package. Place the DLL at the path referenced in the project (default: `Desktop\Interop.FRRJIf.dll`) or update the `HintPath` in `FanucWpf.csproj` accordingly.
- Visual Studio 2019 or later (for building from source)

## Getting Started

### Build

1. Clone the repository:
   ```bash
   git clone https://github.com/DemirhanCakir/FanucWpf.git
   ```
2. Open `FanucWpf.sln` in Visual Studio.
3. Restore NuGet packages (Visual Studio usually does this automatically on build).
4. Ensure `Interop.FRRJIf.dll` is available at the path configured in `FanucWpf.csproj`.
5. Build the solution (`Ctrl+Shift+B`).

### Run

1. Launch the built executable or press **F5** in Visual Studio.
2. Enter the robot's **IP address** in the *Robot IP* field.
3. Click **Connect** to establish a connection.
4. Use **Get** to read register values and **Set** to write register values.
5. All responses appear in the **Communication Log** panel.
6. Click **Disconnect** when finished.

## Project Structure

```
FanucWpf/
├── Commands/
│   └── RelayCommand.cs       # ICommand implementation for MVVM bindings
├── ViewModels/
│   └── MainViewModel.cs      # Main view model (MVVM)
├── FanucInterface.cs         # Robot communication layer (FRRJIf wrapper)
├── MainWindow.xaml           # Main application window UI
├── MainWindow.xaml.cs        # Code-behind for MainWindow
├── App.xaml / App.xaml.cs    # Application entry point
└── FanucWpf.csproj           # Project file
```

## Technologies

| Technology | Purpose |
|---|---|
| C# / .NET Framework 4.8 | Application runtime |
| WPF (Windows Presentation Foundation) | UI framework |
| MVVM pattern | Separation of UI and business logic |
| FRRJIf (FANUC Robot Remote Interface) | Robot communication |

## License

This project is provided as-is for educational and integration purposes. Refer to the FANUC software license terms for usage of the FRRJIf library.
