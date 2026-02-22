# PortManager v3.0

<p align="center">
  <img src="v3.0/src/PortManager.Web/wwwroot/appicon.svg" alt="PortManager Icon" width="128" height="128">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License: MIT"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%2B-0078D6" alt="Windows"></a>
  <a href="https://github.com/jingshui127/port-killer/releases"><img src="https://img.shields.io/github/v/release/jingshui127/port-killer" alt="GitHub Release"></a>
</p>

<p align="center">
  A powerful cross-platform port management tool for developers.<br>
  Monitor ports, integrate Cloudflare Tunnels, and kill processes with one click.
</p>

## About

PortManager is a powerful cross-platform port management tool developed by **科控物联 (KeKong WuLian)**. Version 3.0 features a complete UI redesign with MASA Blazor, providing a modern, responsive interface for monitoring, managing, and controlling network ports, processes, and Cloudflare Tunnels on Windows.

### Developer
- **Team**: 科控物联 (KeKong WuLian)
- **QQ**: 2492123056
- **Feedback**: Welcome to feedback and suggestions via QQ

## What's New in v3.0

### 🎨 Complete UI Redesign
- **MASA Blazor Integration**: Modern Material Design components
- **Responsive Layout**: Full-width design that adapts to screen size
- **Fixed Column Table**: Better table viewing with fixed left/right columns
- **Improved Navigation**: Streamlined menu and action buttons

### 📊 Enhanced Table View
- **Fixed Columns**: Port and action columns stay visible while scrolling
- **Pagination**: Configurable items per page (default 15)
- **Sorting**: Click column headers to sort data
- **Column Width Optimization**: Better use of screen space

### 🖥️ WinForms Support
- **Native Windows Application**: WinForms version with embedded Blazor WebView
- **Application Icon**: Custom app icon for both Web and Desktop versions
- **Simultaneous Access**: Run WinForms app while accessing via web browser

### 📁 Data Export
- **CSV Export**: Export port data to CSV format
- **JSON Export**: Export port data to JSON format
- **Quick Access**: Export buttons integrated into main toolbar

### 🔄 Improved Layout
- **Fixed Header**: Title, buttons, and stats panel stay fixed at top
- **Scrollable Content**: Table/Card view scrolls independently
- **Better Spacing**: Optimized spacing between elements

## Installation

### Requirements
- **.NET 10 SDK** or later
- **Windows 10** or later
- **Cloudflared** (optional, for tunnel functionality)

### Web Version

**Run locally:**
```bash
cd v3.0/src/PortManager.Web
dotnet run
```

**Access:** Open your browser and navigate to `http://localhost:5000`

### Desktop (WinForms) Version

**Run locally:**
```bash
cd v3.0/src/PortManager.Desktop
dotnet run
```

Or build and run the executable:
```bash
cd v3.0/src/PortManager.Desktop
dotnet build -c Release
# Run the generated .exe in bin/Release/net10.0-windows/
```

### Download Release

