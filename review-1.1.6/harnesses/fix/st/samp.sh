#!/bin/bash
# sample the mono process: time(ms since start) VmRSS VmData VmSize Threads
t0=$(date +%s%3N)
while true; do P=$(pgrep -f '^mono AdeeboMeter.exe --selftest' | head -1); [ -n "$P" ] && break; sleep 0.05; done
echo "pid $P start $(date +%s%3N)"
while kill -0 $P 2>/dev/null; do now=$(date +%s%3N); awk -v t=$((now-t0)) '/VmRSS|VmData|VmSize|Threads/{a[$1]=$2} END{print t, a["VmRSS:"], a["VmData:"], a["VmSize:"], a["Threads:"]}' /proc/$P/status 2>/dev/null; sleep 0.2; done
