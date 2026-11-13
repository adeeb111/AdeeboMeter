#!/bin/bash
# Microsoft's .NET security and quality analyzers (Microsoft.CodeAnalysis.NetAnalyzers) with every rule on, over src/.
# Needs: the .NET SDK (6 or newer - it carries the analyzers and the Roslyn compiler) and .NET Framework 4.8 reference
# assemblies (mono's 4.8-api folder, or set REFS to any folder that holds mscorlib.dll, System.dll ... System.Windows.Forms.dll).
# Prints every finding; exits 1 if there is any. Nothing is built or kept (the output goes to a temp folder).
set -euo pipefail
cd "$(dirname "$0")/.."
SDK=${SDK:-$(dotnet --list-sdks | tail -1 | sed -E 's/^([^ ]+) \[(.*)\]$/\2\/\1/')}
REFS=${REFS:-/usr/lib/mono/4.8-api}
AN=$(ls "$SDK"/Sdks/Microsoft.NET.Sdk/analyzers/Microsoft.CodeAnalysis*NetAnalyzers.dll | sed 's/^/-analyzer:/')
R=""; for a in mscorlib System System.Core System.Management System.Security System.Drawing System.Windows.Forms; do R="$R -r:$REFS/$a.dll"; done
T=$(mktemp -d); trap 'rm -rf "$T"' EXIT
dotnet exec "$SDK/Roslyn/bincore/csc.dll" -nologo -noconfig -nostdlib -langversion:5 -target:winexe -out:"$T/ca.exe" $AN \
  -analyzerconfig:tools/analyzers.globalconfig $R -recurse:'src/*.cs' > "$T/out.txt" 2>&1 || true
n=$(grep -c . "$T/out.txt" || true)
cat "$T/out.txt"; echo "analyzer findings: $n"
[ "$n" -eq 0 ]
