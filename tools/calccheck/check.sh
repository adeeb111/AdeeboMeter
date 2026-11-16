#!/bin/bash
# The phone's calculators (docs/calc.js) and QR codes (docs/qr.js) against the PC's own code (src/Checks/Calc*.cs, src/Qr.cs):
# every vector in vec.tsv must give the same rows, and every QR the same modules. Needs mono and node.
set -e; cd "$(dirname "$0")"; T=$(mktemp -d)
mcs -langversion:5 -out:$T/calc.exe ../../src/Checks/Calc.cs ../../src/Checks/Calc2.cs ../../src/Checks/Calc3.cs H.cs Stub.cs
LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 mono $T/calc.exe vec.tsv | sed '1s/^\xEF\xBB\xBF//' > $T/cs.out
node run.js vec.tsv $T/js.out
diff $T/cs.out $T/js.out && echo "calculators: same as the PC ($(wc -l < vec.tsv) inputs)"
mcs -out:$T/q.exe -r:System.Drawing ../../src/Qr.cs Q.cs
printf 'WIFI:T:WPA;S:Home;P:pass word;;\nhttps://adeebometer.com/app#x\n%0900d\n' 0 > $T/q.txt
mono $T/q.exe $T/q.txt > $T/qcs.txt
node -e "require('../../docs/qr.js');require('fs').readFileSync('$T/q.txt','utf8').split('\n').filter(Boolean).forEach(s=>console.log(AMQR.encode(s).map(r=>r.map(b=>b?1:0).join('')).join('/')+'/'))" > $T/qjs.txt
diff $T/qcs.txt $T/qjs.txt && echo "QR codes: same as the PC"