Download `.zip` from [GitHub Releases](https://github.com/jingshui127/port-killer/releases) and extract.

## Features

### Port Management
- 🔍 **Auto-discovery**: Automatically discovers all listening TCP/UDP ports
- ⚡ **One-click termination**: Kill processes with a single click
- 🔄 **Auto-refresh**: Automatic refresh with incremental updates, no flickering
- 🔎 **Search & Filter**: Quick search by port number, process name, or address
- ⭐ **Favorites**: Mark important ports as favorites for quick access
- 👁 **Watched Ports**: Monitor specific ports with notifications
- 📊 **Table View**: Professional table view with fixed columns and pagination
- 🗑 **Batch Operations**: Select and manage multiple ports at once
- 📁 **Process Information**: View process path, PID, address, user, and command information
- 🔔 **Notification System**: Real-time notifications for port status changes
- 📜 **Notification History**: View all notification records
- 📤 **Data Export**: Export port data to CSV or JSON format
- 🎨 **Dual Platform**: Both Web and WinForms desktop versions available

### Cloudflare Tunnels
- ☁️ **Tunnel Management**: Create and manage Cloudflare Tunnel connections
- 🌐 **Quick Access**: One-click expose local ports to the internet
- 🚀 **Auto-start**: Automatically restore tunnels on application startup
- 📊 **Tunnel Status**: Real-time tunnel status and URL display
- 🔄 **Restart Support**: Stop and restart tunnels
- 💾 **Persistence**: Tunnel information saved locally, auto-recover after restart

### User Interface
- 🌓 **Theme Support**: Dark and light theme switching
- 📱 **Responsive Design**: Works on desktop and mobile devices
- 🎨 **Modern UI**: Material Design components based on MASA Blazor
- 🔔 **Notification System**: Port status changes and tunnel event notifications
- 📜 **Notification History**: View all notification records

## Usage Guide

### Port Management

#### Viewing Ports
1. Open the application
2. Navigate to the "Ports" page
3. View all active ports with their associated processes
4. Switch between card view and table view using the toggle button

#### Terminating a Process
1. Find the port you want to terminate
2. Click the "Kill" button on the port card
3. The process will be terminated immediately

#### Adding to Favorites
1. Hover over a port card
2. Click the star icon to add/remove from favorites
3. Favorite ports can be filtered using the "Favorites" filter button

#### Monitoring Ports
1. Click the "Watch" button on a port card
2. When monitored ports start or stop, you will receive notifications
3. View all status changes in the "Notification History"

#### Using Table View
1. Click the "表格" (Table) button to switch to table view
2. **Fixed Columns**: Port number (left) and actions (right) stay visible while scrolling
3. **Pagination**: Use the dropdown at the bottom to change items per page (default: 15)
4. **Sorting**: Click column headers to sort by that column
5. **Horizontal Scroll**: Scroll right to see all columns (Process Name, Command, Address, etc.)

#### Exporting Data
1. Click the "CSV" or "JSON" button in the toolbar
2. Data will be exported and saved to your Downloads folder
3. Open the file with your preferred application

#### Batch Operations
1. Click on port cards to select multiple ports
2. Use the batch action buttons to:
   - Kill all selected processes
   - Add all to favorites

### Cloudflare Tunnels

#### Creating a Tunnel
1. Navigate to the "Tunnels" page, or click the "Tunnel Management" card on the home page
2. Click the "Create Tunnel" button
3. Enter the port number and tunnel name (optional)
4. Click "Create" to start the tunnel
5. Wait for the tunnel URL to be generated, then click the copy button to copy the URL

#### Managing Tunnels
- **Stop Tunnel**: Click the stop button to terminate a tunnel
- **Restart Tunnel**: Click the restart button to recreate a tunnel
- **Copy URL**: Click the copy button to copy the tunnel URL to clipboard
- **View Status**: Real-time view of tunnel running status and uptime

#### Prerequisites
1. Download and install Cloudflared from [Cloudflare's website](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/)
2. Ensure `cloudflared` is accessible in your system PATH
3. The application will automatically detect Cloudflared installation

### Notifications

The application provides notifications for:
- Monitored ports starting or stopping
- Tunnel created successfully
- Tunnel stopped
- Tunnel restarted successfully
- Process terminated

Click the "Notification History" button on the Ports page to view all notification records.

## Technical Stack

### Core Technologies
- **.NET 10**: Latest .NET framework for cross-platform development
- **Blazor Server**: Web framework for building interactive web UIs
- **Blazor WebView**: Embedded web view for WinForms desktop application
- **MASA Blazor**: Material Design component library for Blazor
- **Cloudflare Tunnel**: Secure tunneling service for exposing local services

### Project Structure
```
v3.0/
├── src/
│   ├── PortManager.Web/          # Blazor Server Web Application
│   ├── PortManager.Desktop/      # WinForms Desktop Application
│   ├── PortManager.Shared/       # Shared Components and Pages
│   └── PortManager.Core/         # Core Services and Models
└── PortManager.sln
```

### Platform Support
- **Windows**: Full support with both Web and Desktop (WinForms) versions
- **Web**: Cross-platform support via Blazor Server
- **Simultaneous Access**: Run desktop app while accessing via web browser

## Configuration

### Settings Location
- **Windows**: `%LocalAppData%\PortKiller.Blazor\settings.json`

### Saved Data
- Favorite ports list
- Watched ports list
- Active tunnel information
- Theme settings

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

1. **Check the documentation**: Review this README and the inline help
2. **Search existing issues**: Check [GitHub Issues](https://github.com/jingshui127/port-killer/issues) for similar problems
3. **Create an issue**: If you found a bug, create a detailed issue on GitHub

## Contributing

We welcome contributions from the community!

### Development Setup

1. Fork the repository
2. Clone your fork
3. Create a feature branch
4. Make your changes
5. Test thoroughly
6. Submit a pull request

## License

MIT License - see [LICENSE](LICENSE).

## Acknowledgments

- Original PortKiller project by [productdevbook](https://github.com/productdevbook)
- Cloudflare for the excellent tunneling service
- MASA Blazor team for the amazing UI components
- All contributors and users of PortKiller

---

**Developed with ❤️ by 科控物联**

**中文用户支持：QQ 2492123056**
