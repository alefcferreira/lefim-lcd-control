$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
Push-Location $PSScriptRoot
try {
    $compilerArgs = @(
        '/nologo', '/codepage:65001', '/target:winexe', '/platform:x64', '/optimize+'
        '/out:LeFiM-LCD-Control.exe', '/r:System.Xaml.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
        "/r:$wpf\WindowsBase.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\PresentationFramework.dll"
        '/resource:src\Main.xaml,Main.xaml', '/resource:src\Logo.png,Logo.png', '/r:Microsoft.CSharp.dll', 'src\Tray.cs', 'src\Words.cs', 'src\SensorPanel.cs', 'src\SensorReader.cs', 'src\SensorOverlay.cs', 'src\Options.cs', 'src\App.cs', 'src\Media.cs', 'src\Usb.cs', 'src\LcdPort.cs'
    )
    if (Test-Path -LiteralPath iconeLcd.ico) { $compilerArgs += "/win32icon:iconeLcd.ico", "/resource:iconeLcd.ico,iconeLcd.ico" }
    & "$framework\csc.exe" @compilerArgs
    if ($LASTEXITCODE -ne 0) { throw 'Compilacao interrompida.' }
}
finally { Pop-Location }
