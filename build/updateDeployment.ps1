param (
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'prod')]
	[string]$environment,

    [Switch]$skipDownload
)

$harvestRootDir = "C:\HarvesterDev";
$buildQueueName = "Bloom_HarvesterMasterContinuous";
If ($environment -eq "prod") {
	$harvestRootDir = "C:\Harvester";
    $buildQueueName = "Bloom_HarvesterReleaseContinuous";
}

# Currently just forget about RestartOnCrash (because it doesn't work properly when running with multiple users)
# And we are running the two harvesters under different user accounts so that they can process books simultaneously
# Instead, we'll rely on the fallback that just starts the Harvester up directly
# I haven't actually observed RestartOnCrash  helping in a case where the Harvester terminated, so i think it's ok...
# In a really bad case scneario where the Harvester somehow terminated, it will get restarted when this script is next run
$restartOnCrashPath = "C:\blahblahMadeUpPathThatDoesNotExist"
#$restartOnCrashPath = "C:\Harvester\RestartOnCrash\RestartOnCrash.exe"


$outputDir= "$($harvestRootDir)\ReleaseNext"
$currentVersionDir ="$($harvestRootDir)\Release"
$backupOfPreviousDir= "$($harvestRootDir)\ReleasePrev"

$VerbosePreference = "Continue";

# Reference a custom commandlet that allows a synchronous delete
. "$PSScriptRoot\removeFileSystemItemSynchronous.ps1"

$downloadDir = "$PSScriptRoot\Download"

# ENHANCE: Check if we already have the latest version

# Download the latest build (if requested)
$unzipDestination = "$($downloadDir)\Unzipped"
$command = "$($PSScriptRoot)\downloadAndExtractZip.ps1 -URL https://build.palaso.org/guestAuth/repository/downloadAll/$($buildQueueName)/latest.lastSuccessful -Filename harvester.zip -Output $($downloadDir)\Unzipped $(If ($skipDownload) { "-skipDownload"})"
Invoke-Expression $command

# Copy from the unzip destination
# Precondition: This copy operation expects the destination folder not to exist. (So that it can rename Release -> ReleaseNext, instead of mkaing ReleaseNext\Release
If (Test-Path $outputDir) {
    # Clean the output directory
    Remove-FileSystemItem "$($outputDir)" -Recurse
}

Copy-Item "$($unzipDestination)\Release\" -Destination "$($outputDir)\" -Recurse
Write-Host "Setup of Next version done."
Write-Host

# Close current Harvester and RestartOnCrash
#
# First check if the Harvester's Bloom subprocess is running to attempt to be polite and not kill it if Harvester is actively working on something.
# (This would get a book stuck in the InProgress state, which is moderately annoying, but if a book is stuck in InProgress for too long,
# the Harvester will eventually decide it's stuck and re-process it.)
# If we exceed the max number of tries though, we'll just forcibly close Bloom so we can continue updating.
$bloomExePath = "$($currentVersionDir)\Bloom.exe"
$maxTries = 5;
for ($tryNumber = 0; $tryNumber -lt $maxTries; $tryNumber++) {
    # This is just an approximation... if Bloom is running, then that definitely shows Harvester is working on a book.
    # But if Bloom isn't running, we're not 100% sure. Harvester might still be working on a book, e.g. doing downloading or uploading
    # We'll just have to take our best guess though.
    if (0 -eq ((Get-Process -Name "Bloom" -ErrorAction Ignore | Where-Object { $_.Path -eq $bloomExePath }).count )) {
        # Running Bloom process count is equal to 0.
        # Let's go ahead and continue
        break
    } else {
        # Hmm. Bloom is running. harvester seems to be working on something.
        # Maybe let's wait for a bit and see if it goes away.
        Write-Host "Did not update Harvester yet because Bloom subprocess is running. Will retry again 60 seconds.";
        sleep 60;  # seconds
    }
}


