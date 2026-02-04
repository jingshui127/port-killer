# PortKiller

<p align="center">
  <img src="https://raw.githubusercontent.com/productdevbook/port-killer/refs/heads/main/platforms/macos/Resources/AppIcon.svg" alt="PortKiller 图标" width="128" height="128">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="许可证: MIT"></a>
  <a href="https://www.apple.com/macos/"><img src="https://img.shields.io/badge/macOS-15.0%2B-brightgreen" alt="macOS"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%2B-0078D6" alt="Windows"></a>
  <a href="https://github.com/productdevbook/port-killer/releases"><img src="https://img.shields.io/github/v/release/productdevbook/port-killer" alt="GitHub 发布"></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10.0"></a>
</p>

<p align="center">
一款强大的跨平台端口管理工具，专为开发者设计。<br>
监控端口、管理 Kubernetes 端口转发、集成 Cloudflare 隧道，一键终止进程。
</p>

## 作者

**科控物联**  
QQ: 2492123056

### macOS

<p align="center">
  <img src=".github/assets/macos.png" alt="PortKiller macOS" width="800">
</p>

### Windows

<p align="center">
  <img src=".github/assets/windows.jpeg" alt="PortKiller Windows" width="800">
</p>

## 安装

### macOS

**Homebrew:**
```bash
brew install --cask productdevbook/tap/portkiller
```

**手动安装:** 从 [GitHub Releases](https://github.com/productdevbook/port-killer/releases) 下载 `.dmg` 文件。

### Windows

从 [GitHub Releases](https://github.com/productdevbook/port-killer/releases) 下载 `.zip` 文件并解压。

## 功能特性

### 端口管理
- 🔍 自动发现所有监听的 TCP 端口
- ⚡ 一键终止进程（优雅终止 + 强制终止）
- 🔄 可配置间隔的自动刷新
- 🔎 按端口号或进程名称搜索和过滤
- ⭐ 收藏重要端口以便快速访问
- 👁️ 监控端口并接收通知
- 📂 智能分类（Web 服务器、数据库、开发工具、系统）

### Kubernetes 端口转发
- 🔗 创建和管理 kubectl port-forward 会话
- 🔌 连接丢失时自动重连
- 📝 连接日志和状态监控
- 🔔 连接/断开通知

### Cloudflare 隧道
- 🌐 内网站点发布至公网，方便给客户演示
- ☁️ 查看和管理活动的 Cloudflare 隧道连接
- 🌐 快速访问隧道状态

### 跨平台
- 📍 菜单栏集成（macOS）
- 🖥️ 系统托盘应用（Windows）
- 🎨 各平台原生 UI

## 贡献

请参阅 [CONTRIBUTING.md](CONTRIBUTING.md) 了解开发环境设置。

## 赞助者

<p align="center">
  <a href="https://cdn.jsdelivr.net/gh/productdevbook/static/sponsors.svg">
    <img src='https://cdn.jsdelivr.net/gh/productdevbook/static/sponsors.svg'/>
  </a>
</p>

## 许可证

MIT 许可证 - 请参阅 [LICENSE](LICENSE)。
