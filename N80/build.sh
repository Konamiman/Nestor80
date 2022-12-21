#!/bin/sh

# This script needs to be run from its own directory!

# Build the program on all the publish profiles if ran without arguments.
# Or build only the publish profile supplied as argument (e.g: build.sh SelfContained__win_x64)
# The built programs are placed in <N80 project root>/Release.

output() {
	echo "$(tput setaf "$1")$2$(tput sgr0)"
}

publish() {
	dotnet publish ../Nestor80.sln /p:PublishProfile=$1 /p:DebugType=None -c Release
}

banner() {
	echo
	output 6 "----- $1 -----"
	echo
}

set -e

mkdir -p Release

if [ -z "$(which dotnet > /dev/null && dotnet --list-sdks | grep ^6.0.)" ]; then
	output 1 "*** .NET SDK 6.0 is not installed! See https://docs.microsoft.com/dotnet/core/install/linux"
	exit 1
fi

if [ -z "$1" ]; then
		for PROFILE in $(find ./Properties/PublishProfiles -type f -name "*.pubxml" -exec basename {} .pubxml ';')
		do
			banner $PROFILE
			publish $PROFILE
		done
	else
		if [ -f "$1.pubxml" ]; then
			banner $1
			publish $1
		else
			output 1 "*** $1: profile not found!"
			exit 1
		fi
fi

rm -f ./Release/Portable/*.exe
find ./Release -name *.pdb -type f -delete

echo
output 3 "Build succeeded!"
