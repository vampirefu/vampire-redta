rem 通过注册表获取桌面路径
set desk_rq=REG QUERY "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders" /v "Desktop"
for /F "tokens=3,*" %%d in ('%desk_rq%') do set desk_path=%%d

set dtaDllDir=%desk_path%\AGWar1.3.0\Resources\Binaries\Windows
set TargetPath=%1
set TargetDir=%2

echo TargetPath:%TargetPath%
echo TargetDir:%TargetDir%
echo SrcPath:%TargetPath%
echo CopyDir:%dtaDllDir%

if exist %dtaDllDir% xcopy /y /e /d /f %TargetPath% %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d /f %TargetDir%\ClientCore.dll %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d /f %TargetDir%\ClientGUI.dll %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d /f %TargetDir%\DTAConfig.dll %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d /f %TargetDir%\DTAClient.Online.Core.dll %dtaDllDir%
rem LoadingScreen 的视频背景使用 LibVLCSharp.WinForms 播放 mp4。
rem clientdx.dll 运行在 %dtaDllDir%，所以托管 DLL 必须跟 clientdx.dll 放在同一目录，
rem 否则 CLR 加载 LoadingScreen 时会找不到 LibVLCSharp.dll / LibVLCSharp.WinForms.dll。
if exist %dtaDllDir% xcopy /y /e /d /f %TargetDir%\LibVLCSharp.dll %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d /f %TargetDir%\LibVLCSharp.WinForms.dll %dtaDllDir%

rem VideoLAN.LibVLC.Windows 会在输出目录生成 libvlc\win-x64、libvlc\win-x86、
rem libvlc\win-arm64 以及 plugins 子目录。LibVLCSharp 初始化时需要这些原生文件，
rem 只复制 DLL 不复制 plugins 会导致 mp4 解码器、demuxer 或视频输出插件缺失。
if exist %dtaDllDir% xcopy /y /e /d /f %TargetDir%\libvlc %dtaDllDir%\libvlc\
