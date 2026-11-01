param([switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$publishArguments = @('publish','src/LingNote','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=false','-o','app')
if ($SkipRestore) { $publishArguments += '--no-restore' } else { $publishArguments += @('--configfile','NuGet.Config') }
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw '发布失败' }
if(Test-Path -LiteralPath 'library-location.json') { Copy-Item -LiteralPath 'library-location.json' -Destination 'app/library-location.json' }
New-Item -ItemType Directory -Force app/models,app/licenses | Out-Null
Copy-Item assets/model-manifest.json app/models/
Copy-Item assets/USER-GUIDE.md app/README.md
Copy-Item assets/THIRD-PARTY-NOTICES.txt app/
Copy-Item assets/Geist-OFL.txt app/licenses/
Copy-Item assets/Markdig-LICENSE.txt app/licenses/
Copy-Item assets/WHISPER-LICENSE.txt app/licenses/
Copy-Item .packages/naudio/2.2.1/license.txt app/licenses/NAudio.txt
Copy-Item .packages/whisper.net/1.9.1/LICENSE app/licenses/Whisper.net.txt
Copy-Item .packages/microsoft.windowsdesktop.app.runtime.win-x64/10.0.12/LICENSE app/licenses/WindowsDesktop.txt
& tools/Inno/ISCC.exe tools/installer.iss
if ($LASTEXITCODE -ne 0) { throw '安装包编译失败' }
$releaseInstaller = Get-Item artifacts/0.2.0/LingNote-0.2.0-Setup.exe
$releaseHash = (Get-FileHash -LiteralPath $releaseInstaller.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($releaseInstaller.FullName+'.sha256', $releaseHash+'  '+$releaseInstaller.Name+[Environment]::NewLine, [Text.Encoding]::ASCII)
Get-FileHash -LiteralPath $releaseInstaller.FullName -Algorithm SHA256 | Format-List

