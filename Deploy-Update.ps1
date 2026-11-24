#Requires -Version 5.1
<#
.SYNOPSIS
    ERSoftSMSBox güncelleme paketini derleyip sunucuya yükler.
.PARAMETER Version
    Yayımlanacak sürüm numarası (örn. 1.0.1). Belirtilmezse AppVersion.cs'ten okunur.
.PARAMETER Notes
    Sürüm notları. İsteğe bağlı.
.PARAMETER ServerUrl
    Güncelleme sunucusunun kök URL'si. Varsayılan: http://ec2-51-21-196-155.eu-north-1.compute.amazonaws.com:8080
.EXAMPLE
    .\Deploy-Update.ps1 -Version "1.1.0" -Notes "Güncelleme ekranı eklendi"
#>
param(
    [string]$Version = "",
    [string]$Notes = "",
    [string]$ServerUrl = "http://ec2-51-21-196-155.eu-north-1.compute.amazonaws.com:8080",
    [string]$UploadSecret = $env:ERSOFT_UPLOAD_SECRET
)

$ErrorActionPreference = "Stop"
if (-not $UploadSecret) {
    Write-Error "Upload secret bulunamadı. ERSOFT_UPLOAD_SECRET ortam değişkenini veya -UploadSecret parametresini belirleyin."
    exit 1
}
$ProjectDir = $PSScriptRoot
$PublishDir = Join-Path $env:TEMP "ERSoftPublish_$(Get-Date -Format 'yyyyMMddHHmmss')"
$ZipPath = Join-Path $env:TEMP "ERSoftUpdate.zip"

# Sürüm numarasını AppVersion.cs'ten oku (parametre verilmezse)
if (-not $Version) {
    $versionFile = Join-Path $ProjectDir "AppVersion.cs"
    if (Test-Path $versionFile) {
        $match = Select-String -Path $versionFile -Pattern 'Current\s*=\s*"([^"]+)"'
        if ($match) { $Version = $match.Matches[0].Groups[1].Value }
    }
    if (-not $Version) {
        Write-Error "Sürüm numarası bulunamadı. -Version parametresini belirtin."
        exit 1
    }
}

Write-Host "=== ERSoftSMSBox Dağıtım Aracı ===" -ForegroundColor Cyan
Write-Host "Sürüm   : $Version"
Write-Host "Sunucu  : $ServerUrl"
Write-Host ""

# 1) Publish
Write-Host "[1/4] Proje derleniyor..." -ForegroundColor Yellow
$null = New-Item -ItemType Directory -Force -Path $PublishDir
dotnet publish "$ProjectDir\ERSoftSMSBox.csproj" -c Release -r win-x64 --self-contained false -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "Derleme başarısız."; exit 1 }
Write-Host "      Tamam. ($PublishDir)" -ForegroundColor Green

# 2) ZIP
Write-Host "[2/4] Paket oluşturuluyor..." -ForegroundColor Yellow
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
$sizeMB = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host "      Tamam. ($ZipPath, $sizeMB MB)" -ForegroundColor Green

# 3) Sunucu erişilebilirlik kontrolü
Write-Host "[3/4] Sunucu kontrol ediliyor..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$ServerUrl/health" -TimeoutSec 10
    Write-Host "      Sunucu aktif: $($health.status)" -ForegroundColor Green
} catch {
    Write-Error "Sunucuya erişilemiyor: $_"
    exit 1
}

# 4) Yükle
Write-Host "[4/4] Paket yükleniyor (v$Version)..." -ForegroundColor Yellow
$boundary = [System.Guid]::NewGuid().ToString()
$filePath = $ZipPath
$fileName = "ERSoftSMSBox.zip"
$fileBytes = [System.IO.File]::ReadAllBytes($filePath)
$encoding = [System.Text.Encoding]::UTF8

$body = [System.IO.MemoryStream]::new()
$writer = [System.IO.StreamWriter]::new($body, $encoding)

$addField = {
    param($name, $value)
    $writer.Write("--$boundary`r`n")
    $writer.Write("Content-Disposition: form-data; name=`"$name`"`r`n`r`n")
    $writer.Write("$value`r`n")
    $writer.Flush()
}

& $addField "version" $Version
if ($Notes) { & $addField "notes" $Notes }

$writer.Write("--$boundary`r`n")
$writer.Write("Content-Disposition: form-data; name=`"package`"; filename=`"$fileName`"`r`n")
$writer.Write("Content-Type: application/zip`r`n`r`n")
$writer.Flush()
$body.Write($fileBytes, 0, $fileBytes.Length)
$writer.Write("`r`n--$boundary--`r`n")
$writer.Flush()

$bodyBytes = $body.ToArray()
$body.Dispose()

try {
    $response = Invoke-RestMethod -Uri "$ServerUrl/upload" -Method Post `
        -Body $bodyBytes `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Headers @{ "X-Upload-Key" = $UploadSecret } `
        -TimeoutSec 120
    Write-Host ""
    Write-Host "=== YÜKLEME BAŞARILI ===" -ForegroundColor Green
    Write-Host "Sürüm  : $($response.version)"
    Write-Host "Boyut  : $([math]::Round($response.size / 1MB, 1)) MB"
} catch {
    Write-Error "Yükleme başarısız: $_"
    exit 1
} finally {
    Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
}
