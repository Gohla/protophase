cd ..
copy Mono\Registry\bin\Debug\Registry.exe Mono\Registry\bin\Debug\Registry2.exe
:TOP
start "window1" /min WindowsTestcases\StartAltRegistry2.bat /c
ping 127.0.0.1 -n 2
taskkill -IM Registry2.exe /F
GOTO TOP 