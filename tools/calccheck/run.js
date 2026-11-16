global.window={};require(require('path').join(__dirname,'../../docs/calc.js'));const C=window.AMCALC;
C.rates.list={USD:1.4012,EUR:1.6233};C.rates.day=new Date(2026,10,13);
const fs=require('fs');const out=[];
for(const l of fs.readFileSync(process.argv[2],'utf8').split('\n')){if(!l)continue;const p=l.split('\t');const f=C.fn[p[0]];out.push('### '+l);
 try{for(const r of f.apply(null,p.slice(1).map(s=>s.replace(/\\n/g,'\n'))))out.push(r.slice(0,4).join(' | '))}catch(e){out.push('ERR '+e.message)}}
fs.writeFileSync(process.argv[3],out.join('\n')+'\n');
