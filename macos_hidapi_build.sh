#!/bin/bash

echo "\n\n>>>>>"
echo ">>> BUILDING FOR MACOS"
echo ">>>>>\n"

# Compile hidapi with default command
./bootstrap
./configure --prefix=$1/build/ # install in project_dir/build
make
make install

echo "\n>>>>>"
echo ">>> BUILD MACOS .BUNDLE STRUCTURE"
echo ">>>>>\n"

# Build osx bundle file.  Unity does not support .dylibs but
# it does support .bundles (essentially repackaged dylibs).
# https://developer.apple.com/library/archive/documentation/CoreFoundation/Conceptual/CFBundles/BundleTypes/BundleTypes.html
# (see: "Lodable Bundles")

BDIR=$1/build
FDIR=$1/build/hidapi.bundle

# delete whatever we have made in the past
if [ -d $FDIR ]; then
	rm -r $FDIR
fi

# make bundle directory in project_dir/build
cd $1/build
mkdir hidapi.bundle
cd hidapi.bundle
mkdir Contents Contents/MacOS

# move compiled dylib into bundle
mv $BDIR/lib/libhidapi.0.dylib Contents/MacOS/hidapi
chmod a+rwx Contents/MacOS/hidapi

# build info.plist
# https://github.com/kevinSuttle/macOS-Defaults/blob/master/REFERENCE.md
# https://developer.apple.com/library/archive/documentation/General/Reference/InfoPlistKeyReference/Articles/CoreFoundationKeys.html
alias plistbuddy=/usr/libexec/PlistBuddy
PLFILE=$FDIR/Contents/info.plist

plistbuddy -c "Add :CFBundleDisplayName string \"Signal11 HIDAPI\"" $PLFILE
plistbuddy -c "Add :CFBundleDevelopmentRegion string \"en-US\"" $PLFILE
plistbuddy -c "Add :CFBundleExecutable string \"hidapi\"" $PLFILE
plistbuddy -c "Add :CFBundleIdentifier string \"com.signal11.hidapi\"" $PLFILE
plistbuddy -c "Add :CFBundlePackageType string \"BNDL\"" $PLFILE
plistbuddy -c "Add :CFBundleVersion string \"1.0.0\"" $PLFILE

echo "\n>>>>>"
echo ">>> BUNDLE CREATED AT $FDIR"
echo ">>>>>\n"