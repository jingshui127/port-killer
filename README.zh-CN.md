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

## 预览

### macOS

<p align="center">
  <img src=".github/assets/macos.png" alt="PortKiller macOS" width="800">
</p>

### Windows

<p align="center">
  <img src=".github/assets/windows.jpeg" alt="PortKiller Windows" width="800">
</p>

## 系统要求

- **macOS**: 15.0 及以上版本
- **Windows**: 10 及以上版本
- **.NET**: 10.0 运行时（Windows 版本内置）

## 安装

### macOS

**Homebrew（推荐）:**
```bash
brew install --cask productdevbook/tap/portkiller
```

**手动安装:** 从 [GitHub Releases](https://github.com/productdevbook/port-killer/releases) 下载 `.dmg` 文件，双击打开并拖拽到 Applications 文件夹。

### Windows

从 [GitHub Releases](https://github.com/productdevbook/port-killer/releases) 下载 `.zip` 文件，解压到任意位置，然后运行 `PortKiller.exe`。

## 功能特性

### 端口管理
- 🔍 **自动发现**: 自动扫描并显示所有监听的 TCP 端口
- ⚡ **一键终止**: 支持优雅终止和强制终止进程
- 🔄 **自动刷新**: 可配置刷新间隔，实时监控端口状态
- 🔎 **智能搜索**: 按端口号、进程名称、地址等多维度搜索
- ⭐ **收藏功能**: 收藏重要端口，方便快速访问
- 👁️ **端口监控**: 监控端口状态变化，接收通知提醒
- 📂 **智能分类**: 自动分类端口类型（Web 服务器、数据库、开发工具、系统）
- 📊 **端口统计**: 显示总端口数，直观了解系统端口使用情况

### Kubernetes 端口转发
- 🔗 **端口转发管理**: 创建和管理多个 kubectl port-forward 会话
- 🔌 **自动重连**: 连接丢失时自动尝试重连
- 📝 **详细日志**: 记录连接日志和状态变化
- 🔔 **状态通知**: 连接建立/断开时发送通知

### Cloudflare 隧道
- 🌐 **内网站点发布**: 一键将内网站点发布至公网，方便演示和远程访问
- ☁️ **隧道管理**: 查看和管理所有活动的 Cloudflare 隧道
- 🔗 **隧道 URL**: 自动生成并显示隧道 URL，支持复制到剪贴板
- 📊 **隧道统计**: 显示总隧道数，实时了解隧道使用情况
- 🔄 **隧道刷新**: 支持手动刷新隧道状态

### 跨平台特性
- 📍 **菜单栏集成**: macOS 上集成到菜单栏，方便快速访问
- 🖥️ **系统托盘应用**: Windows 上集成到系统托盘，随系统启动
- 🎨 **原生 UI**: 各平台采用原生 UI 设计，提供一致的用户体验
- 🌙 **深色模式**: 支持深色模式，保护视力

## 使用指南

### 基本操作
1. **启动应用**: 安装完成后，从 Applications 文件夹（macOS）或解压目录（Windows）启动应用
2. **查看端口**: 应用会自动扫描并显示所有监听的端口
3. **终止进程**: 点击端口卡片上的 "终止" 按钮，选择终止方式
4. **搜索端口**: 在搜索框中输入端口号或进程名称进行搜索
5. **收藏端口**: 点击端口卡片上的 "收藏" 按钮，将端口添加到收藏列表

### Cloudflare 隧道使用
1. **安装 cloudflared**: 首次使用隧道功能时，应用会自动检测并提示安装 cloudflared
2. **创建隧道**: 在隧道页面，输入要暴露的本地端口，点击 "创建隧道" 按钮
3. **访问公网**: 隧道创建成功后，会生成一个公网 URL，通过该 URL 可以访问本地服务
4. **管理隧道**: 在隧道页面可以查看所有活动的隧道，支持停止或重新创建隧道

### Kubernetes 端口转发使用
1. **配置 kubectl**: 确保本地已安装并配置好 kubectl
2. **创建转发**: 在 Kubernetes 页面，输入集群信息和端口转发配置
3. **监控状态**: 应用会显示转发状态，支持自动重连

## 常见问题

### 1. 为什么某些端口无法终止？
- **原因**: 某些系统端口或受保护的进程可能需要管理员/root 权限才能终止
- **解决方案**: 以管理员/root 权限运行应用

### 2. Cloudflare 隧道创建失败怎么办？
- **检查 cloudflared**: 确保 cloudflared 已正确安装
- **检查网络**: 确保网络连接正常，cloudflared 需要访问 Cloudflare API
- **检查端口**: 确保要转发的本地端口正在运行服务

### 3. 应用启动缓慢怎么办？
- **调整刷新间隔**: 在设置中增加刷新间隔，减少扫描频率
- **过滤端口**: 使用搜索功能过滤不需要的端口

### 4. 如何随系统启动？
- **macOS**: 在系统设置 → 用户与群组 → 登录项中添加 PortKiller
- **Windows**: 在应用设置中启用 "随系统启动"

## 技术架构

PortKiller 采用现代化的技术栈构建：

- **前端**: Blazor WebAssembly（跨平台 UI）
- **后端**: .NET 10.0
- **系统集成**: 原生 API 调用
- **外部集成**: Cloudflare Tunnel API、Kubernetes API

## 更新日志

### v1.0.0（最新版本）
- ✨ 全新发布，支持 macOS 和 Windows 平台
- 🚀 实现核心端口管理功能
- 🌐 集成 Cloudflare 隧道
- 🔗 支持 Kubernetes 端口转发
- 🎨 原生 UI 设计

## 贡献

我们欢迎社区贡献！请参阅 [CONTRIBUTING.md](CONTRIBUTING.md) 了解开发环境设置和贡献流程。

### 开发环境要求
- .NET 10.0 SDK
- Visual Studio 2022 或 Rider
- macOS 或 Windows 开发机器

## 赞助

如果您喜欢 PortKiller 并希望支持其发展，请考虑赞助我们：

<p align="center">
  <a href="https://cdn.jsdelivr.net/gh/productdevbook/static/sponsors.svg">
    <img src='https://cdn.jsdelivr.net/gh/productdevbook/static/sponsors.svg'/>
  </a>
</p>

## 许可证

PortKiller 使用 MIT 许可证 - 请参阅 [LICENSE](LICENSE) 文件了解详情。

## 联系我们

- **QQ**: 2492123056
- **GitHub Issues**: [https://github.com/productdevbook/port-killer/issues](https://github.com/productdevbook/port-killer/issues)

感谢您使用 PortKiller！如果您有任何建议或问题，欢迎随时联系我们。
