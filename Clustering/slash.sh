#!/usr/bin/env bash
# Use mono to run the SLASH program.
# Run without arguments to get help.
MONO=/Library/Frameworks/Mono.framework/Versions/4.8.0/bin/mono 
EXECUTABLE=Clustering.exe
${MONO} ${EXECUTABLE} "$@"


# NOTE: If this script won't run, it may be because it was saved as UTF-8.
#       To remove the BOM (byte-order mark), use awk:
#   awk 'NR==1{sub(/^\xef\xbb\xbf/,"")}1' slash.sh > slash2.sh ; mv slash2.sh slash.sh; chmod 777 slash.sh
