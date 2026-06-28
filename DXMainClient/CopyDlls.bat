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