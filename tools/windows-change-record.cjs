// Export old/new values against an explicit Git baseline for proof and handover.
// node tools/windows-change-record.cjs bn <baseline commit> [output.txt]
const fs=require('node:fs'),path=require('node:path'),cp=require('node:child_process');
const [code,base,out]=process.argv.slice(2),root=path.resolve(__dirname,'..');
if(!/^[a-z]{2}$/.test(code||'')||!base)throw Error('Usage: code baseline [output.txt]');
const read=s=>Object.fromEntries(s.replace(/^\uFEFF/,'').split(/\r?\n/).filter(x=>x&&!x.startsWith('#')&&x.includes('\t')).map(x=>{let i=x.indexOf('\t');return[x.slice(0,i),x.slice(i+1)]}));
const old=read(cp.execFileSync('git',['show',base+':knowledge/lang-'+code+'.txt'],{cwd:root,encoding:'utf8'}));
const now=read(fs.readFileSync(path.join(root,'knowledge/lang-'+code+'.txt'),'utf8'));
const changes=Object.entries(now).filter(([k,v])=>old[k]!==v),removed=Object.keys(old).filter(k=>!Object.hasOwn(now,k));
fs.writeFileSync(path.resolve(out||path.join(root,'windows-'+code+'-changes.txt')),'\uFEFF'+['AdeeboMeter Windows translation change record','Baseline: '+base,'Old entries: '+Object.keys(old).length+'; current: '+Object.keys(now).length+'; added or changed: '+changes.length+'; removed: '+removed.length,'English source\tPrevious translation (empty if new)\tCurrent translation',...changes.map(([k,v])=>[k,old[k]||'',v].join('\t')), ...removed.map(k=>'REMOVED\t'+k+'\t'+old[k])].join('\r\n'));
console.log({previous:Object.keys(old).length,current:Object.keys(now).length,addedOrChanged:changes.length,removed:removed.length});
