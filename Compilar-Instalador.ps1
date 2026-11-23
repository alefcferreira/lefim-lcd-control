param([string]$OutputPath = (Join-Path $PSScriptRoot 'LeFiM-LCD-Control-Setup.exe'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $PSScriptRoot 'installer\Setup.cs'
$binary = Join-Path $PSScriptRoot 'LeFiM-LCD-Control.exe'
$icon = Join-Path $PSScriptRoot 'iconeLcd.ico'
$compilerArgs = @('/nologo','/codepage:65001','/platform:x64','/target:winexe','/optimize+',"/out:$OutputPath",'/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:Microsoft.CSharp.dll',"/win32icon:$icon","/resource:$binary,LeFiM.Lcd.Control.Payload",$source)
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
