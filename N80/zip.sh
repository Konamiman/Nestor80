#!/bin/sh

# This script needs to be run from its own directory!

# Generate ZIP files for all the variants inside the <project root>/Release/zips directory.
# You need to run build.sh first in order to actually have something to zip!

source functions.sh

set -e

if [ -z "$(which zip)" ]; then
	output 1 "*** zip is not installed!"
	exit 1
fi

version=$(grep 'AssemblyVersion' N80.csproj | grep -Eo '[0-9.]+')

cd Release
mkdir -p zips
rm -rf zips/*

for DIR in $(find * -type d ! -path "zips" ! -path "FrameworkDependant" ! -path "SelfContained"); do
	banner $DIR
	zip -r -j zips/N80_${version}_$(echo $DIR | tr '/' '_').zip $DIR
done

cd -

echo
output 3 "ZIPs generation succeeded!"
