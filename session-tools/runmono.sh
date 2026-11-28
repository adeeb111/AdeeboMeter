#!/bin/bash
# Self-test under Mono (fast, ~5 min; no Wine). Usage: session-tools/runmono.sh /tmp/am48.exe
# Mono can't do everything Windows can: about 25 checks always fail here (session-tools/mono-baseline.txt).
# Only lines under "new:" matter - those are real failures.
EXE=${1:-/tmp/am48.exe}; R=$(cd "$(dirname "$0")/.." && pwd); D=/tmp/stx
rm -rf $D; mkdir -p $D && cp "$EXE" $D/AdeeboMeter.exe && cp -r "$R/knowledge" $D/ && cd $D || exit 1
(Xvfb :98 -screen 0 1600x900x24 >/dev/null 2>&1 &) ; sleep 2
DISPLAY=:98 timeout 520 mono AdeeboMeter.exe --selftest >/dev/null 2>&1; pkill Xvfb
grep -E "FAILED$" $D/AdeeboMeter-selftest.txt
grep "^FAIL" $D/AdeeboMeter-selftest.txt | cut -c1-60 | sort > /tmp/st-now.txt
echo new:; comm -13 "$R/session-tools/mono-baseline.txt" /tmp/st-now.txt
