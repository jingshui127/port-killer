# PortKiller (端口管理器)

<p align="center">
  <img src="https://raw.githubusercontent.com/productdevbook/port-killer/refs/heads/main/platforms/macos/Resources/AppIcon.svg" alt="PortKiller 图标" width="128" height="128">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/许可证-MIT-blue.svg" alt="License: MIT"></a>
  <a href="https://www.apple.com/macos/"><img src="https://img.shields.io/badge/macOS-15.0%2B-brightgreen" alt="macOS"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%2B-0078D6" alt="Windows"></a>
  <a href="https://www.linux.org/"><img src="https://img.shields.io/badge/Linux-所有发行版-FFA500" alt="Linux"></a>
  <a href="https://github.com/productdevbook/port-killer/releases"><img src="https://img.shields.io/github/v/release/productdevbook/port-killer" alt="GitHub Release"></a>
</p>

<p align="center">
  一款强大的跨平台端口管理工具，专为开发者设计。<br>
  监控端口、管理Kubernetes端口转发、集成Cloudflare隧道、一键终止进程。
</p>

<p align="center">
  <strong>技术支持：科控物联 | QQ: 2492123056</strong>
</p>

## 关于

PortKiller (端口管理器) 是由科控物联开发的强大跨平台端口管理工具。它为开发者提供了直观的界面，用于监控、管理和控制Windows、macOS和Linux操作系统上的网络端口、进程和Cloudflare隧道。

### macOS 版本

<p align="center">
  <img src=".github/assets/macos.png" alt="PortKiller macOS" width="800">
</p>

### Windows 版本

<p align="center">
  <img src=".github/assets/windows.jpeg" alt="PortKiller Windows" width="800">
</p>

### Blazor Web 版本

<p align="center">
  <img src=".github/assets/blazor.png" alt="PortKiller Blazor" width="800">
</p>

## 安装

### macOS

**使用 Homebrew:**
```bash
brew install --cask productdevbook/tap/portkiller
```

