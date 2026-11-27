const fs=require('fs'),vm=require('vm'),assert=require('assert/strict'),zlib=require('zlib');
vm.runInThisContext(fs.readFileSync('tools/lab/project-transfer.js','utf8'));
vm.runInThisContext(fs.readFileSync('docs/qr.js','utf8'));
(async()=>{
 const project={v:1,fid:'fexisting123',name:'আদীব network / café',nodes:[{id:'pc',k:'pc',x:20,y:30,name:'My PC'},{id:'router',k:'router',x:200,y:30,name:'Router'}],links:[{id:'c',a:'pc',b:'router',kind:'cable'}],texts:[],areas:[],ink:[]};
 const token=await AMLabTransfer.encode(project),copy=await AMLabTransfer.decode(token);assert.equal(copy.name,project.name);assert.equal(copy.fid,undefined);assert.deepEqual(copy.nodes,project.nodes);
 const url='https://adeebometer.com/lab.html#project='+token;assert(url.length<1500);assert(AMQR.encode(url));
 await assert.rejects(()=>AMLabTransfer.decode('g.%%%%'));await assert.rejects(()=>AMLabTransfer.decode('j.'+'a'.repeat(12001)));
 const bomb='g.'+zlib.gzipSync(Buffer.alloc(2000001,65)).toString('base64url');await assert.rejects(()=>AMLabTransfer.decode(bomb),/too large/);
 const native=global.CompressionStream;global.CompressionStream=undefined;const plain=await AMLabTransfer.encode(project);assert(plain.startsWith('j.'));assert.equal((await AMLabTransfer.decode(plain)).name,project.name);global.CompressionStream=native;
 const html=fs.readFileSync('docs/lab.html','utf8');let count=0;for(const m of html.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/g)){new vm.Script(m[1]);count++;}
 assert(html.includes("connect-src 'none'"));assert(!html.includes('/*__PROJECT_TRANSFER__*/'));assert(!html.includes('/*__QR__*/'));
 console.log('PASS: Unicode project transfer, fresh ID, gzip/raw fallback, QR capacity, malformed/oversized/bomb rejection, '+count+' inline scripts parse; no-network CSP preserved');
})().catch(e=>{console.error(e);process.exit(1)});
