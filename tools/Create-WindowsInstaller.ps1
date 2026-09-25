param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root "PronosticosAbasto\PronosticosAbasto.csproj"
$targetFramework = "net8.0-windows10.0.26100.0"
$publishDir = Join-Path $root "PronosticosAbasto\bin\$Configuration\$targetFramework\$Runtime\publish"
$outDir = Join-Path $root "dist\installer"
$filesWxs = Join-Path $outDir "AppFiles.wxs"
$productWxs = Join-Path $outDir "Product.wxs"
$msiPath = Join-Path $outDir "PronosticosAbastoSetup.msi"

Write-Host "Publishing safe build..."
dotnet publish $projectPath -c $Configuration -r $Runtime /p:PublishProfile=$Runtime /p:PublishTrimmed=false

if (-not (Test-Path (Join-Path $publishDir "PronosticosAbasto.exe"))) {
    throw "Publish output was not found at $publishDir"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function ConvertTo-WixXmlAttribute([string]$value) {
    $value.
        Replace("&", "&amp;").
        Replace('"', "&quot;").
        Replace("<", "&lt;").
        Replace(">", "&gt;")
}

$allowedSatelliteFolders = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
@("en-us", "es-ES", "es-MX") | ForEach-Object { [void]$allowedSatelliteFolders.Add($_) }

function Test-CultureFolderName([string]$name) {
    $name -match '^[a-z]{2,3}(-[A-Za-z]{2,8}){0,2}$'
}

function Test-ExcludedPublishPath([string]$path) {
    $rootPath = [System.IO.Path]::GetFullPath($publishDir).TrimEnd([char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if ($fullPath.Length -le $rootPath.Length) {
        return $false
    }

    $relativePath = $fullPath.Substring($rootPath.Length).TrimStart([char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
    $firstSegment = $relativePath.Split([char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar), [System.StringSplitOptions]::RemoveEmptyEntries)[0]

    (Test-CultureFolderName $firstSegment) -and -not $allowedSatelliteFolders.Contains($firstSegment)
}

$directories = Get-ChildItem -LiteralPath $publishDir -Directory -Recurse |
    Where-Object { -not (Test-ExcludedPublishPath $_.FullName) } |
    Sort-Object FullName
$directoryIds = @{}
$directoryIds[$publishDir] = "INSTALLFOLDER"

$directoryIndex = 1
foreach ($directory in $directories) {
    $directoryIds[$directory.FullName] = "Dir{0:D4}" -f $directoryIndex
    $directoryIndex++
}

function Get-DirectoryXml([string]$parentPath, [int]$indentLevel) {
    $indent = " " * $indentLevel
    $children = Get-ChildItem -LiteralPath $parentPath -Directory |
        Where-Object { -not (Test-ExcludedPublishPath $_.FullName) } |
        Sort-Object Name
    $lines = New-Object System.Collections.Generic.List[string]

    foreach ($child in $children) {
        $childId = $directoryIds[$child.FullName]
        $childName = ConvertTo-WixXmlAttribute $child.Name
        $childXml = Get-DirectoryXml $child.FullName ($indentLevel + 2)

        if ($childXml.Count -gt 0) {
            $lines.Add("$indent<Directory Id=""$childId"" Name=""$childName"">")
            foreach ($line in $childXml) {
                $lines.Add($line)
            }
            $lines.Add("$indent</Directory>")
        }
        else {
            $lines.Add("$indent<Directory Id=""$childId"" Name=""$childName"" />")
        }
    }

    $lines
}

$directoryXmlLines = Get-DirectoryXml $publishDir 6
$directoryFragment = ""
if ($directoryXmlLines.Count -gt 0) {
    $directoryFragment = @"

  <Fragment>
    <DirectoryRef Id="INSTALLFOLDER">
$($directoryXmlLines -join "`r`n")
    </DirectoryRef>
  </Fragment>
"@
}

$files = Get-ChildItem -LiteralPath $publishDir -File -Recurse |
    Where-Object { -not (Test-ExcludedPublishPath $_.FullName) } |
    Sort-Object FullName
$componentRefs = New-Object System.Collections.Generic.List[string]
$fileComponents = New-Object System.Collections.Generic.List[string]

$index = 1
foreach ($file in $files) {
    $componentId = "Cmp{0:D4}" -f $index
    $fileId = "File{0:D4}" -f $index
    $source = ConvertTo-WixXmlAttribute $file.FullName
    $directoryId = $directoryIds[$file.DirectoryName]

    $componentRefs.Add("      <ComponentRef Id=""$componentId"" />")
    $fileComponents.Add("    <Component Id=""$componentId"" Guid=""*"" Directory=""$directoryId"">")
    $fileComponents.Add("      <File Id=""$fileId"" Source=""$source"" KeyPath=""yes"" />")
    $fileComponents.Add("    </Component>")
    $index++
}

$filesXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
$($fileComponents -join "`r`n")
  </Fragment>
$directoryFragment

  <Fragment>
    <ComponentGroup Id="AppFiles">
$($componentRefs -join "`r`n")
    </ComponentGroup>
  </Fragment>
</Wix>
"@

Set-Content -LiteralPath $filesWxs -Value $filesXml -Encoding UTF8

$iconPath = Join-Path $publishDir "AppIcon.ico"
if (-not (Test-Path $iconPath)) {
    $iconPath = Join-Path $root "PronosticosAbasto\Assets\AppIcon.ico"
}

$iconPathEscaped = $iconPath.
    Replace("&", "&amp;").
    Replace('"', "&quot;").
    Replace("<", "&lt;").
    Replace(">", "&gt;")

$productXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package
    Name="Pronostico de Abasto"
    Manufacturer="Pronostico de Abasto"
    Version="1.0.0.0"
    UpgradeCode="{4A3435E0-F172-4D17-96BC-5512D4519035}"
    Scope="perMachine">

    <MajorUpgrade DowngradeErrorMessage="Ya hay una version mas nueva instalada." />
    <MediaTemplate EmbedCab="yes" />
    <Icon Id="AppIcon.ico" SourceFile="$iconPathEscaped" />
    <Property Id="ARPPRODUCTICON" Value="AppIcon.ico" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="Pronostico de Abasto" />
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="Pronostico de Abasto" />
    </StandardDirectory>

    <StandardDirectory Id="DesktopFolder" />

    <Component Id="ApplicationShortcuts" Directory="INSTALLFOLDER" Guid="*">
      <Shortcut
        Id="StartMenuShortcut"
        Directory="ApplicationProgramsFolder"
        Name="Pronostico de Abasto"
        Description="Pronostico de Abasto"
        Target="[INSTALLFOLDER]PronosticosAbasto.exe"
        WorkingDirectory="INSTALLFOLDER"
        Icon="AppIcon.ico" />
      <Shortcut
        Id="DesktopShortcut"
        Directory="DesktopFolder"
        Name="Pronostico de Abasto"
        Description="Pronostico de Abasto"
        Target="[INSTALLFOLDER]PronosticosAbasto.exe"
        WorkingDirectory="INSTALLFOLDER"
        Icon="AppIcon.ico" />
      <RemoveFolder Id="RemoveApplicationProgramsFolder" Directory="ApplicationProgramsFolder" On="uninstall" />
      <RegistryValue Root="HKCU" Key="Software\PronosticoDeAbasto" Name="Installed" Type="integer" Value="1" KeyPath="yes" />
    </Component>

    <Feature Id="MainFeature" Title="Pronostico de Abasto" Level="1">
      <ComponentGroupRef Id="AppFiles" />
      <ComponentRef Id="ApplicationShortcuts" />
    </Feature>
  </Package>
</Wix>
"@

Set-Content -LiteralPath $productWxs -Value $productXml -Encoding UTF8

Write-Host "Building MSI..."
if (Test-Path $msiPath) {
    [System.IO.File]::Delete($msiPath)
}

wix build $productWxs $filesWxs -arch x64 -out $msiPath
if ($LASTEXITCODE -ne 0) {
    throw "WiX failed to build the MSI."
}

Write-Host "Installer created:"
Get-Item -LiteralPath $msiPath | Select-Object FullName, Length
