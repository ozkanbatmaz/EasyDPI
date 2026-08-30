@echo off
REM ---------------------------------------------------------------
REM  EasyDPI build script
REM
REM  No Visual Studio required. This uses the C# compiler that ships with
REM  Windows as part of .NET Framework 4.x, which is present on every
REM  Windows 10 and 11 installation.
REM
REM  Usage: double-click this file. EasyDPI.exe appears in the parent folder.
REM ---------------------------------------------------------------

setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo [ERROR] C# compiler not found.
    echo .NET Framework 4.x must be installed.
    pause
    exit /b 1
)

echo Building...
echo.

"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 ^
    /out:"..\EasyDPI.exe" ^
    /win32manifest:app.manifest ^
    /win32icon:icon.ico ^
    /resource:icon.ico,appicon ^
    /resource:logo.png,logo ^
    /resource:assets\hero-shield.png,hero-shield ^
    /resource:assets\ic-service.png,ic-service ^
    /resource:assets\ic-dns.png,ic-dns ^
    /resource:assets\ic-globe.png,ic-globe ^
    /resource:assets\ic-tune.png,ic-tune ^
    /resource:assets\ic-log.png,ic-log ^
    /resource:assets\ic-gear.png,ic-gear ^
    /resource:assets\ic-power.png,ic-power ^
    /resource:assets\ob-1.png,ob-1 ^
    /resource:assets\ob-2.png,ob-2 ^
    /resource:assets\ob-3.png,ob-3 ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.ServiceProcess.dll ^
    /reference:System.IO.Compression.dll ^
    /reference:System.IO.Compression.FileSystem.dll ^
    AssemblyInfo.cs ^
    Program.cs ^
    Strings.cs ^
    Settings.cs ^
    EmbeddedAssets.cs ^
    UiKit.cs ^
    TitleBar.cs ^
    PopupMenu.cs ^
    TabBar.cs ^
    FlagRenderer.cs ^
    ArtworkPanel.cs ^
    ServiceManager.cs ^
    NetworkTools.cs ^
    ProbeList.cs ^
    BypassController.cs ^
    Uninstaller.cs ^
    DiagnosticReport.cs ^
    UpdateCheck.cs ^
    Updater.cs ^
    AutoTuner.cs ^
    MainWindow.cs ^
    OnboardingWindow.cs

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo Done: ..\EasyDPI.exe
echo.
pause
