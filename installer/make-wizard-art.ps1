# Gera as imagens do assistente do instalador (BMP, formato que o Inno Setup exige) usando a
# paleta e as fontes REAIS do projeto — as mesmas Fraunces/Sora que o app usa, carregadas do
# diretório Fonts/ via PrivateFontCollection, sem depender de estarem instaladas no Windows.
#
# Rodar de novo só é necessário quando a identidade visual mudar; os .bmp ficam versionados.
param(
    [string]$OutDir = $PSScriptRoot
)

Add-Type -AssemblyName System.Drawing

$fontsDir = Join-Path $PSScriptRoot "..\desktop\AionixScribe\Fonts"
$fonts = New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Resolve-Path (Join-Path $fontsDir "Fraunces-VariableFont.ttf")).Path)
$fonts.AddFontFile((Resolve-Path (Join-Path $fontsDir "Sora-VariableFont.ttf")).Path)
$fraunces = $fonts.Families | Where-Object { $_.Name -like "Fraunces*" } | Select-Object -First 1
$sora     = $fonts.Families | Where-Object { $_.Name -like "Sora*" }     | Select-Object -First 1

# Tokens do Theme.Dark.xaml — a arte do instalador não pode divergir do app.
$bg      = [System.Drawing.Color]::FromArgb(0x18, 0x16, 0x1D)
$card    = [System.Drawing.Color]::FromArgb(0x21, 0x1F, 0x27)
$accent  = [System.Drawing.Color]::FromArgb(0xE8, 0x76, 0x3F)
$primary = [System.Drawing.Color]::FromArgb(0xF2, 0xF0, 0xED)
$muted   = [System.Drawing.Color]::FromArgb(0xA6, 0xA3, 0xAC)

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    # Fundo com leve profundidade vertical, na direção de elevação do tema escuro.
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $card, $bg, 90.0)
    $g.FillRectangle($brush, $rect)
    return @($bmp, $g)
}

# --- Imagem grande da lateral (164x314 é o nominal; 2x para telas HiDPI) ---
$w = 328; $h = 628
$c = New-Canvas $w $h
$bmp = $c[0]; $g = $c[1]

# Marca d'água: onda de áudio abstrata em accent, bem discreta (o app é sobre voz).
$pen = New-Object System.Drawing.Pen((
    [System.Drawing.Color]::FromArgb(38, $accent.R, $accent.G, $accent.B)), 6.0)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$rand = New-Object System.Random(7)
for ($i = 0; $i -lt 26; $i++) {
    $x = 34 + $i * 11
    $amp = 18 + $rand.Next(0, 92)
    $g.DrawLine($pen, $x, ($h - 150 - $amp), $x, ($h - 150 + $amp))
}

$fBrand = New-Object System.Drawing.Font($fraunces, 34, [System.Drawing.FontStyle]::Bold)
$fTag   = New-Object System.Drawing.Font($sora, 15)
$bPrim  = New-Object System.Drawing.SolidBrush($primary)
$bMuted = New-Object System.Drawing.SolidBrush($muted)
$bAcc   = New-Object System.Drawing.SolidBrush($accent)

$g.FillRectangle($bAcc, 34, 52, 56, 5)
$g.DrawString("Aionix", $fBrand, $bPrim, 30, 74)
$g.DrawString("Scribe", $fBrand, $bPrim, 30, 118)
# Acentuado montado por code point de propósito: o Windows PowerShell 5.1 lê arquivo .ps1 sem BOM
# como ANSI, e um "é" literal aqui vira "Ã©" impresso na arte do instalador (visto ao conferir a
# imagem gerada, não em teoria).
$g.DrawString(("Falar " + [char]0x00E9 + " uma forma"), $fTag, $bMuted, 34, 176)
$g.DrawString("superior de digitar.", $fTag, $bMuted, 34, 198)

$bmp.Save((Join-Path $OutDir "wizard-large.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g.Dispose(); $bmp.Dispose()

# --- Imagem pequena do topo (55x58 nominal; 2x) ---
$w2 = 110; $h2 = 116
$c2 = New-Canvas $w2 $h2
$bmp2 = $c2[0]; $g2 = $c2[1]
$pen2 = New-Object System.Drawing.Pen($accent, 7.0)
$pen2.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen2.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$heights = @(16, 30, 44, 26, 12)
for ($i = 0; $i -lt $heights.Count; $i++) {
    $x = 23 + $i * 16
    $g2.DrawLine($pen2, $x, (58 - $heights[$i]), $x, (58 + $heights[$i]))
}
$bmp2.Save((Join-Path $OutDir "wizard-small.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g2.Dispose(); $bmp2.Dispose()

Write-Output "Arte do instalador gerada em $OutDir"
