#!/bin/bash
# server=build.palaso.org
# build_type=Bloom_HarvesterMasterContinuous
# root_dir=..
# Auto-generated by https://github.com/chrisvire/BuildUpdate.
# Do not edit this file by hand!

cd "$(dirname "$0")"

# *** Functions ***
force=0
clean=0

while getopts fc opt; do
case $opt in
f) force=1 ;;
c) clean=1 ;;
esac
done

shift $((OPTIND - 1))

copy_auto() {
if [ "$clean" == "1" ]
then
echo cleaning $2
rm -f ""$2""
else
where_curl=$(type -P curl)
where_wget=$(type -P wget)
if [ "$where_curl" != "" ]
then
copy_curl "$1" "$2"
elif [ "$where_wget" != "" ]
then
copy_wget "$1" "$2"
else
echo "Missing curl or wget"
exit 1
fi
fi
}

copy_curl() {
echo "curl: $2 <= $1"
if [ -e "$2" ] && [ "$force" != "1" ]
then
curl -# -L -z "$2" -o "$2" "$1"
else
curl -# -L -o "$2" "$1"
fi
}

copy_wget() {
echo "wget: $2 <= $1"
f1=$(basename $1)
f2=$(basename $2)
cd $(dirname $2)
wget -q -L -N "$1"
# wget has no true equivalent of curl's -o option.
# Different versions of wget handle (or not) % escaping differently.
# A URL query is the only reason why $f1 and $f2 should differ.
if [ "$f1" != "$f2" ]; then mv $f2\?* $f2; fi
cd -
}


# *** Results ***
# build: Harvester-Master-Continuous (Bloom_HarvesterMasterContinuous)
# project: Bloom
# URL: http://build.palaso.org/viewType.html?buildTypeId=Bloom_HarvesterMasterContinuous
# VCS: https://github.com/BloomBooks/bloom-harvester.git [refs/heads/master]
# dependencies:
# [0] build: YouTrackSharp (Bloom_YouTrackSharp)
#     project: Bloom
#     URL: http://build.palaso.org/viewType.html?buildTypeId=Bloom_YouTrackSharp
#     clean: false
#     revision: latest.lastSuccessful
#     paths: {"bin/YouTrackSharp.dll"=>"lib/dotnet", "bin/YouTrackSharp.pdb"=>"lib/dotnet"}
#     VCS: https://github.com/BloomBooks/YouTrackSharp.git [LinuxCompatible]

# make sure output directories exist
mkdir -p ../lib/dotnet

# download artifact dependencies
copy_auto http://build.palaso.org/guestAuth/repository/download/Bloom_YouTrackSharp/latest.lastSuccessful/bin/YouTrackSharp.dll ../lib/dotnet/YouTrackSharp.dll
copy_auto http://build.palaso.org/guestAuth/repository/download/Bloom_YouTrackSharp/latest.lastSuccessful/bin/YouTrackSharp.pdb ../lib/dotnet/YouTrackSharp.pdb
# End of script