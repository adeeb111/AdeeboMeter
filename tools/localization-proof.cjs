// Export canonical catalogs as a bilingual UTF-8 TXT for a human proofreader.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const code=process.argv[2],root=path.resolve(__dirname,'..');if(!/^[a-z]{2}$/.test(code||''))throw Error('Usage: node tools/localization-proof.cjs <code> [output.txt]');
const ctx={window:{}};vm.runInNewContext(fs.readFileSync(path.join(root,'docs/locale-messages.js'),'utf8'),ctx);
const web=ctx.window.AdeeboMeterMessages[code];if(!web)throw Error('Unknown catalog');
const win=fs.readFileSync(path.join(root,'knowledge/lang-'+code+'.txt'),'utf8').replace(/^\uFEFF/,'').split(/\r?\n/).filter(s=>s&&!s.startsWith('#')&&s.includes('\t'));
const lines=['AdeeboMeter — '+code+' translation proof','English source, TAB, translation. Native-speaker proof review is required.','Product names, protocols, addresses, file extensions, code and keys may intentionally remain unchanged.','','WEBSITE AND MOBILE — '+Object.keys(web).length+' entries',...Object.entries(web).map(x=>x.join('\t')),'','WINDOWS — '+win.length+' entries',...win,''];
const out=process.argv[3]?path.resolve(process.argv[3]):path.join(root,'lang-'+code+'-proof.txt');fs.writeFileSync(out,'\uFEFF'+lines.join('\r\n'));console.log(out);
