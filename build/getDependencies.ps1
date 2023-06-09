param (
    [Switch]$skipDownload,
    [Switch]$clean
)

# Reference a custom commandlet that allows a synchronous delete
. "$PSScriptRoot\removeFileSystemItemSynchronous.ps1"

$downloadDir = "$PSScriptRoot\Download"
$libDir = "$PSScriptRoot\..\lib\dotnet\";
#$debugBuildDir = "$PSScriptRoot\..\src\Harvester\bin\Debug\net461";
#$releaseBuildDir = "$PSScriptRoot\..\src\Harvester\bin\Release\net461";

# Now, only need to copy to libDir... the build will take care of copying to the build dirs instead.
#$folders = $libDir, $debugBuildDir, $releaseBuildDir
$folders = $libDir

# Download/extract/copy dependencies from Bloom Desktop
$dependenciesDir = "$($downloadDir)\UnzippedDependencies"
$command = "$($PSScriptRoot)\downloadAndExtractZip.ps1 -URL https://build.palaso.org/guestAuth/repository/downloadAll/Bloom_BloomDesktopHarvesterBranchContinuous/latest.lastSuccessful -Filename bloom.zip -Output $($dependenciesDir) $(If ($skipDownload) { "-skipDownload"})"
Invoke-Expression $command


If ($clean) {
    ForEach ($folder in $folders) {
        Write-Host "Cleaning directory: $($folder)."
        Remove-FileSystemItem "$($folder)/*" -Recurse
    }
}

ForEach ($folder in $folders) {
    Write-Host "Copying to $($folder)"

    New-Item -ItemType Directory -Force -Path "$($folder)" | Out-Null
    Copy-Item "$($dependenciesDir)\bin\Release\*" -Destination "$($folder)\" -Force
    Copy-Item "$($folder)\BloomAlpha.exe" -Destination "$($folder)\Bloom.exe" -Force
	Copy-Item "$($folder)\BloomAlpha.exe.config" -Destination "$($folder)\Bloom.exe.config" -Force

    New-Item -ItemType Directory -Force -Path "$($folder)\gm" | Out-Null
    Copy-Item "$($dependenciesDir)\bin\Release\gm\*" -Destination "$($folder)\gm\" -Recurse -Force

    New-Item -ItemType Directory -Force -Path "$($folder)\runtimes" | Out-Null
    Copy-Item "$($dependenciesDir)\bin\Release\runtimes\*" -Destination "$($folder)\runtimes\" -Recurse -Force

    Copy-Item "$($dependenciesDir)\output\browser" -Destination "$($folder)\" -Recurse -Force

    New-Item -ItemType Directory -Force -Path "$($folder)\DistFiles" | Out-Null
    Copy-Item "$($dependenciesDir)\DistFiles\*" -Destination "$($folder)\DistFiles\" -Force -Exclude "localization","fonts"
    Copy-Item "$($dependenciesDir)\DistFiles\localization" -Destination "$($folder)\" -Recurse -Force
    Copy-Item "$($dependenciesDir)\DistFiles\fonts" -Destination "$($folder)\" -Recurse -Force
}



Write-Host
Write-Host "Done"
