rem 通过注册表获取桌面路径
set desk_rq=REG QUERY "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders" /v "Desktop"
for /F "tokens=3,*" %%d in ('%desk_rq%') do set desk_path=%%d

set dtaDllDir=%desk_path%\AGWarDTA\Resources\Binaries\Windows
set TargetPath=%1
set TargetDir=%2

echo TargetPath:%TargetPath%
echo TargetDir:%TargetDir%
echo SrcPath:%TargetPath%
echo CopyDir:%dtaDllDir%

if exist %dtaDllDir% xcopy /y /e /d %TargetPath% %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d %TargetDir%\ClientCore.dll %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d %TargetDir%\ClientGUI.dll %dtaDllDir%
if exist %dtaDllDir% xcopy /y /e /d %TargetDir%\DTAConfig.dll %dtaDllDir%