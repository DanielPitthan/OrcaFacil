# O Android System WebView 83 (emulador sem atualização da Play Store) não entende
# blocos static {} nem atribuição ||= / ??=. O blazor.webview.js do .NET 10 usa os três,
# o script falha com "Unexpected token '{'" e a página permanece em "Carregando...".
# As substituições abaixo são equivalentes e cabem no WebView 83.

$ErrorActionPreference = "Stop"

$perfil = $env:USERPROFILE
if ([string]::IsNullOrEmpty($perfil)) {
    $perfil = $env:HOME
}

$pastaNuget = Join-Path $perfil ".nuget"
$pastaPacotes = Join-Path $pastaNuget "packages"
$pastaWebView = Join-Path $pastaPacotes "microsoft.aspnetcore.components.webview"
$pastaPai = Join-Path $PSScriptRoot ".."
$pastaObj = Join-Path $pastaPai "obj"

$raizes = @(
    $pastaWebView,
    $pastaObj
)

$arquivos = foreach ($raiz in $raizes) {
    if (Test-Path $raiz) {
        Get-ChildItem -Path $raiz -Recurse -Filter "blazor.webview.js" -ErrorAction SilentlyContinue
    }
}

if (-not $arquivos) {
    Write-Host "blazor.webview.js não encontrado."
    exit 1
}

$restouStatic = $false
foreach ($arquivo in $arquivos) {
    $texto = [System.IO.File]::ReadAllText($arquivo.FullName)
    $ajustado = $texto.Replace('static{this.nextEventDelegatorId=0}', 'static nextEventDelegatorId=0;')
    $ajustado = $ajustado.Replace('static nextEventDelegatorId=0constructor', 'static nextEventDelegatorId=0;constructor')
    $ajustado = $ajustado.Replace('t||=[]', 't=t||[]')
    $ajustado = $ajustado.Replace('t[Ue]??={}', '(t[Ue]=t[Ue]??{})')

    if ($ajustado.Contains('static{')) {
        Write-Host "O padrão static{ permaneceu em $($arquivo.FullName)"
        $restouStatic = $true
        continue
    }

    if ($ajustado -eq $texto) {
        continue
    }

    if ($arquivo.IsReadOnly) {
        $arquivo.IsReadOnly = $false
    }

    [System.IO.File]::WriteAllText($arquivo.FullName, $ajustado)
    Write-Host "Ajustado para WebView antigo: $($arquivo.FullName)"
}

if ($restouStatic) {
    exit 1
}
