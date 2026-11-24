#!/bin/bash
# usage: sc.sh <home> <exe-dir> <outprefix> <steps> [app args...]
# steps: space-separated: s:N (sleep) p:name (screenshot) c:x,y (click) m:x,y k:hex (key) r (wait for exit up to 60s)
H=$1; D=$2; O=$3; ST=$4; shift 4
export HOME=$H XDG_DATA_HOME=$H/.local/share XDG_CONFIG_HOME=$H/.config
export FONTCONFIG_FILE=/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review5/D1-day-one/fc/fonts.conf
unset http_proxy https_proxy HTTP_PROXY HTTPS_PROXY no_proxy NO_PROXY ALL_PROXY all_proxy
CL=/tmp/claude-0/-home-user-AdeeboMeter/c27bf091-9be3-567e-be11-8336f7c19f2a/scratchpad/review5/D1-day-one/click.exe
cd $D
mono --debug AdeeboMeter.exe "$@" >> $O.out 2>&1 &
P=$!
for s in $ST; do
 case $s in
  s:*) sleep ${s#s:};;
  p:*) import -window root $O-${s#p:}.png 2>/dev/null;;
  c:*) mono $CL ${s#c:};; m:*|k:*) mono $CL $s;;
  r) for i in $(seq 60); do kill -0 $P 2>/dev/null || break; sleep 1; done;;
 esac
done
pgrep -f "mono --debug AdeeboMeter.exe" >/dev/null && echo "still running" >> $O.out
pkill -f "mono --debug Adeebo"; sleep 1; pkill -9 -f "mono --debug Adeebo"
true
