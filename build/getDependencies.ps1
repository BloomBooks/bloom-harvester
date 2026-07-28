param (
    [Switch]$skipDownload,
    [Switch]$clean,

    [ValidateSet('development', 'production')]
    [string]$channel = 'development'
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

# Download/extract/copy dependencies from Bloom Desktop.
# The zip is published by BloomDesktop's harvester-artifacts GitHub Actions workflow: every
# successful build of a harvester branch replaces the asset on that branch's rolling release
# tag, so (like TeamCity's old latest.lastSuccessful URL) this always serves the latest green
# build. The zip's internal layout matches the old TeamCity artifact.
#
# There is one bundle per channel, and which one you want depends on what is being built:
#   development (default) -> Bloom's harvester-development branch. Local development, and
#                            CI on master, which deploys to the dev harvester server.
#   production            -> Bloom's harvester-production branch. CI on release, which
#                            deploys to the production harvester server.
# TeamCity expressed that same pairing as per-config artifact dependencies
# (Harvester-Master-Continuous  <- BloomDesktop-HarvesterDevelopmentBranch-Continuous,
#  Harvester-Release-Continuous <- BloomDesktop-HarvesterProductionBranch-Continuous);
# here the caller selects it with -channel instead.
$dependenciesDir = "$($downloadDir)\UnzippedDependencies"
$dependenciesUrl = "https://github.com/BloomBooks/BloomDesktop/releases/download/harvester-$($channel)-latest/bloom-harvester-deps.zip"
Write-Host "Getting Bloom $($channel) dependencies from $($dependenciesUrl)"
$command = "$($PSScriptRoot)\downloadAndExtractZip.ps1 -URL $($dependenciesUrl) -Filename bloom.zip -Output $($dependenciesDir) $(If ($skipDownload) { "-skipDownload"})"
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
    Copy-Item "$($dependenciesDir)\bin\Release\x64\*" -Destination "$($folder)\" -Force
    if (-not(Test-Path -Path "$($folder)\Bloom.exe" -PathType Leaf)) {
        Copy-Item "$($folder)\BloomAlpha.exe" -Destination "$($folder)\Bloom.exe" -Force
        Copy-Item "$($folder)\BloomAlpha.exe.config" -Destination "$($folder)\Bloom.exe.config" -Force
    }

    New-Item -ItemType Directory -Force -Path "$($folder)\gm" | Out-Null
    Copy-Item "$($dependenciesDir)\bin\Release\x64\gm\*" -Destination "$($folder)\gm\" -Recurse -Force

    New-Item -ItemType Directory -Force -Path "$($folder)\runtimes" | Out-Null
    Copy-Item "$($dependenciesDir)\bin\Release\x64\runtimes\*" -Destination "$($folder)\runtimes\" -Recurse -Force

    Copy-Item "$($dependenciesDir)\output\browser" -Destination "$($folder)\" -Recurse -Force

    New-Item -ItemType Directory -Force -Path "$($folder)\DistFiles" | Out-Null
    Copy-Item "$($dependenciesDir)\DistFiles\*" -Destination "$($folder)\DistFiles\" -Force -Exclude "localization","fonts"
    Copy-Item "$($dependenciesDir)\DistFiles\localization" -Destination "$($folder)\" -Recurse -Force
    Copy-Item "$($dependenciesDir)\DistFiles\fonts" -Destination "$($folder)\" -Recurse -Force
}



Write-Host
Write-Host "Done"
