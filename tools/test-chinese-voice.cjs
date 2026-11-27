const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert'),docs=path.resolve(__dirname,'../docs');
(async()=>{let language='zh',handler;const requests=[];const storage={'am-voice':'epic-fantasy','am-voice-chat':'normal','am-voice-addr':'sir'};
 const context={window:{AdeeboMeterLocale:{code:()=>language}},document:{currentScript:{src:'http://localhost/voice.js'},documentElement:{lang:'zh-Hans'},addEventListener:(name,fn)=>{if(name==='adeebometerlanguagechange')handler=fn;}},localStorage:{getItem:k=>storage[k]||null,setItem:(k,v)=>{storage[k]=v;},removeItem:k=>{delete storage[k];}},addEventListener:()=>{},fetch:async url=>{const p=new URL(url).pathname;requests.push(p);return {ok:true,json:async()=>JSON.parse(fs.readFileSync(path.join(docs,p),'utf8'))};}};
 vm.runInNewContext(fs.readFileSync(path.join(docs,'voice.js'),'utf8'),context);await context.window.AMV.ready;
 for(let i=0;i<8;i++){const text=context.window.AMV.line('app.open');assert(/[\u3400-\u9fff]/.test(text),text);assert(!/\{addr\}|my lord|friend/.test(text),text);}
 language='en';handler();await new Promise(setImmediate);assert(!/[\u3400-\u9fff]/.test(context.window.AMV.line('app.open')));
 language='zh';handler();await new Promise(setImmediate);assert(/[\u3400-\u9fff]/.test(context.window.AMV.line('app.open')));
 assert(requests.includes('/kb/voice/zh/epic-fantasy.json'));assert(requests.includes('/kb/voice/epic-fantasy.json'));
 console.log('Chinese companion regression passed: localized packs, addresses and zh/en/zh switching.');
})().catch(e=>{console.error(e);process.exitCode=1;});
