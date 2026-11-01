<#
.SYNOPSIS
    Comprobación de calidad completa de LogiTracker (punto 9 de la Actividad 4).

.DESCRIPTION
    1. Compila en Release con las advertencias como errores (analizadores de .NET).
    2. Ejecuta las pruebas unitarias, de integración (PostgreSQL real en Docker) y de arquitectura.
    3. Revisa las dependencias con vulnerabilidades conocidas.
    Guarda la evidencia en docs/actividad-4/evidencia/ (UTF-8).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'LogiTracker.slnx'
$evidence = Join-Path $root 'docs/actividad-4/evidencia'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

# Ejecuta un comando, muestra su salida y la guarda como evidencia. Falla si el comando falla.
function Invoke-Step([string] $title, [string] $evidenceFile, [scriptblock] $command) {
    Write-Host "== $title" -ForegroundColor Cyan
    $output = & $command 2>&1 | ForEach-Object { "$_" }
    $exitCode = $LASTEXITCODE
    $output | Write-Host
    if ($evidenceFile) {
        $header = "# $title`r`n# Ejecutado el $(Get-Date -Format 'yyyy-MM-dd HH:mm')`r`n"
        Set-Content -Path (Join-Path $evidence $evidenceFile) -Value (@($header) + $output) -Encoding UTF8
    }
    if ($exitCode -ne 0) { throw "Falló el paso: $title (código $exitCode)" }
}

Invoke-Step 'Docker en ejecución (requerido por las pruebas de integración)' $null { docker info --format '{{.ServerVersion}}' }
Invoke-Step 'Restaurar herramientas locales' $null { dotnet tool restore }
Invoke-Step 'Compilación con analizadores (advertencias = errores)' 'compilacion.txt' {
    dotnet build $solution -c Release --nologo
}
Invoke-Step 'Pruebas unitarias, de integración y de arquitectura' 'ejecucion-pruebas.txt' {
    dotnet test $solution -c Release --no-build --nologo --logger 'console;verbosity=normal'
}
Invoke-Step 'Dependencias con vulnerabilidades conocidas' 'paquetes-vulnerables.txt' {
    dotnet list $solution package --vulnerable --include-transitive
}

Write-Host "`nTodas las comprobaciones pasaron. Evidencia en $evidence" -ForegroundColor Green
