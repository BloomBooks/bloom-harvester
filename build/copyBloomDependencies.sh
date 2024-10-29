#!/bin/sh

set -e

cd "$(dirname "$0")"

buildMode="Debug"
if [ -n "$1" ]; then
    bloomDir="$1";
    if [ -n "$2" ]; then buildMode="$2"; fi
else
    echo "Usage: copyBloomDependencies.sh BloomDesktopRootDir [BuildMode]"
    echo "    BloomDesktopRootDir looks something like \"c:/src/BloomDesktop\" or \"/d/github/BloomMaster\"."
    echo "    BuildMode is either \"Debug\" or \"Release\".  It defaults to Debug."
    exit 1
fi

echo removing all files and folders from lib/dotnet
rm -rf ../lib/dotnet/*
echo copying files from \"$bloomDir/output/$buildMode\" to lib/dotnet
cp -r "$bloomDir/output/$buildMode/"* ../lib/dotnet
echo copying files from \"$bloomDir/output/browser\" to lib/dotnet
cp -r "$bloomDir/output/browser" ../lib/dotnet
echo copying files from \"$bloomDir/DistFiles\" to lib/dotnet
cp -r "$bloomDir/DistFiles/fonts" ../lib/dotnet
cp -r "$bloomDir/DistFiles/ghostscript" ../lib/dotnet
cp -r "$bloomDir/DistFiles/localization" ../lib/dotnet
cp -r "$bloomDir/DistFiles/icons" ../lib/dotnet
cp -r "$bloomDir/DistFiles/ColorProfiles" ../lib/dotnet
mkdir ../lib/dotnet/DistFiles
cp -r "$bloomDir/DistFiles/"*.* ../lib/dotnet/DistFiles
