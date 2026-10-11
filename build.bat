@echo off
setlocal
rem Lang2Launcher 빌드 스크립트
rem Windows 기본 .NET Framework 4.x 컴파일러(csc.exe)를 사용합니다. 별도 설치 불필요.
chcp 65001 >nul
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [오류] .NET Framework 4.x 컴파일러를 찾을 수 없습니다.
  exit /b 1
)

if not exist dist mkdir dist

set "ICON="
if exist icon.ico set "ICON=/win32icon:icon.ico"

"%CSC%" /nologo /target:winexe /codepage:65001 /optimize+ /platform:anycpu %ICON% ^
  /out:dist\Lang2Launcher.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\Lang2Launcher.cs
if errorlevel 1 (
  echo [오류] 빌드 실패
  exit /b 1
)
echo 빌드 완료: dist\Lang2Launcher.exe
