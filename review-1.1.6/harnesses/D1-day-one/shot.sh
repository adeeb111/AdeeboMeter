#!/bin/bash
# usage: shot.sh <home> <exe-dir> <seconds> <outprefix> [args...]
H=$1; D=$2; S=$3; O=$4; shift 4
export HOME=$H XDG_DATA_HOME=$H/.local/share XDG_CONFIG_HOME=$H/.config
cd $D
mono --debug AdeeboMeter.exe "$@" > $O.out 2>&1 &
P=$!
for t in $(echo $S | tr ',' ' '); do sleep $t; import -window root $O-$t.png 2>/dev/null; done
kill $P 2>/dev/null; sleep 1; kill -9 $P 2>/dev/null
