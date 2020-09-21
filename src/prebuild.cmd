@echo off

set SolutionDir=%1%
set ProjectDir=%2%

echo %SolutionDir%
echo %ProjectDir%

REM SET

REM DEL %ProjectDir%Properties\AssemblyInfo.cs

set GitDir=%SolutionDir%

:loop

pushd %GitDir%
set abs=%CD%

if exist "%abs%\.git" (
	echo "git found at %CD%"
	set GitDir=%CD%
	goto endloop	
)

set GitDir=%GitDir=%..\
if NOT "%abs:~3,1%"=="" goto loop
:endloop

if exist "%GitDir%\.git" (
	"%~dp0SubGitRev.cmd" %GitDir% %ProjectDir% %ProjectDir%Properties\AssemblyInfo.tmpl.git.cs %ProjectDir%Properties\AssemblyInfo.cs 
) else (
	echo "git not found"

	if exist "%ProgramW6432%\TortoiseSVN\bin\SubWCRev.exe" (
		echo "Update 64 Bit -> %ProgramW6432%\TortoiseSVN\bin\SubWCRev.exe"
		"%ProgramW6432%\TortoiseSVN\bin\SubWCRev" %SolutionDir%.. %ProjectDir%Properties\AssemblyInfo.tmpl.svn.cs %ProjectDir%Properties\AssemblyInfo.cs

	) else (
		echo Update 64 Bit not found

		if DEFINED "%ProgramFiles(x86)%" (
			echo "Update 32 Bit -> %ProgramFiles(x86)%\TortoiseSVN\bin\SubWCRev"
			"%ProgramFiles(x86)%\TortoiseSVN\bin\SubWCRev" %SolutionDir%.. %ProjectDir%Properties\AssemblyInfo.tmpl.svn.cs %ProjectDir%Properties\AssemblyInfo.cs
		) else (
			echo Update 32 Bit not found
		)
	)
)

if %errorlevel% EQU 0 (
	echo Update of AssemblyInfo successfull
) else (
	echo "Fehler: " %errorlevel%
	goto end
)

goto end

:end


