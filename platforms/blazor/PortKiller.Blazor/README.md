# PortKiller Blazor Web Version

A powerful cross-platform port management web application built with Blazor Server. Monitor ports, manage Cloudflare Tunnels, and control processes from your browser.

## About

PortKiller Blazor Web Version is developed by 科控物联. It provides a web-based interface for developers to monitor, manage, and control network ports, processes, and Cloudflare Tunnels across Windows, macOS, and Linux operating systems.

**中文用户支持：科控物联 | QQ: 2492123056**

## Features

### Port Management
- 🔍 **Auto-discovery**: Automatically discovers all listening TCP ports
- ⚡ **One-click termination**: Kill processes with a single click (graceful + force kill)
- 🔄 **Auto-refresh**: Configurable refresh interval for real-time monitoring
- 🔎 **Search & Filter**: Quick search by port number or process name
- ⭐ **Favorites**: Mark important ports as favorites for quick access
- 👁 **Watched Ports**: Monitor specific ports with notifications
- 📂 **Smart Categorization**: Automatic categorization (Web Server, Database, Development, System)
- 📊 **Table View**: Switch between card and table views for better data visualization
- 🗑 **Batch Operations**: Select and manage multiple ports at once
- 📁 **Process Information**: View process path and directory information

### Cloudflare Tunnels
- ☁️ **Tunnel Management**: View and manage active Cloudflare Tunnel connections
- 🌐 **Quick Status**: Real-time tunnel status monitoring
- 🚀 **Auto-start**: Automatically restart tunnels on application startup
- 📊 **Tunnel Statistics**: View detailed tunnel information and statistics
- 🔄 **Auto-refresh**: Automatic tunnel status updates

### Cross-Platform
- 🌐 **Web-based UI**: Access from any modern browser
- 🎨 **Modern UI**: Material Design with MASA Blazor components
- 🌓 **Theme Support**: Dark and light theme options
- 📱 **Responsive Design**: Works on desktop and mobile devices
- 🔔 **Notifications**: Real-time notifications for important events
- 📜 **Notification History**: View past notifications

## Requirements

- .NET 10 Runtime
- Modern web browser (Chrome, Firefox, Safari, Edge)
- Administrator/root privileges (required to kill processes)
- Cloudflared (optional, for tunnel functionality)

## Installation

### Option 1: Run from Source

1. Clone the repository:
```bash
git clone https://github.com/productdevbook/port-killer.git
cd port-killer/platforms/blazor/PortKiller.Blazor
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Run the application:
```bash
dotnet run
```

4. Open your browser and navigate to `http://localhost:5000`

### Option 2: Build for Production

```bash
dotnet publish -c Release -o ./publish
```

Then host the published files on any web server that supports ASP.NET Core.

### Option 3: Docker (Coming Soon)

```bash
docker build -t portkiller-blazor .
docker run -p 5000:5000 portkiller-blazor
```

## Usage

### Port Management

#### Viewing Ports
1. Open the application in your browser
2. Navigate to the "Ports" page
3. View all active ports with their associated processes
4. Switch between card view and table view using the toggle button

#### Terminating a Process
1. Find the port you want to terminate
2. Click the "Kill" button next to the port
3. Confirm the action in the dialog
4. The process will be terminated gracefully

#### Adding to Favorites
1. Hover over a port card or table row
2. Click the star icon to add/remove from favorites
3. Favorite ports appear at the top of the list

#### Batch Operations
1. Select multiple ports using the checkboxes
2. Use the batch action buttons to:
   - Kill all selected processes
   - Add all to favorites
   - Remove all from favorites

### Cloudflare Tunnels

#### Creating a Tunnel
1. Navigate to the "Tunnels" page
2. Click the "Create Tunnel" button
3. Enter the port number and tunnel name
4. Click "Create" to start the tunnel
5. The tunnel URL will be displayed once connected

#### Managing Tunnels
- **Stop Tunnel**: Click the stop button to terminate a tunnel
- **Restart Tunnel**: Click the restart button to restart a stopped tunnel
- **View Details**: Click on a tunnel to view detailed information
- **Auto-refresh**: Tunnels are automatically refreshed every 5 seconds

