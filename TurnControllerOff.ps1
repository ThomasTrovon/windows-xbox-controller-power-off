$ErrorActionPreference = 'Stop'

# O PowerShell permanece apenas como lançador para preservar o ponto de entrada
# do projeto original. A regra GIP vive somente no executável moderno em C#.
$candidates = @(
    (Join-Path $PSScriptRoot 'XBoxControllerOff.exe'),
    (Join-Path $PSScriptRoot 'XBoxControllerOff\bin\Release\net10.0-windows\win-x64\publish\XBoxControllerOff.exe')
)

$executable = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $executable) {
    throw 'XBoxControllerOff.exe não foi encontrado. Compile o projeto com dotnet publish antes de executar este lançador.'
}

$process = Start-Process -FilePath $executable -Wait -PassThru
exit $process.ExitCode
