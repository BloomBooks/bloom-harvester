param (
    [Switch]$skipDownload,
    [Switch]$clean
)

$downloadDir = "$PSScriptRoot\Download"
$libDir = "$PSScriptRoot\..\lib\dotnet\";
$debugBuildDir = "$PSScriptRoot\..\src\Harvester\bin\Debug\net461";
$releaseBuildDir = "$PSScriptRoot\..\src\Harvester\bin\Release\net461";
$folders = $libDir, $debugBuildDir, $releaseBuildDir

# Download/extract/copy dependencies from Bloom Desktop
$dependenciesDir = "$($downloadDir)\UnzippedDependencies"
$command = "$($PSScriptRoot)\downloadAndExtractZip.ps1 -URL https://build.palaso.org/guestAuth/repository/downloadAll/bt222/latest.lastSuccessful -Filename bloom.zip -Output $($dependenciesDir) $(If ($skipDownload) { "-skipDownload"})"
Invoke-Expression $command


If ($clean) {
    ForEach ($folder in $folders) {
        Write-Host "Cleaning directory: $($folder)."
        Remove-Item -Path "$($folder)/*" -Recurse -ErrorAction Ignore
    }
    Start-Sleep -Seconds 2
}

ForEach ($folder in $folders) {
    Write-Host "Copying to $($folder)"

    Copy-Item "$($dependenciesDir)\bin\Release\*" -Destination "$($folder)\" -Force
    Copy-Item "$($folder)\BloomAlpha.exe" -Destination "$($folder)\Bloom.exe" -Force

    New-Item -ItemType Directory -Force -Path "$($folder)\Firefox" | Out-Null
    Copy-Item "$($dependenciesDir)\bin\Release\Firefox\*" -Destination "$($folder)\Firefox\" -Force

    Copy-Item "$($dependenciesDir)\output\browser" -Destination "$($folder)\" -Recurse -Force

    New-Item -ItemType Directory -Force -Path "$($folder)\DistFiles" | Out-Null
    Copy-Item "$($dependenciesDir)\DistFiles\*" -Destination "$($folder)\DistFiles\" -Force -Exclude "localization"
    Copy-Item "$($dependenciesDir)\DistFiles\localization" -Destination "$($folder)\" -Recurse -Force
}



Write-Host
Write-Host "Done"
