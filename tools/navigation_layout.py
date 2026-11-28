"""Keep navigation concise and browser playgrounds discoverable after language builds."""
from pathlib import Path
import re,html
DOCS=Path(__file__).resolve().parents[1]/'docs'
labels={
'en':['Mobile app','Files','Games','Live','Privacy','More to explore','Gaming','Streaming','Trading','ISP report','Hall of Fame','Poster'],
'fr':['Application mobile','Fichiers','Jeux','En direct','Confidentialité','À découvrir aussi','Jeu en ligne','Streaming','Trading','Rapport FAI','Palmarès','Affiche'],
'es':['Aplicación móvil','Archivos','Juegos','En directo','Privacidad','Más para explorar','Gaming','Streaming','Trading','Informe para tu proveedor','Salón de la fama','Póster'],
'bn':['মোবাইল অ্যাপ','ফাইল','গেমস','লাইভ','গোপনীয়তা','আরও দেখুন','গেমিং','স্ট্রিমিং','ট্রেডিং','আইএসপি রিপোর্ট','হল অব ফেম','পোস্টার'],
'ar':['تطبيق الهاتف','الملفات','الألعاب','مباشر','الخصوصية','المزيد للاستكشاف','الألعاب','البث','التداول','تقرير المزوّد','لوحة الشرف','الملصق'],
'zh':['手机应用','文件','游戏','实时信息','隐私','探索更多','游戏','流媒体','交易','运营商报告','名人堂','海报']}
# Reuse the shared navigation vocabulary instead of introducing competing translations.
site=(DOCS/'site.js').read_text(encoding='utf-8')
for lang,values in labels.items():
 block=re.search(r'\b'+lang+r':\s*\{(.*?)(?=\n\s*(?:en|fr|es|ar|bn|zh):|\n\s*}\[lang\])',site,re.S)
 if not block:raise SystemExit('Missing navigation language: '+lang)
 for idx,key in enumerate(('phone','files','games','live','privacy',None,'gaming','streaming','trading','isp','hof')):
  if key is None:continue
  value=re.search(r'["\']?'+key+r'["\']?\s*:\s*(?:\'((?:\\.|[^\'])*)\'|"((?:\\.|[^"])*)")',block[1])
  if not value:raise SystemExit('Missing navigation label: '+lang+'/'+key)
  values[idx]=(value[1] if value[1] is not None else value[2]).replace("\\'", "'")
def link(url,label,cls=''):
 return '<a'+(' class="'+cls+'"' if cls else '')+' href="'+url+'">'+html.escape(label)+'</a>'
icons={
'shell':'<path d="m5 7 5 5-5 5M13 17h6"/>',
'lab':'<rect x="3" y="3" width="6" height="5" rx="1"/><rect x="15" y="16" width="6" height="5" rx="1"/><path d="M6 8v10h9M6 12h12v4"/>'}
def card(kind,title,text,color):
 return '<a class="v3card v3tile" data-playground="'+kind+'" href="/'+kind+'.html"><span class="v3top"><span class="v3chip" style="--k:var(--'+color+')"><svg class="i" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">'+icons[kind]+'</svg></span><b>'+title+'</b></span><span class="d">'+text+'</span></a>'
cards=card('shell','Terminal Playground','Run commands. Create text files. Try simulated network checks. English · no installation.','green')+card('lab','Network Lab Playground','Draw a network. Break one thing. Follow the packet. English · no account.','blue')
for lang,l in labels.items():
 pre='' if lang=='en' else '/'+lang
 p=DOCS/(pre.strip('/')+'/' if pre else '')/'index.html'
 s=p.read_text(encoding='utf-8')
 # Six languages share one desktop row; compact screens retain a swipeable row.
 s=re.sub(r'<style id="language-row-layout">.*?</style>', '', s, flags=re.S)
 s=s.replace('</head>', '<style id="language-row-layout">@media(min-width:901px){.v5lcard{flex-wrap:nowrap;gap:24px}.v5lcard>div:first-child{flex:0 1 30%}.v5ltiles{flex:1 1 0;min-width:0;grid-template-columns:repeat(6,minmax(0,1fr));gap:10px}.v5l{min-width:0;padding:14px 10px}.v5l>b{font-size:clamp(18px,1.65vw,26px);white-space:nowrap}.v5l span{font-size:14px}}@media(min-width:901px) and (max-width:1199px){.v5lcard{flex-wrap:wrap}.v5lcard>div:first-child{flex:1 1 100%}.v5ltiles{flex:1 1 100%}}</style></head>',1)
 # Complete the six-language card family without translating the greetings.
 tiles=re.search(r'(<div class="v5ltiles">)(.*?)(</div>)',s,re.S)
 if not tiles:raise SystemExit('Missing language cards: '+str(p))
 content=tiles[2]
 content=re.sub(r'(<a class="v5l"[^>]*href=")[^"]*("[^>]*hreflang="en")',r'\1/?lang=en\2',content,count=1)
 if 'hreflang="zh-Hans"' not in content:
  content+='<a class="v5l" href="/zh/" hreflang="zh-Hans" style="--a:#D97706;--b:#DC2626"><b lang="zh-Hans">欢迎</b><span lang="zh-Hans">简体中文<small data-cov="zh">100%</small></span></a>'
 s=s[:tiles.start()]+tiles[1]+content+tiles[3]+s[tiles.end():]
 # Preserve the narrow-screen copies of primary links, but put mobile first.
 m=re.search(r'(<details class="navmore">.*?<div>)(.*?)(</div></details>)',s,re.S)
 if not m:raise SystemExit('Missing home More menu: '+str(p))
 narrow=''.join(re.findall(r'<a\b[^>]*class="nm-narrow"[^>]*>.*?</a>',m[2],re.S))
 menu=link(pre+'/app.html',l[0])+narrow+link('/shell.html','Terminal Playground')+link('/lab.html','Network Lab Playground')+link(pre+'/files.html',l[1])+link(pre+'/games.html',l[2])+link(pre+'/live.html',l[3])+link(pre+'/privacy.html',l[4])
 s=s[:m.start()]+m[1]+menu+m[3]+s[m.end():]
 s=re.sub(r'<a class="v3card v3tile" data-playground="(?:shell|lab)".*?</a>','',s,flags=re.S)
 if '<div class="v3four">' not in s:raise SystemExit('Missing browser cards: '+str(p))
 s=s.replace('<div class="v3four">','<div class="v3four">'+cards,1)
 # Remove the temporary one-line Shell link; dedicated browser cards replace it.
 s=re.sub(r'\s*<p class="v5also">Try <a href="/shell.html">Terminal Playground</a>:.*?</p>','',s)
 p.write_text(s,encoding='utf-8',newline='\n')
 p=p.parent/'blog/index.html';s=p.read_text(encoding='utf-8')
 s=re.sub(r'<!--blog-resources:start-->.*?<!--blog-resources:end-->\s*','',s,flags=re.S)
 resources='<section class="example" aria-labelledby="blog-resources"><h2 id="blog-resources">'+l[5]+'</h2><ul class="next-checks">'
 for slug,label in zip(('gaming.html','streaming.html','trading.html','isp-report.html','hall-of-fame.html'),l[6:11]):resources+='<li>'+link(pre+'/'+slug,label)+'</li>'
 resources+='<li>'+link('/img/poster.png',l[11])+'</li></ul></section>'
 s=s.replace('</main>','<!--blog-resources:start-->'+resources+'<!--blog-resources:end-->\n</main>',1)
 p.write_text(s,encoding='utf-8',newline='\n')
print('Updated 6 homepages and 6 Blog indexes; playground cards remain English.')
