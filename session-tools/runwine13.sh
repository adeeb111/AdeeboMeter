#!/bin/bash
[ -z "$CLEANENV" ] && exec env -i CLEANENV=1 HOME=/root PATH=/usr/lib/wine:/usr/bin:/bin bash "$0" "$@"
export WINEPREFIX=/tmp/wpfx WINEDEBUG=-all PATH=/usr/lib/wine:$PATH
cd /tmp/amt13
Xvfb :83 -screen 0 1600x900x24 >/dev/null 2>&1 & XP=$!; export DISPLAY=:83; sleep 2
timeout 560 wine64 AdeeboMeter.exe --selftest >/dev/null 2>&1
wineserver -k 2>/dev/null; kill $XP 2>/dev/null
