#!/bin/bash

# Build the programs on all the publish profiles for all runtimes,
# unless specific programs/profiles/runtimes are specified via environment variables
# (see how RUNTIMES, PROGRAMS and BUILD_TYPES are set).
#
# The built programs are placed in <project root>/Release/<build type>/<runtime>,
# where <project root> is N80, LK80 and LB80.

output() {
	echo "$(tput setaf "$1")$2$(tput sgr0)"
}

publish() {
	dotnet publish Nestor80.sln /p:PublishProfile=$1 /p:DebugType=None -c Release
}

banner() {
	echo
	output 6 "----- $1 -----"
	echo
}

set -e

RUNTIMES="${RUNTIMES:=linux-arm linux-arm64 linux-x64 osx-arm64 osx-x64 win-arm win-x64 win-x86}"
PROGRAMS="${PROGRAMS:=N80 LK80 LB80}"
BUILD_TYPES="${BUILD_TYPES:=portable FrameworkDependant SelfContained}"

if [ -z "$(which dotnet > /dev/null && dotnet --list-sdks | grep ^6.0.)" ]; then
	output 1 "*** .NET SDK 6.0 is not installed! See https://docs.microsoft.com/dotnet/core/install/linux"
	exit 1
fi

for PROGRAM in $PROGRAMS; do
    mkdir -p $PROGRAM/Release

    for BUILD_TYPE in $BUILD_TYPES; do
		if [ $BUILD_TYPE = "portable" ]; then
    		banner "$PROGRAM Portable"

    		dotnet publish $PROGRAM/$PROGRAM.csproj /p:PublishProtocol=FileSystem /p:DebugType=None -c Release /p:TargetFramework=net6.0 \
      		  -o $PROGRAM/Release/Portable

			rm -f $PROGRAM/Release/Portable/*.exe
		else
			for RUNTIME in $RUNTIMES; do
				if [ $BUILD_TYPE = "SelfContained" ]; then
					SELF_CONTAINED=true
				else
					SELF_CONTAINED=false
				fi

				RUNTIME_FILENAME=${RUNTIME//-/_}

				banner "$PROGRAM $BUILD_TYPE $RUNTIME"

				dotnet publish $PROGRAM/$PROGRAM.csproj /p:PublishProtocol=FileSystem /p:DebugType=None -c Release /p:TargetFramework=net6.0 \
				  /p:PublishSingleFile=true --self-contained $SELF_CONTAINED /p:RuntimeIdentifier=$RUNTIME \
				  -o $PROGRAM/Release/$BUILD_TYPE/$RUNTIME_FILENAME
			done
		fi
    done

    find $PROGRAM/Release -name *.pdb -type f -delete
done

echo
output 3 "Build succeeded!"
