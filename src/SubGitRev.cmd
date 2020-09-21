@echo off
setlocal

set PATH=%USERPROFILE%\appdata\local\Atlassian\SourceTree\git_local\bin;%PATH%
set PATH=C:\Program Files\Git\bin;%PATH%

echo SubGitRev

if "%2" == "" (
  echo Usage: SubGitRev WorkingCopyPath [SrcVersionFile DstVersionFile]
  echo.
  echo Params:
  echo WorkingCopyPath    :   path to a Subversion working copy.
  echo SrcVersionFile     :   path to a template file containing keywords.
  echo DstVersionFile     :   path to save the resulting parsed file.
  echo.
  echo SrcVersionFile is then copied to DstVersionFile but the placeholders
  echo are replaced with information about the working copy as follows:
  echo.
  echo ${REV_COUNT}     Revision number
  echo ${REV_DATE}      Revision date     
  echo ${REV_HASH}      Revision hash
  echo ${REV_URL}       Repository URL
  echo ${REV_DIRTY}     'M' when there is any change in current worktree
  echo ${APP_REV}       ${REV_DIRTY}${REV_COUNT}-${REV_HASH}
  
  exit /b 1
)

git -C %1 rev-list HEAD --count > %2tmp.tmp 2>nul

set /p REV_COUNT=<%2tmp.tmp

if "%REV_COUNT%" == "" (
  echo Not int GIT repository
  del %2tmp.tmp
  endlocal
  exit /b 1
)

git -C %1 log -1 --date=iso-local --pretty=format:%%cd > %2tmp.tmp
set /p REV_DATE=<%2tmp.tmp

git -C %1 log -1 --pretty=format:%%h --abbrev=8 > %2tmp.tmp
set /p REV_HASH=<%2tmp.tmp

git -C %1 config --get remote.origin.url > %2tmp.tmp
set /p REV_URL=<%2tmp.tmp

del %2tmp.tmp

git -C %1 diff --exit-code --quiet
if %errorlevel% == 0 (
  set REV_DIRTY=0
) else (
  set REV_DIRTY=1
)

set APP_REV=%REV_DIRTY%%REV_COUNT%-%REV_HASH%

echo Revision: "%REV_DIRTY%%REV_COUNT%-%REV_HASH%"

sh -c envsubst < %3 > %2tmp.tmp

:: Update file only when changed
fc "%4" %2tmp.tmp > nul 2>nul
if errorlevel 1 (
  copy %2tmp.tmp "%4" > nul
)

del %2tmp.tmp
endlocal
exit /b 0
