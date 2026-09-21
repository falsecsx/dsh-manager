@echo off
setlocal
rem ============================================================
rem  DeepSeek Harness Manager - one-click build script
rem  Output: DSH Manager.exe (.NET Framework 4.x, AnyCPU)
rem  Requires: csc.exe and .NET Framework 4.x runtime
rem ============================================================
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. Install .NET Framework 4.x first.
  exit /b 1
)

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:dsh-manager.ico /resource:dsh-gpt-compat.cjs,DeepSeekHarness.GptCompat.cjs /out:"DSH Manager.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Management.dll dsh-manager.cs

if errorlevel 1 (
  echo.
  echo [FAILED] Compile error.
  exit /b 1
)

echo.
echo [OK] Build succeeded: DSH Manager.exe
exit /b 0