**手动安装:** 从 [GitHub Releases](https://github.com/productdevbook/port-killer/releases) 下载 `.dmg` 文件。

### Windows

从 [GitHub Releases](https://github.com/productdevbook/port-killer/releases) 下载 `.zip` 文件并解压。

### Linux

**下载安装:** 从 [GitHub Releases](https://github.com/productdevbook/port-killer/releases) 下载适合您发行版的安装包。

**从源码构建:**
```bash
git clone https://github.com/productdevbook/port-killer.git
cd port-killer
dotnet build
```

### Blazor Web 版本

**本地运行:**
```bash
cd platforms/blazor/PortKiller.Blazor
dotnet run
```

**访问:** 在浏览器中打开 `http://localhost:5000`

## 功能特性

### 端口管理
- 🔍 **自动发现**: 自动发现所有监听的TCP端口
- ⚡ **一键终止**: 单击终止进程（优雅终止 + 强制终止）
- 🔄 **自动刷新**: 可配置的刷新间隔，实时监控
- 🔎 **搜索与筛选**: 按端口号或进程名快速搜索
- ⭐ **收藏功能**: 将重要端口标记为收藏，快速访问
- 👁 **端口监控**: 监控特定端口并接收通知
- 📂 **智能分类**: 自动分类（Web服务器、数据库、开发工具、系统）
- 📊 **表格视图**: 在卡片视图和表格视图之间切换，更好的数据可视化
- 🗑 **批量操作**: 选择并一次管理多个端口
- 📁 **进程信息**: 查看进程路径和目录信息

### Kubernetes 端口转发
- 🔗 **端口转发管理**: 创建和管理kubectl端口转发会话
- 🔌 **自动重连**: 连接丢失时自动重连
- 📝 **连接日志**: 详细的日志和状态监控
- 🔔 **通知**: 连接/断开时接收通知

### Cloudflare 隧道
- ☁️ **隧道管理**: 查看和管理活动的Cloudflare隧道连接
- 🌐 **快速状态**: 实时隧道状态监控
- 🚀 **自动启动**: 应用程序启动时自动重启隧道
- 📊 **隧道统计**: 查看详细的隧道信息和统计数据
- 🔄 **自动刷新**: 自动隧道状态更新

### 跨平台
- 📍 **菜单栏集成** (macOS)
- 🖥️ **系统托盘应用** (Windows)
- 🌐 **基于Web的UI** (Blazor Server)
- 🎨 **原生UI**: 每个平台的优化界面
- 🌓 **主题支持**: 深色和浅色主题选项
- 📱 **响应式设计**: 支持桌面和移动设备

## 使用指南

### 端口管理

#### 查看端口
1. 打开应用程序
2. 导航到"端口"页面
3. 查看所有活动端口及其关联的进程
4. 使用切换按钮在卡片视图和表格视图之间切换

#### 终止进程
1. 找到要终止的端口
2. 点击端口旁边的"终止"按钮
3. 在对话框中确认操作
4. 进程将被优雅地终止

#### 添加到收藏
1. 将鼠标悬停在端口卡片或表格行上
2. 点击星形图标添加/移除收藏
3. 收藏的端口显示在列表顶部

#### 批量操作
1. 使用复选框选择多个端口
2. 使用批量操作按钮：
   - 终止所有选中的进程
   - 将所有添加到收藏
   - 从收藏中移除所有

### Cloudflare 隧道

#### 创建隧道
1. 导航到"隧道"页面
2. 点击"创建隧道"按钮
3. 输入端口号和隧道名称
4. 点击"创建"启动隧道
5. 连接后将显示隧道URL

#### 管理隧道
- **停止隧道**: 点击停止按钮终止隧道
- **重启隧道**: 点击重启按钮重启已停止的隧道
- **查看详情**: 点击隧道查看详细信息
- **自动刷新**: 隧道每5秒自动刷新

#### Cloudflared 设置
1. 从 [Cloudflare官网](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/) 下载并安装Cloudflared
2. 应用程序将自动检测Cloudflared安装
3. Cloudflared版本每5分钟检查一次，以避免性能影响

### 通知

应用程序为以下情况提供通知：
- 端口状态变化
- 隧道连接/断开
- Cloudflared版本更新
- 错误消息和警告

在通知面板中查看通知历史。

## 技术栈

### 核心技术
- **.NET 10**: 用于跨平台开发的最新.NET框架
- **Blazor Server**: 用于构建交互式Web UI的Web框架
- **MASA Blazor**: Blazor的Material Design组件库
- **Cloudflare Tunnel**: 用于暴露本地服务的安全隧道服务

### 平台支持
- **Windows**: 完全支持，原生UI
- **macOS**: 完全支持，菜单栏集成
- **Linux**: 完全支持，基于Web的UI
- **Web**: 通过Blazor Server的跨平台支持

## 配置

### 设置位置
- **Windows**: `%APPDATA%\PortKiller\settings.json`
- **macOS**: `~/Library/Application Support/PortKiller/settings.json`
- **Linux**: `~/.config/PortKiller/settings.json`

### 可用设置
- 端口扫描的刷新间隔
- 通知首选项
- 主题选择（深色/浅色）
- 收藏的端口
- 活动的隧道

## 故障排除

### 常见问题

#### 端口未显示
- 确保端口确实在监听（使用 `netstat` 或 `lsof` 验证）
- 检查应用程序是否有足够的权限
- 尝试手动刷新端口列表

#### 无法终止进程
- 确保应用程序以管理员/root权限运行
- 某些系统进程可能需要提升的权限
- 检查进程是否受操作系统保护

#### 隧道无法启动
- 验证Cloudflared是否正确安装
- 检查Cloudflared是否在您的PATH中可访问
- 确保端口未被占用
- 检查应用程序日志中的错误消息

#### 性能问题
- 在设置中增加刷新间隔
- 减少监控的端口数量
- 关闭不必要的应用程序

### 获取帮助

如果您遇到任何问题或有疑问：

1. **查看文档**: 阅读此README和内联帮助
2. **搜索现有问题**: 在 [GitHub Issues](https://github.com/productdevbook/port-killer/issues) 中查找类似问题
3. **联系支持**: 通过QQ联系科控物联: **2492123056**
4. **创建问题**: 如果您发现了bug，请在GitHub上创建详细的问题

## 贡献

我们欢迎社区贡献！请参阅 [CONTRIBUTING.md](CONTRIBUTING.md) 了解开发设置和指南。

### 开发设置

1. Fork 仓库
2. 克隆您的fork
3. 创建功能分支
4. 进行更改
5. 彻底测试
6. 提交拉取请求

## 路线图

- [ ] macOS和Linux原生应用程序
- [ ] 高级筛选和排序选项
- [ ] 端口使用统计和分析
- [ ] 集成其他隧道服务
- [ ] 插件系统以实现可扩展性
- [ ] 多语言支持
- [ ] 高级通知规则
- [ ] 端口使用历史和趋势

## 支持

### 中文支持
- **团队**: 科控物联
- **QQ**: 2492123056
- **反馈**: 欢迎通过QQ反馈问题和建议

### 英文支持
- **GitHub Issues**: [报告错误](https://github.com/productdevbook/port-killer/issues)
- **GitHub Discussions**: [提问](https://github.com/productdevbook/port-killer/discussions)

## 赞助者

<p align="center">
  <a href="https://cdn.jsdelivr.net/gh/productdevbook/static/sponsors.svg">
    <img src='https://cdn.jsdelivr.net/gh/productdevbook/static/sponsors.svg'/>
  </a>
</p>

## 许可证

MIT 许可证 - 请参阅 [LICENSE](LICENSE)。

## 致谢

- [productdevbook](https://github.com/productdevbook) 的原始PortKiller项目
- Cloudflare提供的优秀隧道服务
- MASA Blazor团队的优秀UI组件
- PortKiller的所有贡献者和用户

---

**由科控物联用 ❤️ 开发**

**中文用户支持：QQ 2492123056**
