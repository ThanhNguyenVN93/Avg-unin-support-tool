@echo off
setlocal EnableDelayedExpansion

set "PROJECT=frm_avg_unin_support_tool.csproj"
set "CONFIG=Release"
set "MSBUILD="

rem --- Detect MSBuild via vswhere (ships with VS 2017+) -----------------------
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"

if exist "%VSWHERE%" (
    for /f "usebackq delims=" %%i in (
        `"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`
    ) do (
        if exist "%%i" ( set "MSBUILD=%%i" & goto :found )
    )
)

rem --- Fallback: well-known VS paths ------------------------------------------
for %%V in (2022 2019 2017) do (
    for %%E in (Professional Enterprise Community BuildTools) do (
        set "_TRY=%ProgramFiles%\Microsoft Visual Studio\%%V\%%E\MSBuild\Current\Bin\MSBuild.exe"
        if exist "!_TRY!" ( set "MSBUILD=!_TRY!" & goto :found )
        set "_TRY=%ProgramFiles(x86)%\Microsoft Visual Studio\%%V\%%E\MSBuild\Current\Bin\MSBuild.exe"
        if exist "!_TRY!" ( set "MSBUILD=!_TRY!" & goto :found )
    )
)

rem --- Fallback: PATH ---------------------------------------------------------
where msbuild >nul 2>&1
if %errorlevel%==0 (
    for /f "delims=" %%i in ('where msbuild') do (
        set "MSBUILD=%%i" & goto :found
    )
)

echo.
echo  [ERROR] MSBuild not found.
echo  Install Visual Studio 2017 or later (any edition), or the
echo  "Build Tools for Visual Studio" standalone package, then retry.
echo.
pause
exit /b 1

:found
echo.
echo  MSBuild : %MSBUILD%
echo  Project : %PROJECT%
echo  Config  : %CONFIG%
echo.

rem --- Restore NuGet packages -------------------------------------------------
if exist "%~dp0nuget.exe" (
    echo [1/2] Restoring NuGet packages...
    "%~dp0nuget.exe" restore "%PROJECT%" -PackagesDirectory packages
    if errorlevel 1 (
        echo.
        echo  [WARNING] NuGet restore had errors. Build may still succeed
        echo            if packages are already present in .\packages\.
        echo.
    )
) else (
    echo [1/2] nuget.exe not found - skipping restore.
    echo.
)

rem --- Build ------------------------------------------------------------------
echo [2/2] Building %CONFIG%...
echo.
"%MSBUILD%" "%PROJECT%" /p:Configuration=%CONFIG% /v:minimal /nologo
set "BUILD_ERR=%errorlevel%"

echo.
set "EXE=%~dp0bin\%CONFIG%\Avg unin support tool.exe"
if %BUILD_ERR%==0 (
    echo  [OK] Build succeeded.
    if exist "%EXE%" (
        echo  EXE : %EXE%
        for %%F in ("%EXE%") do echo  Size: %%~zF bytes
    ) else (
        echo  Output: bin\%CONFIG%\
    )
) else (
    echo  [FAIL] Build failed ^(exit code %BUILD_ERR%^).
    echo         Check the errors above, then run build.bat again.
)

echo.
pause
exit /b %BUILD_ERR%
