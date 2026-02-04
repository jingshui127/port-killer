@echo off
chcp 65001 >nul
echo ========================================
echo PortKiller .NET 10.0 运行时安装程序
echo ========================================
echo.

echo 正在检查 .NET 10.0 运行时...
dotnet --list-runtimes | findstr "Microsoft.NETCore.App 10.0" >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] .NET 10.0 运行时已安装
    goto :end
)

echo [信息] .NET 10.0 运行时未安装，正在下载...
echo.

set DOTNET_URL=https://dotnetcli.blob.core.windows.net/dotnet/WindowsDesktop/10.0.0/windowsdesktop-runtime-10.0.0-win-x64.exe
set DOTNET_FILE=dotnet-runtime-10.0.0-win-x64.exe

echo 下载地址: %DOTNET_URL%
echo.

if exist "%DOTNET_FILE%" (
    echo [信息] 安装文件已存在，跳过下载
) else (
    echo [下载] 正在下载 .NET 10.0 运行时...
    echo 这可能需要几分钟时间，请耐心等待...
    echo.
    
    powershell -Command "& { Invoke-WebRequest -Uri '%DOTNET_URL%' -OutFile '%DOTNET_FILE%' }"
    
    if %errorlevel% neq 0 (
        echo [错误] 下载失败！
        echo 请手动下载: %DOTNET_URL%
        pause
        exit /b 1
    )
    
    echo [OK] 下载完成
)

echo.
echo [安装] 正在安装 .NET 10.0 运行时...
echo 请以管理员身份运行此脚本
echo.

"%DOTNET_FILE%" /install /quiet /norestart

if %errorlevel% neq 0 (
    echo [错误] 安装失败！
    echo 请以管理员身份运行此脚本
    pause
    exit /b 1
)

echo.
echo [OK] .NET 10.0 运行时安装成功！
echo.
echo 您现在可以运行 PortKiller 了

:end
echo.
echo ========================================
echo 按任意键退出...
pause >nul
