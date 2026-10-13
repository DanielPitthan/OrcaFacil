# O Android System WebView 83 (emulador sem atualização da Play Store) não entende
# blocos static {} nem atribuição ||= / ??=. O blazor.webview.js do .NET 10 usa os três,
# o script falha com "Unexpected token '{'" e a página permanece em "Carregando...".
# As substituições abaixo são equivalentes e cabem no WebView 83.

$ErrorActionPreference = "Stop"

$raizes = @(
    (Join-Path $env:USERPROFILE ".nuget\packages\microsoft.aspnetcore.components.webview"),
    (Join-Path $PSScriptRoot "..\obj")
)

$arquivos = foreach ($raiz in $raizes) {
    if (Test-Path $raiz) {
        Get-ChildItem -Path $raiz -Recurse -Filter "blazor.webview.js" -ErrorAction SilentlyContinue
    }
}

if (-not $arquivos) {
    Write-Host "blazor.webview.js não encontrado; nada a ajustar."
    exit 0
}

foreach ($arquivo in $arquivos) {
    $texto = [System.IO.File]::ReadAllText($arquivo.FullName)
    $ajustado = $texto.Replace('static{this.nextEventDelegatorId=0}', 'static nextEventDelegatorId=0;')
    $ajustado = $ajustado.Replace('static nextEventDelegatorId=0constructor', 'static nextEventDelegatorId=0;constructor')
    $ajustado = $ajustado.Replace('t||=[]', 't=t||[]')
    $ajustado = $ajustado.Replace('t[Ue]??={}', '(t[Ue]=t[Ue]??{})')

    if ($ajustado -eq $texto) {
        continue
    }

    if ($arquivo.IsReadOnly) {
        $arquivo.IsReadOnly = $false
    }

    [System.IO.File]::WriteAllText($arquivo.FullName, $ajustado)
    Write-Host "Ajustado para WebView antigo: $($arquivo.FullName)"
}
