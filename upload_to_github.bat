@echo off
setlocal

cd /d "%~dp0"

where git >nul 2>nul
if errorlevel 1 (
    echo Git was not found in PATH.
    pause
    exit /b 1
)

git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
    echo This folder is not a Git repository.
    pause
    exit /b 1
)

for /f "delims=" %%b in ('git branch --show-current') do set "BRANCH=%%b"
if "%BRANCH%"=="" (
    echo Could not detect the current Git branch.
    pause
    exit /b 1
)

git remote get-url origin >nul 2>nul
if errorlevel 1 (
    git remote add origin https://github.com/cpy114514/Bullet-Foundry.git
) else (
    git remote set-url origin https://github.com/cpy114514/Bullet-Foundry.git
)

echo.
echo Current branch: %BRANCH%
echo Remote: https://github.com/cpy114514/Bullet-Foundry.git
echo.
git status --short
echo.

set "COMMIT_MSG=Update project"
set /p COMMIT_MSG=Commit message [Update project]: 
if "%COMMIT_MSG%"=="" set "COMMIT_MSG=Update project"

git add -A
if errorlevel 1 goto fail

git diff --cached --quiet
if errorlevel 1 (
    git commit -m "%COMMIT_MSG%"
    if errorlevel 1 goto fail
) else (
    echo No changes to commit.
)

git push -u origin "%BRANCH%"
if errorlevel 1 goto fail

echo.
echo Upload complete.
pause
exit /b 0

:fail
echo.
echo Upload failed. Check the error message above.
pause
exit /b 1
