# PortManager v3.0 (端口管理器)

<p align="center">
  <img src="v3.0/src/PortManager.Web/wwwroot/appicon.svg" alt="PortManager 图标" width="128" height="128">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/许可证-MIT-blue.svg" alt="License: MIT"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%2B-0078D6" alt="Windows"></a>
  <a href="https://github.com/jingshui127/port-killer/releases"><img src="https://img.shields.io/github/v/release/jingshui127/port-killer" alt="GitHub Release"></a>
</p>

<p align="center">
  一款强大的跨平台端口管理工具，专为开发者设计。<br>
  监控端口、集成Cloudflare隧道、一键终止进程。
</p>

## 关于

PortManager (端口管理器) 是一款由 **科控物联** 开发的强大跨平台端口管理工具。v3.0 版本采用 MASA Blazor 进行了完整的 UI 重设计，为 Windows 系统上的网络端口、进程和 Cloudflare 隧道的监控、管理和控制提供了现代化的响应式界面。

### 开发者信息
- **团队**: 科控物联
- **QQ**: 2492123056
- **反馈**: 欢迎通过QQ反馈问题和建议

## v3.0 新特性

### 🎨 完整的 UI 重设计
- **MASA Blazor 集成**: 现代化的 Material Design 组件
- **响应式布局**: 全宽设计，自适应屏幕尺寸
- **固定列表格**: 更好的表格查看体验，左右列固定
- **改进的导航**: 简化的菜单和操作按钮

### 📊 增强的表格视图
- **固定列**: 端口号（左侧）和操作（右侧）在滚动时保持可见
- **分页支持**: 可配置每页显示数量（默认15条）
- **排序功能**: 点击列标题可按该列排序
- **列宽优化**: 更好地利用屏幕空间

### 🖥️ WinForms 桌面版支持
- **原生 Windows 应用程序**: 使用 Blazor WebView 嵌入的 WinForms 版本
- **应用程序图标**: Web 和桌面版本都有自定义应用图标
- **同时访问**: 运行桌面应用的同时可通过浏览器访问

### 📁 数据导出
- **CSV 导出**: 将端口数据导出为 CSV 格式
- **JSON 导出**: 将端口数据导出为 JSON 格式
- **快速访问**: 导出按钮集成到主工具栏

### 🔄 改进的布局
- **固定头部**: 标题、按钮和统计面板固定在顶部
- **可滚动内容**: 表格/卡片视图独立滚动
- **更好的间距**: 优化元素之间的间距

## 安装

### 系统要求
- **.NET 10 SDK** 或更高版本
- **Windows 10** 或更高版本
- **Cloudflared**（可选，用于隧道功能）

### Web 版本

**本地运行:**
```bash
cd v3.0/src/PortManager.Web
dotnet run
```

**访问:** 在浏览器中打开 `http://localhost:5000`

### 桌面版 (WinForms)

**本地运行:**
```bash
cd v3.0/src/PortManager.Desktop
dotnet run
```

或构建并运行可执行文件:
```bash
cd v3.0/src/PortManager.Desktop
dotnet build -c Release
# 运行生成的 .exe 文件在 bin/Release/net10.0-windows/ 目录中
```

### 下载发布版

