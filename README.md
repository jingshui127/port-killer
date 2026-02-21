# PortManager

<p align="center">
  <img src="platforms/blazor/PortKiller.Blazor/wwwroot/appicon.svg" alt="PortManager Icon" width="128" height="128">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License: MIT"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%2B-0078D6" alt="Windows"></a>
  <a href="https://www.apple.com/macos/"><img src="https://img.shields.io/badge/macOS-15.0%2B-brightgreen" alt="macOS"></a>
  <a href="https://github.com/jingshui127/port-killer/releases"><img src="https://img.shields.io/github/v/release/jingshui127/port-killer" alt="GitHub Release"></a>
</p>

<p align="center">
  A powerful cross-platform port management tool for developers.<br>
  Monitor ports, integrate Cloudflare Tunnels, and kill processes with one click.
</p>

## About

PortManager is a powerful cross-platform port management tool developed by **科控物联 (KeKong WuLian)**. It provides developers with an intuitive interface to monitor, manage, and control network ports, processes, and Cloudflare Tunnels on Windows operating system.

### Developer
- **Team**: 科控物联 (KeKong WuLian)
- **QQ**: 2492123056
- **Feedback**: Welcome to feedback and suggestions via QQ

### Blazor Web Version

<p align="center">
  <img src=".github/assets/blazor.png" alt="PortManager Blazor" width="800">
</p>

## Installation

### Blazor Web Version

**Run locally:**
```bash
cd platforms/blazor/PortKiller.Blazor
dotnet run
```

**Access:** Open your browser and navigate to `http://localhost:5000`

### Windows

Download `.zip` from [GitHub Releases](https://github.com/jingshui127/port-killer/releases) and extract.

**Access:** Open your browser and navigate to `http://localhost:5000`

## Features

### Port Management
- 🔍 **Auto-discovery**: Automatically discovers all listening TCP ports
- ⚡ **One-click termination**: Kill processes with a single click
- 🔄 **Auto-refresh**: Automatic refresh with incremental updates, no flickering
- 🔎 **Search & Filter**: Quick search by port number or process name
- ⭐ **Favorites**: Mark important ports as favorites for quick access
- 👁 **Watched Ports**: Monitor specific ports with notifications
- 📊 **Table View**: Switch between card and table views
- 🗑 **Batch Operations**: Select and manage multiple ports at once
- 📁 **Process Information**: View process path, PID, address and user information
- 🔔 **Notification System**: Real-time notifications for port status changes
- 📜 **Notification History**: View all notification records

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
- **MASA Blazor**: Material Design component library for Blazor
- **Cloudflare Tunnel**: Secure tunneling service for exposing local services

### Platform Support
- **Windows**: Full support with native application and Web UI
- **Web**: Cross-platform support via Blazor Server

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
