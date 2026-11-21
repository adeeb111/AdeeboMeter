// Reusable exact-key coverage check. No translation service or runtime dependency.
// node tools/localization-audit.cjs ar
const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const root=path.resolve(__dirname,'..'),code=process.argv[2]||'ar';
if(!/^[a-z]{2}$/.test(code))throw Error('Expected a two-letter language code');
const ctx={window:{}};vm.runInNewContext(fs.readFileSync(path.join(root,'docs/locale-messages.js'),'utf8'),ctx);
const web=ctx.window.AdeeboMeterMessages;
function read(code){return Object.fromEntries(fs.readFileSync(path.join(root,'knowledge/lang-'+code+'.txt'),'utf8').replace(/^\uFEFF/,'').split(/\r?\n/).filter(s=>s&&!s.startsWith('#')&&s.includes('\t')).map(s=>{const i=s.indexOf('\t');return[s.slice(0,i),s.slice(i+1)]}));}
const windows=read(code),reference=read('fr'),issues=[];
const windowsOnly=process.argv.includes('--windows-only');
const marks=s=>(s.match(/\{\w+\}/g)||[]).sort().join('|');
for(const [surface,table]of (windowsOnly?[['windows',windows]]:[['web',web[code]],['windows',windows]]))for(const [key,value]of Object.entries(table)){
 if(!value.trim())issues.push({surface,key,reason:'empty'});
 if(marks(key)!==marks(value))issues.push({surface,key,reason:'placeholder'});
 for(const n of key.match(/\d+/g)||[])if(!value.includes(n))issues.push({surface,key,reason:'number '+n});
 if(/^\s*/.exec(key)[0]!==/^\s*/.exec(value)[0]||/\s*$/.exec(key)[0]!==/\s*$/.exec(value)[0])issues.push({surface,key,reason:'edge whitespace'});
 if(/ZXQ\d+QXZ/.test(value))issues.push({surface,key,reason:'draft marker'});
}
if(windows['not in the table at all'])issues.push({surface:'windows',reason:'self-test sentinel must remain untranslated'});
const sourceFile=path.join(__dirname,'localization-source.json');
const source=fs.existsSync(sourceFile)?JSON.parse(fs.readFileSync(sourceFile,'utf8')):{webKeys:Object.keys(web.fr),windowsKeys:Object.keys(reference)};
const result={code,web:Object.keys(web[code]).length,windows:Object.keys(windows).length,missingWeb:source.webKeys.filter(k=>!Object.hasOwn(web[code],k)),missingWindows:source.windowsKeys.filter(k=>!Object.hasOwn(windows,k)),issues};
fs.writeFileSync(path.join(root,'localization-audit-'+code+'.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify({...result,missingWeb:result.missingWeb.length,missingWindows:result.missingWindows.length,issues:issues.length}));
if((!windowsOnly&&result.missingWeb.length)||result.missingWindows.length||issues.length)process.exitCode=1;