Write-Host "Killing any existing Harvester-related processes."
$harvesterExePath = "$($currentVersionDir)\BloomHarvester.exe"
Get-Process -Name "RestartOnCrash" -ErrorAction Ignore | Where-Object { $_.Path -eq $restartOnCrashPath } | Stop-Process -Verbose
Get-Process -Name "BloomHarvester" -ErrorAction Ignore | Where-Object { $_.Path -eq $harvesterExePath } | Stop-Process -Verbose
# This is needed to shutdown the Bloom subprocess in case the Harvester is in the middle of calling Bloom to process a book
Get-Process -Name "Bloom" -ErrorAction Ignore | Where-Object { $_.Path -eq $bloomExePath } | Stop-Process -Verbose

Write-Host
Write-Host "Swapping to next version"
Remove-FileSystemItem "$($backupOfPreviousDir)" -Recurse
Write-Host "Moving current to backup location $($backupOfPreviousDir)";
Copy-Item "$($currentVersionDir)" -Destination "$($backupOfPreviousDir)" -Recurse
Remove-FileSystemItem "$($currentVersionDir)" -Recurse

# Wait a while to make sure items are fully removed.
Write-Host "Copying new build to current location $($currentVersionDir)";
New-Item -ItemType Directory -Force -Path $currentVersionDir | Out-Null
Copy-Item "$($outputDir)\*" -Destination $currentVersionDir -Recurse -Force


# Check for environment variables
# Enhance: I suppose you could allow an optional parameter which would specify whether it wants dev or prod. And only check the relevant keys.
$anyEnvVarsMissing = $false
If ($environment -eq "prod") {
    $envVarKeys = "BloomBooksS3KeyProd", "BloomBooksS3SecretKeyProd", "BloomHarvesterAzureAppInsightsKeyProd", "BloomHarvesterParseAppIdProd", "BloomHarvesterS3KeyProd", "BloomHarvesterS3SecretKeyProd", "BloomHarvesterUserName", "BloomHarvesterUserPasswordProd"	
} Else {
    $envVarKeys = "BloomBooksS3KeyDev", "BloomBooksS3SecretKeyDev", "BloomHarvesterAzureAppInsightsKeyDev", "BloomHarvesterParseAppIdDev", "BloomHarvesterS3KeyDev", "BloomHarvesterS3SecretKeyDev",  "BloomHarvesterUserName", "BloomHarvesterUserPasswordDev"
}

ForEach ($key in $envVarKeys) {
    if (-Not (Test-Path "env:$($key)")) {
        Write-Error "Missing environment variable: $key"
        $anyEnvVarsMissing=$true
    }
}

If ($anyEnvVarsMissing) {
    Write-Host "Aborting starting up new Harvester process because some environment variables are missing. Please set the environment variables, (possibly restart the console window/etc), and try again."
    return
}

# Startup RestartOnCrash, or just startup the Harvester directly as a fallback
#If ((Test-Path -Path $restartOnCrashPath -PathType Leaf)) {
#    Write-Host "Running RestartOnCrash from: $($restartOnCrashPath)"
#    Start-Process -FilePath $restartOnCrashPath
#} Else {
#    Write-Host "RestartOnCrash not found at $($restartOnCrashPath)"
#    Write-Host "Starting Harvester"
#    Start-Process -FilePath $harvesterExePath -ArgumentList  @("harvest", "--mode=default", "--environment=$($environment)", "--parseDBEnvironment=$($environment)", "--count=1", "--loop", "--loopWaitSeconds=60")
#}

# Don't try to startup the Harvester automatically anymore. Right now, the first time you run it,
# it fails if it's run as a background process (seems to be due to L10nSharp failing).
# But if you run it manually via a CLI first, and L10nSharp does its updates successfully, in subsequent runs
# you can run it either from CLI or as a background process
Write-Host "Please start the Harvester manually via a CLI and ensure it successfully processes a book"


Write-Host "Done"