$configBuild = "Debug"

$path = "C:\Code\GitHubRepos"

$pathExecuter = "$path\SyncMLViewer\SyncMLViewer.Executer\bin\x64\$configBuild\SyncMLViewer.Executer.exe" 
$pathExecuterHashOutput = "$path\SyncMLViewer\SyncMLViewer\SyncMLViewer.Executer.exe.hash"

$(Get-FileHash -Path $pathExecuter -Algorithm SHA256).Hash | Out-File $pathExecuterHashOutput -Encoding ascii