#!/bin/bash
# build against the real .NET Framework 4.8 reference assemblies with Roslyn (what Windows' compiler sees)
set -e
cd /home/claude/adeebometer
O=/tmp/amobj48; rm -rf $O; mkdir -p $O
R=""
for f in knowledge/*.txt; do n=$(basename $f .txt); python3 -c "import gzip;open('$O/$n.gz','wb').write(gzip.compress(open('$f','rb').read(),9))"; R="$R -resource:$O/$n.gz,AdeeboMeter.knowledge.$n.gz"; done
for f in assets/fonts/*.ttf; do n=$(basename $f .ttf); python3 -c "import gzip;open('$O/$n.ttf.gz','wb').write(gzip.compress(open('$f','rb').read(),9))"; R="$R -resource:$O/$n.ttf.gz,AdeeboMeter.font.$n.gz"; done
for f in assets/emoji/*.png; do n=$(basename $f .png); R="$R -resource:$f,AdeeboMeter.emoji.$n.png"; done
HK=""
if [ -s secrets/hof.key ]; then python3 -c "
k=open('secrets/hof.key','rb').read().strip()
b=','.join(str(c^((i*73+41)&255)) for i,c in enumerate(k))
open('$O/HofKey.cs','w').write('namespace AdeeboMeter { static partial class HallOfFame { static partial void BuildKey(ref string key) { key = Unmask(new byte[] { '+b+' }); } } }\\n')"; HK="$O/HofKey.cs"; fi
D=/usr/lib/dotnet/sdk/8.0.133; M=/usr/lib/mono/4.8-api
timeout 500 dotnet exec $D/Roslyn/bincore/csc.dll -nologo -noconfig -nostdlib -langversion:5 -optimize+ -target:winexe -win32icon:assets/AdeeboMeter.ico $R -out:${1:-/tmp/am48.exe} -r:$M/mscorlib.dll -r:$M/System.dll -r:$M/System.Core.dll -r:$M/System.Management.dll -r:$M/System.Security.dll -r:$M/System.Numerics.dll -r:$M/System.Drawing.dll -r:$M/System.Windows.Forms.dll -recurse:'src/*.cs' $HK 2>&1 | grep -E "error|warning CS" | head -30
ls -la ${1:-/tmp/am48.exe}
