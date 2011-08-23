@ECHO OFF

echo Copying DLLs

xcopy ..\..\..\Dependencies\Lib\ReleaseWin32\libzmq.dll Release\ /d /y

xcopy ..\..\..\Dependencies\Lib\DebugWin32\libzmq.dll Debug\ /d /y

pause