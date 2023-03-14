#!/bin/sh

# Generate ZIP files for all the variants inside the <project root>/Release/zips directories,
# where <project root> is N80, LK80 and LB80.
# You need to run build.sh first in order to actually have something to zip!

output() {
	echo "$(tput setaf "$1")$2$(tput sgr0)"
}

banner() {
	echo
	output 6 "----- $1 -----"
	echo
}

set -e

if [ -z "$(which zip)" ]; then
	output 1 "*** zip is not installed!"
	exit 1
fi

for APPDIR in N80 LK80 LB80; do
	banner " *** Program: $APPDIR *** "
	cd $APPDIR
	version=$(grep 'AssemblyVersion' ${APPDIR}.csproj | grep -Eo '[0-9.]+')
	cd Release
	mkdir -p zips
	rm -rf zips/*

	for DIR in $(find * -type d ! -path "zips" ! -path "FrameworkDependant" ! -path "SelfContained"); do
		banner $DIR
		FILE=zips/${APPDIR}_${version}_$(echo $DIR | tr '/' '_').zip
		zip -r -j $FILE $DIR
		echo "  Created: $APPDIR/Release/$FILE"
	done
	cd ..
	cd ..
done

cd -

echo
output 3 "ZIPs generation succeeded!"