从 [GitHub Releases](https://github.com/jingshui127/port-killer/releases) 下载 `.zip` 文件并解压。

## 功能特性

### 端口管理
- 🔍 **自动发现**: 自动发现所有监听的 TCP/UDP 端口
- ⚡ **一键终止**: 单击终止占用端口的进程
- 🔄 **自动刷新**: 自动刷新，增量更新不闪烁，带刷新计数器显示
- 🔎 **搜索与筛选**: 按端口号、进程名或地址快速搜索
- ⭐ **收藏功能**: 将重要端口标记为收藏，快速访问
- 👁 **端口监控**: 监控特定端口并接收状态变化通知
- 📊 **表格视图**: 专业的表格视图，支持固定列和分页
- 🗑 **批量操作**: 选择并一次管理多个端口
- 📁 **进程信息**: 查看进程路径、PID、地址、用户和命令信息
- 🔔 **通知系统**: 端口状态变化实时通知
- 📜 **通知历史**: 查看所有通知记录
- 📤 **数据导出**: 将端口数据导出为 CSV 或 JSON 格式
- 🎨 **双平台**: 同时提供 Web 和 WinForms 桌面版本

### Cloudflare 隧道
- ☁️ **隧道管理**: 创建和管理Cloudflare隧道连接
- 🌐 **快速访问**: 一键将本地端口暴露到公网
- 🚀 **自动启动**: 应用程序启动时自动恢复隧道
- 📊 **隧道状态**: 实时查看隧道运行状态和URL
- 🔄 **重启支持**: 支持停止和重启隧道
- 💾 **持久化**: 隧道信息保存到本地，重启后自动恢复

### 用户界面
- 🌓 **主题支持**: 深色和浅色主题切换
- 📱 **响应式设计**: 适配桌面和移动设备
- 🎨 **现代化UI**: 基于 MASA Blazor 的 Material Design 组件
- 🔔 **通知系统**: 端口状态变化和隧道事件通知
- 📜 **通知历史**: 查看所有通知记录

## 使用指南

### 端口管理

#### 查看端口
1. 打开应用程序，进入主页
2. 点击"端口管理"卡片或导航到"端口"页面
3. 查看所有活动端口及其关联的进程信息
4. 使用切换按钮在卡片视图和表格视图之间切换

#### 终止进程
1. 找到要终止的端口
2. 点击端口卡片上的"✕"终止按钮
3. 进程将被立即终止

#### 收藏端口
1. 将鼠标悬停在端口卡片上
2. 点击星形图标添加/移除收藏
3. 收藏的端口显示在列表顶部

#### 监控端口
1. 点击端口卡片上的"👁"监控按钮
2. 当监控的端口启动或停止时，会收到通知
3. 在"通知历史"中查看所有状态变化

#### 使用表格视图
1. 点击"表格"按钮切换到表格视图
2. **固定列**: 端口号（左侧）和操作（右侧）在滚动时保持可见
3. **分页**: 使用底部的下拉框更改每页显示数量（默认：15条）
4. **排序**: 点击列标题可按该列排序
5. **水平滚动**: 向右滚动查看所有列（进程名、命令、地址等）

#### 导出数据
1. 点击工具栏中的"CSV"或"JSON"按钮
2. 数据将导出并保存到您的下载文件夹
3. 使用您喜欢的应用程序打开文件

#### 批量操作
1. 点击端口卡片选择多个端口
2. 使用顶部批量操作按钮：
   - 终止所有选中的进程
   - 将所有添加到收藏

### Cloudflare 隧道

#### 创建隧道
1. 导航到"隧道"页面，或点击主页"隧道管理"卡片
2. 点击"创建隧道"按钮
3. 输入端口号和隧道名称（可选）
4. 点击"创建"启动隧道
5. 等待隧道URL生成，点击复制按钮复制URL

#### 管理隧道
- **停止隧道**: 点击停止按钮终止隧道
- **重启隧道**: 点击重启按钮重新创建隧道
- **复制URL**: 点击复制按钮复制隧道URL到剪贴板
- **查看状态**: 实时查看隧道的运行状态和运行时间

#### 前提条件
1. 从 [Cloudflare官网](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/) 下载并安装 Cloudflared
2. 确保 `cloudflared.exe` 在系统 PATH 中可访问
3. 应用程序将自动检测 Cloudflared 安装

### 通知

应用程序为以下情况提供通知：
- 监控的端口启动或停止
- 隧道创建成功
- 隧道停止
- 隧道重启成功
- 进程被终止

点击端口页面的"通知历史"按钮查看所有通知记录。

## 技术栈

### 核心技术
- **.NET 10**: 用于跨平台开发的最新.NET框架
- **Blazor Server**: 用于构建交互式Web UI的Web框架
- **Blazor WebView**: 用于 WinForms 桌面应用程序的嵌入式 Web 视图
- **MASA Blazor**: Blazor的Material Design组件库
- **Cloudflare Tunnel**: 用于暴露本地服务的安全隧道服务

### 项目结构
```
v3.0/
├── src/
│   ├── PortManager.Web/          # Blazor Server Web 应用程序
│   ├── PortManager.Desktop/      # WinForms 桌面应用程序
│   ├── PortManager.Shared/       # 共享组件和页面
│   └── PortManager.Core/         # 核心服务和模型
└── PortManager.sln
```

### 平台支持
- **Windows**: 完全支持，同时提供 Web 和桌面（WinForms）版本
- **Web**: 通过 Blazor Server 的跨平台 Web 支持
- **同时访问**: 运行桌面应用的同时可通过浏览器访问

## 配置

### 设置位置
- **Windows**: `%LocalAppData%\PortKiller.Blazor\settings.json`

### 保存的数据
- 收藏的端口列表
- 监控的端口列表
- 活动的隧道信息
- 主题设置

## 故障排除

### 常见问题

#### 端口未显示
- 确保端口确实在监听（使用 `netstat -ano` 验证）
- 检查应用程序是否有足够的权限
- 尝试手动刷新端口列表

#### 无法终止进程
- 确保应用程序以管理员权限运行
- 某些系统进程可能需要提升的权限
- 检查进程是否受操作系统保护

#### 隧道无法启动
- 验证 Cloudflared 是否正确安装
- 检查 Cloudflared 是否在您的 PATH 中可访问
- 确保端口未被占用
- 检查应用程序日志中的错误消息

### 获取帮助

如果您遇到任何问题或有疑问：

1. **查看文档**: 阅读此README和内联帮助
2. **搜索现有问题**: 在 [GitHub Issues](https://github.com/jingshui127/port-killer/issues) 中查找类似问题
3. **创建问题**: 如果您发现了bug，请在GitHub上创建详细的问题

## 贡献

我们欢迎社区贡献！

### 开发设置

1. Fork 仓库
2. 克隆您的fork
3. 创建功能分支
4. 进行更改
5. 彻底测试
6. 提交拉取请求

## 许可证

MIT 许可证 - 请参阅 [LICENSE](LICENSE)。

---

**用 ❤️ 开发**
