#!/bin/bash
#
# Usage: ./copyBloomDependncies.sh [bloomDesktopRootDir]

bloomDir="$1"

buildModes=("Debug" "Release")

for mode in "${buildModes[@]}"
do
	:
	harvestBuildDir="../src/Harvester/bin/$mode/net461/"
	mkdir -p "$harvestBuildDir/browser"
	cp -r "$bloomDir/output/browser/bookEdit" "$harvestBuildDir/browser/bookEdit"
	cp -r "$bloomDir/output/browser/bookLayout" "$harvestBuildDir/browser/bookLayout"
	cp -r "$bloomDir/output/browser/bookPreview" "$harvestBuildDir/browser/bookPreview"
	cp -r "$bloomDir/output/browser/branding" "$harvestBuildDir/browser/branding"
	cp -r "$bloomDir/output/browser/collection" "$harvestBuildDir/browser/collection"
	cp -r "$bloomDir/output/browser/lib" "$harvestBuildDir/browser/lib"
	cp -r "$bloomDir/output/browser/publish" "$harvestBuildDir/browser/publish"
	cp -r "$bloomDir/output/browser/templates" "$harvestBuildDir/browser/templates"
	cp -r "$bloomDir/output/browser/themes" "$harvestBuildDir/browser/themes"

	cp -r "$bloomDir/DistFiles/localization" "$harvestBuildDir/localization"
	cp "$bloomDir/DistFiles/BloomBlankPage.htm" "$harvestBuildDir"
	cp "$bloomDir/DistFiles/connections.dll" "$harvestBuildDir"
done
