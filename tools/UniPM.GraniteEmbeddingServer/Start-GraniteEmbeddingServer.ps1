param([Parameter(Mandatory)] [string] $PythonPath, [Parameter(Mandatory)] [string] $ModelPath, [int] $Port = 8091, [switch] $Offline)
if (!(Test-Path -LiteralPath $PythonPath) -or !(Test-Path -LiteralPath $ModelPath)) { throw 'Provide existing Python and model paths through parameters; do not commit local paths.' }
$arguments = @("$PSScriptRoot/server.py", '--model-path', $ModelPath, '--port', $Port)
if ($Offline) { $arguments += '--offline' }
& $PythonPath @arguments
