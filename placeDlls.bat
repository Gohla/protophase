@echo off
cls
REM -- Add all projects in here.

echo Copying dll files to appropriate folders...
set debugsrc=Dependencies\Lib\DebugWin32\libzmq.dll
set rlssrc=Dependencies\Lib\ReleaseWin32\libzmq.dll


set destprefix=Mono\
set destsuffixdebug=\bin\Debug\
set destsuffixrls=\bin\Release\


set proj=Registry
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Service
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

echo Copying Ais examples


set proj=Examples\AisServices\AisDataChecksumStatistics
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\AisServices\AisDecoder
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\AisServices\AisKMLDumperRPC
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\AisServices\AisKMLDumperSub
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\AisServices\AisReceiverServiceRPC
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\AisServices\RawAisService
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\AisServices\RawAisServiceTestdata
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y


echo Copying Examples


set proj=Examples\PubSubServiceFailureReplace
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\RegistryFailureTest
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y


set proj=Examples\RegistryStresstester
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\RPCServiceFailureReplace
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\SimpleRPCClient
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\SimpleRPCServer
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\TestServiceClient
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

set proj=Examples\TestServiceServer
xcopy "%debugsrc%" "%destprefix%%proj%%destsuffixdebug%" /Y
xcopy "%rlssrc%" "%destprefix%%proj%%destsuffixrls%" /Y

echo  Finished!
pause