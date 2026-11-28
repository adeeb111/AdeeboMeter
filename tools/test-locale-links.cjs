// Regression for a translated blog opened directly, before any saved preference.
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const source=fs.readFileSync(require('path').join(__dirname,'../docs/locale.js'),'utf8');
const snippet=source.match(/document\.addEventListener\("click", function \(event\) \{[\s\S]*?\}, true\);/)[0];
for(const code of ['fr','es','bn','ar','zh'])for(const [href,expected]of [
 ['/','http://localhost/?lang='+code],['/app.html','http://localhost/app.html?lang='+code],
 ['/live.html#scores','http://localhost/live.html?lang='+code+'#scores'],
 ['/?lang=en',null],['/fr/',null],['https://example.com/',null],['#section',null],['/book.pdf',null]
]){
 let handler;const link={href:null,getAttribute:()=>href,hasAttribute:()=>false};
 vm.runInNewContext(snippet,{URL,code,location:{href:'http://localhost/'+code+'/blog/',origin:'http://localhost'},document:{addEventListener:(name,fn)=>{handler=fn;}}});
 handler({target:{closest:()=>link},defaultPrevented:false,button:0});assert.strictEqual(link.href,expected,code+' '+href);
}
console.log('Locale link regression: 40 cases passed, including blog-home, phone app and explicit English choice.');
