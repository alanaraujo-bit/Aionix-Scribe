# Publica uma versão do Aionix Scribe: compila, empacota o instalador e cria o release no GitHub.
#
# A versão NÃO é passada por parâmetro de propósito: ela vem de <Version> no .csproj, é carimbada
# no .exe pelo compilador, e daí é lida pelo instalador e por este script. Um número, uma origem.
# Para lançar uma versão nova, edite <Version> no .csproj e rode este script.
#
# Uso:
#   .\scripts\release.ps1 -NotesFile .\notas-0.2.0.md
#   .\scripts\release.ps1 -NotesFile .\notas-0.2.0.md -DryRun    (compila tudo, não publica)

param(
    [Parameter(Mandatory = $true)][string]$NotesFile,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$projectDir = Join-Path $root "desktop\AionixScribe"
$publishDir = Join-Path $projectDir "bin\Release\net8.0-windows\win-x64\publish"
$distDir = Join-Path $root "dist"

if (-not (Test-Path $NotesFile)) { throw "Arquivo de notas nao encontrado: $NotesFile" }

$iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { throw "Inno Setup nao encontrado. Instale com: winget install JRSoftware.InnoSetup" }

# O app em execução trava o próprio .exe e faz o publish falhar no meio — mesmo erro que aparece
# ao recompilar com o programa aberto.
Get-Process AionixScribe -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
Start-Sleep -Seconds 2

Write-Host "==> Publicando (self-contained, win-x64)..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou" }
}
finally { Pop-Location }

$version = (Get-Item (Join-Path $publishDir "AionixScribe.exe")).VersionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($version)) { throw "Nao foi possivel ler a versao do binario publicado" }
Write-Host "    versao detectada: $version" -ForegroundColor Green

Write-Host "==> Gerando arte do assistente..." -ForegroundColor Cyan
& (Join-Path $root "installer\make-wizard-art.ps1") | Out-Null

Write-Host "==> Compilando instalador..." -ForegroundColor Cyan
& $iscc (Join-Path $root "installer\AionixScribe.iss") | Select-String "Successful compile|Error"
if ($LASTEXITCODE -ne 0) { throw "Compilacao do instalador falhou" }

$setup = Join-Path $distDir "AionixScribe-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "Instalador nao encontrado em $setup" }
$sizeMb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
$sha = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()
Write-Host "    $setup ($sizeMb MB)" -ForegroundColor Green
Write-Host "    sha256: $sha" -ForegroundColor Green

$tag = "v$version"

if ($DryRun) {
    Write-Host "==> DryRun: parando antes de publicar. Tag que seria criada: $tag" -ForegroundColor Yellow
    return
}

# Existe release com essa tag? Publicar por cima em silencio esconderia um erro de versionamento
# (esquecer de subir <Version> no .csproj e sobrescrever o release anterior).
$existing = gh release view $tag --json tagName 2>$null
if ($LASTEXITCODE -eq 0) {
    throw "Ja existe um release com a tag $tag. Suba <Version> no .csproj antes de publicar de novo."
}

# Duas cópias do MESMO instalador, de propósito:
#  - com versão no nome: é o que fica no histórico de releases, identificável sem abrir;
#  - com nome fixo: /releases/latest/download/AionixScribe-Setup.exe é um link PERMANENTE. O botão
#    de download do site (e qualquer link que alguém compartilhar) aponta pra ele e nunca quebra
#    quando sai versão nova. Com o nome versionado, cada release exigiria trocar o link do site.
$stable = Join-Path $distDir "AionixScribe-Setup.exe"
Copy-Item $setup $stable -Force

Write-Host "==> Criando release $tag no GitHub..." -ForegroundColor Cyan
gh release create $tag $setup $stable --title "Aionix Scribe $version" --notes-file $NotesFile
if ($LASTEXITCODE -ne 0) { throw "gh release create falhou" }

Write-Host ""
Write-Host "Release publicada: $tag" -ForegroundColor Green
Write-Host "Link permanente de download (usar no site):"
Write-Host "  https://github.com/alanaraujo-bit/Aionix-Scribe/releases/latest/download/AionixScribe-Setup.exe"
