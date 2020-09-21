@echo off

set Directory=%1
set InputFileName=%2%
set OutputFileName=%3%


set GitDir=%Directory%

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
	"%~dp0SubGitRev.cmd" %GitDir% %Directory% %InputFileName%.git.tmpl %OutputFileName%
) else (
	echo "git not found"

	if exist "%ProgramW6432%\TortoiseSVN\bin\SubWCRev.exe" (
		echo "Update 64 Bit -> %ProgramW6432%\TortoiseSVN\bin\SubWCRev.exe"
		"%ProgramW6432%\TortoiseSVN\bin\SubWCRev" %Directory%.. %InputFileName%.svn.tmpl %OutputFileName%

	) else (
		echo Update 64 Bit not found

		if DEFINED "%ProgramFiles(x86)%" (
			echo "Update 32 Bit -> %ProgramFiles(x86)%\TortoiseSVN\bin\SubWCRev"
			"%ProgramFiles(x86)%\TortoiseSVN\bin\SubWCRev" %Directory%.. %InputFileName%.svn.tmpl %OutputFileName%
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


