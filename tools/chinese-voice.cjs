// Keep the phone/web companion on the same reviewed strings as Windows.
const fs=require('fs'),path=require('path'),root=path.resolve(__dirname,'..'),out=path.join(root,'docs/kb/voice/zh');fs.mkdirSync(out,{recursive:true});let count=0;
for(const file of fs.readdirSync(path.join(root,'knowledge')).filter(f=>/^lines-.*-zh\.txt$/.test(f))){const pack={};for(const line of fs.readFileSync(path.join(root,'knowledge',file),'utf8').split(/\r?\n/)){if(!line||line.startsWith('#'))continue;const i=line.indexOf('|');if(i<1)throw Error(file);(pack[line.slice(0,i)]??=[]).push(line.slice(i+1));count++;}fs.writeFileSync(path.join(out,file.slice(6,-7)+'.json'),JSON.stringify(pack)+'\n');}
console.log('Exported '+count+' Chinese companion lines for web/mobile.');
