# Builds Vibling, installs it to %LOCALAPPDATA%\Programs\Vibling and adds Desktop and Start menu shortcuts.
# Собирает Вайблинга, ставит в %LOCALAPPDATA%\Programs\Vibling и кладёт ярлыки на рабочий стол и в «Пуск».
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$langs = @((Get-UICulture).Name) + @((Get-WinUserLanguageList)[0].LanguageTag)
$ru = [bool](($langs | Where-Object { $_ -match '^(ru|uk|be|kk)' }))
$name = if ($ru) { 'Вайблинг' } else { 'Vibling' }
$settingsName = if ($ru) { 'Настройки Вайблинга' } else { 'Vibling Settings' }
$desc = if ($ru) { 'Питомец для рабочего стола' } else { 'A tiny desktop pet' }

Stop-Process -Name Vibling, Vibobus -Force -ErrorAction SilentlyContinue
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o out | Out-Null

$dest = Join-Path $env:LOCALAPPDATA 'Programs\Vibling'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item .\out\Vibling.exe $dest -Force
$exe = Join-Path $dest 'Vibling.exe'

$desktop = [Environment]::GetFolderPath('Desktop')
$programs = [Environment]::GetFolderPath('Programs')

# Old name cleanup (the app used to be called Vibobus) / убираем старую версию под прежним названием
Remove-Item (Join-Path $env:LOCALAPPDATA 'Programs\Vibobus') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $desktop 'Вайбобус.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $programs 'Вайбобус') -Recurse -Force -ErrorAction SilentlyContinue

$shell = New-Object -ComObject WScript.Shell
function New-Shortcut($path, $arguments, $description) {
    $lnk = $shell.CreateShortcut($path)
    $lnk.TargetPath = $exe
    $lnk.Arguments = $arguments
    $lnk.WorkingDirectory = $dest
    $lnk.IconLocation = "$exe,0"
    $lnk.Description = $description
    $lnk.Save()
}

$startMenu = Join-Path $programs $name
New-Item -ItemType Directory -Force $startMenu | Out-Null
New-Shortcut (Join-Path $desktop "$name.lnk") '' $desc
New-Shortcut (Join-Path $startMenu "$name.lnk") '' $desc
New-Shortcut (Join-Path $startMenu "$settingsName.lnk") '--settings' $desc

Start-Process $exe
"Installed / установлено: $exe"
