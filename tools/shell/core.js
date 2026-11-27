(function(root){
'use strict';
const LIMIT=200000, MAXFILES=100;
function fresh(){return {version:1,cwd:'/data',files:{'/data/readme.txt':'Welcome to AdeeboShell Playground.\nThese files live only in your browser.\nTry: echo "my first note" > notes.txt\nThen: cat notes.txt\n', '/data/latency.txt':'router 2\ninternet 24\nnames 18\n'},dirs:['/','/data'],scenario:'healthy'};}
function validate(x){if(!x||x.version!==1||!Array.isArray(x.dirs)||!x.files||typeof x.files!=='object'||Array.isArray(x.files))throw Error('Not a Playground project.');
if(JSON.stringify(x).length>LIMIT||Object.keys(x.files).length>MAXFILES||x.dirs.length>MAXFILES)throw Error('Project is too large.');
const safe=p=>typeof p==='string'&&/^\/data(?:\/[\w .-]+)*$/.test(p)&&!p.split('/').some(s=>s==='.'||s==='..');
if(x.dirs.some(p=>p!=='/'&&!safe(p))||Object.entries(x.files).some(([p,v])=>!safe(p)||p==='/data'||typeof v!=='string'||x.dirs.includes(p)))throw Error('Invalid workspace paths.');
const dirs=[...new Set(['/','/data',...x.dirs])];
const parent=p=>p.slice(0,p.lastIndexOf('/'))||'/';
if([...dirs.filter(p=>p!=='/'),...Object.keys(x.files)].some(p=>!dirs.includes(parent(p))))throw Error('Missing parent folder.');
return {version:1,cwd:dirs.includes(x.cwd)?x.cwd:'/data',dirs,files:Object.fromEntries(Object.entries(x.files)),scenario:['healthy','wifi','dns'].includes(x.scenario)?x.scenario:'healthy'};}
function tokens(line){const out=[];let s='',quote='',active=false;for(const c of line){if(quote){if(c===quote)quote='';else s+=c;active=true;}else if(c==='"'||c==="'"){quote=c;active=true;}else if(/\s/.test(c)){if(active){out.push(s);s='';active=false;}}else if(c==='|'||c==='>'){if(active){out.push(s);s='';active=false;}out.push(c);}else{s+=c;active=true;}}if(quote)throw Error('Close the quote first.');if(active)out.push(s);return out;}
class Shell{
constructor(state){this.state=validate(state||fresh());}
path(p='.'){let parts=(p.startsWith('/')?p:this.state.cwd+'/'+p).split('/'),a=[];for(const x of parts){if(!x||x==='.')continue;if(x==='..')a.pop();else a.push(x);}const result='/'+a.join('/');if(result!=='/'&&!/^\/data(?:\/[\w .-]+)*$/.test(result))throw Error('Use the /data workspace.');return result;}
write(p,value){if(!p.startsWith('/data/')||this.state.dirs.includes(p))throw Error('Choose a file inside /data.');if(!this.state.dirs.includes(p.slice(0,p.lastIndexOf('/'))))throw Error('Parent folder does not exist.');const before=this.state.files[p];this.state.files[p]=value;try{validate(this.state);}catch(e){if(before===undefined)delete this.state.files[p];else this.state.files[p]=before;throw e;}}
run(line){try{if(line.length>4096)throw Error('Command is too long.');const t=tokens(line);if(!t.length)return {text:'',kind:'Local'};let redirect=null;const ri=t.indexOf('>');if(ri>=0){if(ri!==t.length-2)throw Error('Use command > file.');redirect=this.path(t[ri+1]);t.splice(ri);}
const chunks=[[]];for(const w of t){if(w==='|')chunks.push([]);else chunks[chunks.length-1].push(w);}if(chunks.length>8)throw Error('Use at most eight pipeline stages.');let result={text:'',kind:'Local'},simulated=false;
for(let i=0;i<chunks.length;i++){if(!chunks[i].length)throw Error('Missing command.');result=this.command(chunks[i],i?result.text:null);simulated=simulated||result.kind==='Simulated';}if(simulated)result.kind='Simulated';
if(redirect){this.write(redirect,result.text);return {text:'Saved '+redirect,kind:result.kind};}return result;
}catch(e){return {text:e.message,kind:'Error'};}}
command(t,input){const [cmd,...a]=t,s=this.state,local=text=>({text,kind:'Local'}),sim=text=>({text,kind:'Simulated'});const read=p=>{p=this.path(p);if(!Object.hasOwn(s.files,p))throw Error('File not found: '+p);return s.files[p];};
switch(cmd){
case 'help':return local('Local: help, pwd, ls, cd, cat, echo, mkdir, touch, rm, cp, wc, grep, sort, uniq, clear\nPipes: cat latency.txt | sort\nSave text: echo "hello" > notes.txt\nSimulated: status, ping, dig, traceroute\nExamples: ping 1.1.1.1 · dig example.com\nOnly the commands listed here are supported. No system commands or scripts execute.');
case 'pwd':return local(s.cwd);
case 'ls':{const p=this.path(a[0]);if(Object.hasOwn(s.files,p))return local(p);if(!s.dirs.includes(p))throw Error('Folder not found.');return local([...s.dirs.filter(x=>x!==p&&x.slice(0,x.lastIndexOf('/'))===(p==='/'?'':p)).map(x=>x.split('/').pop()+'/'),...Object.keys(s.files).filter(x=>x.slice(0,x.lastIndexOf('/'))===(p==='/'?'':p)).map(x=>x.split('/').pop())].sort().join('\n')||'(empty)');}
case 'cd':{const p=this.path(a[0]||'/data');if(!s.dirs.includes(p))throw Error('Folder not found.');s.cwd=p;return local(p);}
case 'echo':return local(a.join(' ')+'\n');
case 'cat':return local(a.length?a.map(read).join(''):input||'');
case 'mkdir':{if(a.length!==1)throw Error('Use mkdir folder.');const p=this.path(a[0]);if(s.dirs.includes(p)||Object.hasOwn(s.files,p))throw Error('Already exists.');if(!s.dirs.includes(p.slice(0,p.lastIndexOf('/'))))throw Error('Parent folder does not exist.');if(s.dirs.length>=MAXFILES)throw Error('Too many folders.');s.dirs.push(p);return local('Created '+p);}
case 'touch':if(a.length!==1)throw Error('Use touch file.');if(!Object.hasOwn(s.files,this.path(a[0])))this.write(this.path(a[0]),'');return local('File ready.');
case 'rm':{if(a.length!==1)throw Error('Use rm file. Folder deletion is not supported.');const p=this.path(a[0]);read(a[0]);delete s.files[p];return local('Removed '+p+' from this workspace.');}
case 'cp':if(a.length!==2)throw Error('Use cp source destination.');this.write(this.path(a[1]),read(a[0]));return local('Copied.');
case 'wc':case 'sort':case 'uniq':case 'grep':{let text;if(cmd==='grep'){if(!a.length)throw Error('Use grep word [file].');text=a[1]?read(a[1]):input||'';}else text=a[0]?read(a[0]):input||'';let lines=text.replace(/\n$/,'').split('\n');if(!text)lines=[];if(cmd==='wc')return local(lines.length+' lines · '+(text.match(/\S+/g)||[]).length+' words · '+Array.from(text).length+' characters');if(cmd==='sort')lines.sort();if(cmd==='uniq')lines=lines.filter((x,i)=>!i||x!==lines[i-1]);if(cmd==='grep')lines=lines.filter(x=>x.includes(a[0]));return local(lines.join('\n'));}
case 'clear':return {text:'',kind:'Local',clear:true};
case 'status':return sim(s.scenario==='wifi'?'Router replies are slow in this example.\nrouter   180 ms\ninternet 204 ms\nnames    198 ms\nthis PC  CPU 12% · memory 48%':s.scenario==='dns'?'Name lookup is failing in this example.\nrouter   2 ms\ninternet 24 ms\nnames    no answer\nthis PC  CPU 12% · memory 48%':'Connection looks steady in this example.\nrouter   2 ms\ninternet 24 ms\nnames    18 ms\nthis PC  CPU 12% · memory 48%');
case 'ping':if(!['1.1.1.1','192.168.1.1','example.com'].includes(a[0])||a.length>1)throw Error('Demo targets: ping 1.1.1.1 · ping 192.168.1.1 · ping example.com');return sim(a[0]==='example.com'&&s.scenario==='dns'?'Name lookup failed. No probe was sent.':'Illustrative reply from '+a[0]+'\ntime='+(a[0]==='192.168.1.1'?(s.scenario==='wifi'?'180':'2'):(s.scenario==='wifi'?'204':'24'))+' ms\nCompare the router and internet targets.');
case 'dig':if(a[0]!=='example.com'||a.length!==1)throw Error('Try dig example.com.');return sim(s.scenario==='dns'?'DNS server did not answer in this scenario.':'example.com.  300  IN  A  192.0.2.10\nDocumentation address used for this simulation.');
case 'traceroute':if(a[0]!=='example.com'||a.length!==1)throw Error('Try traceroute example.com.');return sim('Illustrative path (not your route)\n1  router       '+(s.scenario==='wifi'?'180':'2')+' ms\n2  provider     '+(s.scenario==='wifi'?'190':'12')+' ms\n3  destination  '+(s.scenario==='wifi'?'204':'24')+' ms');
default:throw Error('Not supported in this Playground. Type help for available commands.');
}}
}
const api={Shell,fresh,validate};if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.AMShell=api;
})(typeof window==='undefined'?{}:window);
