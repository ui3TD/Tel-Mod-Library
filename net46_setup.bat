@echo off
setlocal enabledelayedexpansion

rem Get the current directory
set "parentDirectory=%cd%"

rem Loop through each directory (excluding "dll" and ".vs")
for /D %%d in ("%parentDirectory%\*") do (
    if /I not "%%~nxd"=="dll" if /I not "%%~nxd"==".vs" (
        echo Processing directory: %%d
        cd /d %%d
        dotnet add package Microsoft.NETFramework.ReferenceAssemblies.net46 --version 1.0.3
        cd /d "%parentDirectory%"
    )
)