#### Cloudflared Setup
1. Download and install Cloudflared from [Cloudflare's website](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/)
2. The application will automatically detect Cloudflared installation
3. Cloudflared version is checked every 5 minutes to avoid performance impact

### Notifications

The application provides notifications for:
- Port status changes
- Tunnel connection/disconnection
- Cloudflared version updates
- Error messages and warnings

View notification history in the notification panel.

## Architecture

### Technology Stack

- **Framework**: .NET 10 with Blazor Server
- **UI Library**: MASA Blazor (Material Design)
- **Language**: C# 12
- **State Management**: Blazor Server State Management
- **Process Management**: System.Diagnostics.Process
- **Tunnel Management**: Cloudflared CLI integration

### Project Structure

```
PortKiller.Blazor/
├── Pages/              # Razor pages
│   ├── Index.razor             # Home page
│   ├── Ports.razor             # Port management page
│   ├── Tunnels.razor           # Tunnel management page
│   └── About.razor             # About page
├── Services/           # Business logic services
│   ├── PortService.cs           # Port scanning and management
│   ├── TunnelService.cs         # Cloudflare tunnel management
│   ├── NotificationService.cs    # Notification system
│   ├── SettingsService.cs       # Persistent settings
│   └── ThemeService.cs        # Theme management
├── Models/             # Data models
│   ├── PortInfo.cs              # Port information model
│   ├── CloudflareTunnel.cs      # Tunnel information model
│   └── Notification.cs           # Notification model
├── Components/         # Reusable components
│   └── ...
├── Shared/             # Shared layouts and components
│   └── MainLayout.razor         # Main layout
└── wwwroot/           # Static files
    └── ...
```

### How It Works

#### Port Scanning

The application uses platform-specific APIs to scan for listening ports:

**Windows**: Uses `GetExtendedTcpTable` API via P/Invoke
**Linux**: Reads `/proc/net/tcp` and `/proc/net/tcp6` files
**macOS**: Uses `lsof` command or BSD socket APIs

#### Process Information

**Windows**: Uses WMI (Windows Management Instrumentation) to get process details
**Linux**: Reads `/proc/[pid]/cmdline` for command line arguments
**macOS**: Uses `ps` command or BSD APIs

#### Process Termination

Two-stage approach:
1. Try graceful shutdown (SIGTERM on Unix, CloseMainWindow on Windows)
2. Force kill (SIGKILL on Unix, Process.Kill on Windows)

#### Cloudflare Tunnel Integration

- Spawns Cloudflared process with appropriate arguments
- Monitors process output for tunnel URL
- Tracks process status and provides control interface
- Auto-restarts tunnels on application startup

## Development

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022 or VS Code
- Git

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

### Test

```bash
dotnet test
```

### Publish

```bash
dotnet publish -c Release -o ./publish
```

## Configuration

### Settings Location

Settings are stored in:
- **Windows**: `%APPDATA%\PortKiller\settings.json`
- **macOS**: `~/Library/Application Support/PortKiller/settings.json`
- **Linux**: `~/.config/PortKiller/settings.json`

### Available Settings

- Refresh interval for port scanning
- Notification preferences
- Theme selection (dark/light)
- Favorite ports
- Active tunnels

## Troubleshooting

### Common Issues

#### Port not showing up
- Ensure the port is actually listening (use `netstat` or `lsof` to verify)
- Check if the application has sufficient permissions
- Try refreshing the port list manually

#### Cannot terminate process
- Ensure the application is running with administrator/root privileges
- Some system processes may require elevated permissions
- Check if the process is protected by the operating system

#### Tunnel not starting
- Verify Cloudflared is installed correctly
- Check if Cloudflared is accessible in your PATH
- Ensure the port is not already in use
- Check the application logs for error messages

#### Performance issues
- Increase the refresh interval in settings
- Reduce the number of watched ports
- Close unnecessary applications

### Getting Help

If you encounter any issues or have questions:

1. **Check the documentation**: Review this README and inline help
2. **Search existing issues**: Check [GitHub Issues](https://github.com/productdevbook/port-killer/issues) for similar problems
3. **Contact support**: Reach out to 科控物联 via QQ: **2492123056**
4. **Create an issue**: If you found a bug, create a detailed issue on GitHub

## Contributing

We welcome contributions from the community! See [CONTRIBUTING.md](../../CONTRIBUTING.md) for development setup and guidelines.

### Development Setup

1. Fork the repository
2. Clone your fork
3. Create a feature branch
4. Make your changes
5. Test thoroughly
6. Submit a pull request

## Roadmap

- [ ] Advanced filtering and sorting options
- [ ] Port usage statistics and analytics
- [ ] Integration with other tunneling services
- [ ] Multi-language support
- [ ] Advanced notification rules
- [ ] Port usage history and trends
- [ ] Real-time collaboration features
- [ ] API for external integrations

## Support

### 中文支持
- **团队**: 科控物联
- **QQ**: 2492123056
- **反馈**: 欢迎通过QQ反馈问题和建议

### English Support
- **GitHub Issues**: [Report a bug](https://github.com/productdevbook/port-killer/issues)
- **GitHub Discussions**: [Ask a question](https://github.com/productdevbook/port-killer/discussions)

## License

MIT License - see [LICENSE](../../LICENSE).

## Credits

- Original PortKiller project by [productdevbook](https://github.com/productdevbook)
- Cloudflare for the excellent tunneling service
- MASA Blazor team for the amazing UI components
- All contributors and users of PortKiller

---

**Developed with ❤️ by 科控物联**

**中文用户支持：QQ 2492123056**
