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

# FONTE ÚNICA do "o que mudou": um .json estruturado por versão (releases/<versao>.json). Deste
# arquivo saem AS DUAS coisas — o corpo em markdown do release no GitHub e o update.json que o app
# lê para montar o painel de novidades. Se cada um fosse escrito à mão, eles divergiriam e o painel
# dentro do app passaria a mentir sobre o que mudou.
$notes = Get-Content $NotesFile -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($field in @("version", "headline", "summary", "highlights")) {
    if (-not $notes.$field) { throw "Notas incompletas: falta o campo '$field' em $NotesFile" }
}

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

# O binário manda; as notas só acompanham. Divergência aqui significa que alguém esqueceu de subir
# <Version> no .csproj (ou escreveu a versão errada nas notas) — e o resultado seria um update.json
# anunciando uma versão que o instalador não entrega.
if ($notes.version -ne $version) {
    throw "Versao das notas ($($notes.version)) difere da versao compilada ($version). Ajuste <Version> no .csproj ou o campo 'version' em $NotesFile."
}

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
# Nada de "2>$null" aqui: no Windows PowerShell 5.1, redirecionar o stderr de um executavel nativo
# embrulha cada linha num ErrorRecord e, com ErrorActionPreference=Stop, derruba o script — mesmo
# quando o "erro" e a resposta ESPERADA ("release not found", isto e, pode publicar).
$prev = $ErrorActionPreference
$ErrorActionPreference = "Continue"
gh release view $tag --json tagName *> $null
$alreadyPublished = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $prev
if ($alreadyPublished) {
    throw "Ja existe um release com a tag $tag. Suba <Version> no .csproj antes de publicar de novo."
}

# Duas cópias do MESMO instalador, de propósito:
#  - com versão no nome: é o que fica no histórico de releases, identificável sem abrir;
#  - com nome fixo: /releases/latest/download/AionixScribe-Setup.exe é um link PERMANENTE. O botão
#    de download do site (e qualquer link que alguém compartilhar) aponta pra ele e nunca quebra
#    quando sai versão nova. Com o nome versionado, cada release exigiria trocar o link do site.
$stable = Join-Path $distDir "AionixScribe-Setup.exe"
Copy-Item $setup $stable -Force

# update.json — o que o app instalado lê para saber que existe versão nova e o que ela traz.
# setupUrl aponta para o ativo VERSIONADO deste release (não para /latest/), porque o sha256 abaixo
# é o hash exato deste arquivo: com /latest/, o manifesto da 0.3.0 passaria a apontar para o
# instalador da 0.4.0 assim que ela saísse, e a verificação de integridade falharia.
$manifest = [ordered]@{
    version     = $version
    releasedAt  = (Get-Date -Format "dd/MM/yyyy")
    headline    = $notes.headline
    summary     = $notes.summary
    highlights  = @($notes.highlights | ForEach-Object { [ordered]@{ title = $_.title; body = $_.body } })
    setupUrl    = "https://github.com/alanaraujo-bit/Aionix-Scribe/releases/download/$tag/AionixScribe-Setup-$version.exe"
    setupSha256 = $sha
}
$manifestPath = Join-Path $distDir "update.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding utf8

# Corpo do release no GitHub, renderizado das MESMAS notas.
$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("## $($notes.headline)")
[void]$md.AppendLine()
[void]$md.AppendLine($notes.summary)
[void]$md.AppendLine()
[void]$md.AppendLine("### O que mudou")
[void]$md.AppendLine()
foreach ($h in $notes.highlights) { [void]$md.AppendLine("- **$($h.title)** — $($h.body)") }
[void]$md.AppendLine()
[void]$md.AppendLine("### Instalação")
[void]$md.AppendLine()
[void]$md.AppendLine("Baixe e execute ``AionixScribe-Setup.exe``. Quem já tem o Aionix Scribe instalado recebe o aviso de atualização dentro do próprio app.")
[void]$md.AppendLine()
[void]$md.AppendLine("O Windows SmartScreen avisa que o editor é desconhecido — o instalador ainda não é assinado digitalmente. Clique em ""Mais informações"" e depois em ""Executar assim mesmo"".")
$bodyPath = Join-Path $distDir "release-body.md"
$md.ToString() | Set-Content $bodyPath -Encoding utf8

Write-Host "==> Criando release $tag no GitHub..." -ForegroundColor Cyan
gh release create $tag $setup $stable $manifestPath --title "Aionix Scribe $version" --notes-file $bodyPath
if ($LASTEXITCODE -ne 0) { throw "gh release create falhou" }

Write-Host ""
Write-Host "Release publicada: $tag" -ForegroundColor Green
Write-Host "Link permanente de download (usar no site):"
Write-Host "  https://github.com/alanaraujo-bit/Aionix-Scribe/releases/latest/download/AionixScribe-Setup.exe"
