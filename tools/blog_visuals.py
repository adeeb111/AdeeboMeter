"""Flat explanatory visuals and a browsable card layout after article/language generation."""
from pathlib import Path
import re,html
DOCS=Path(__file__).resolve().parents[1]/'docs'
# Each illustration is diagrammatic, with no embedded language-specific prose.
def box(x,y,w,h,stroke='var(--blue)',rx=8):return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="var(--soft)" stroke="{stroke}" stroke-width="3"/>'
def line(x,y,x2,y2,color='var(--blue)',dash=''):return f'<path d="M{x} {y}H{x2}" fill="none" stroke="{color}" stroke-width="4"/>' if y==y2 and not dash else f'<path d="M{x} {y}L{x2} {y2}" fill="none" stroke="{color}" stroke-width="4"'+(' stroke-dasharray="8 7"' if dash else '')+'/>'
def circle(x,y,r=7,color='var(--ok)'):return f'<circle cx="{x}" cy="{y}" r="{r}" fill="{color}"/>'
def sheet(x=75,y=32):return box(x,y,82,112)+''.join(line(x+18,y+i,x+63,y+i,'var(--sub)') for i in (25,42,59))
def lock(x=235,y=70):return f'<path d="M{x+12} {y}v-18a22 22 0 0 1 44 0v18" fill="none" stroke="var(--ok)" stroke-width="5"/>'+box(x,y,68,54,'var(--ok)')+circle(x+34,y+24,5)
def key(x=350,y=90):return f'<circle cx="{x}" cy="{y}" r="14" fill="none" stroke="var(--warn)" stroke-width="4"/><path d="M{x+14} {y}h42m-10 0v12m-14-12v9" fill="none" stroke="var(--warn)" stroke-width="4"/>'
def network():return box(42,57,85,58)+line(127,86,235,86,'var(--ok)')+box(235,62,68,48)+line(303,86,405,86,'var(--bad)','dash')+box(405,40,55,92)+circle(180,86)+f'<path d="m342 74 24 24m0-24-24 24" stroke="var(--bad)" stroke-width="4"/>'
def terminal():return box(70,28,370,130)+line(70,54,440,54,'var(--line)')+circle(87,42,4,'var(--bad)')+circle(102,42,4,'var(--warn)')+circle(117,42,4)+f'<path d="m95 78 14 12-14 12m29 0h24" stroke="var(--ok)" stroke-width="4" fill="none"/>'+line(175,90,275,90,'var(--sub)')+line(95,123,215,123,'var(--blue)')+line(230,123,350,123,'var(--ok)')
def wifi():return box(50,65,74,48)+box(385,65,74,48)+f'<path d="M166 95q85-90 172 0M195 110q56-57 114 0" fill="none" stroke="var(--blue)" stroke-width="5"/>'+circle(252,126)+line(125,147,385,147,'var(--ok)')
def buffering():return box(55,30,240,124)+f'<path d="m155 65 55 28-55 28z" fill="var(--blue)"/>'+f'<path d="M335 137h115M335 107h70M335 77h115M335 47h35" stroke="var(--ok)" stroke-width="9"/>'+box(218,103,56,33,'var(--warn)',6)
def gaming():return box(35,36,205,120)+''.join(box(58+i*37,66,26,60,'var(--blue)',3) for i in range(4))+line(270,107,465,107,'var(--ok)')+circle(295,107)+circle(348,107)+f'<path d="m382 97 20 20m0-20-20 20" stroke="var(--bad)" stroke-width="4"/>'+circle(445,107)
def evening():return line(48,148,466,148,'var(--sub)')+''.join(f'<rect x="{70+i*62}" y="{148-h}" width="32" height="{h}" rx="4" fill="{c}"/>' for i,(h,c) in enumerate([(30,'var(--ok)'),(35,'var(--ok)'),(37,'var(--ok)'),(93,'var(--warn)'),(115,'var(--bad)'),(85,'var(--warn)')]))
def loss():return ''.join(line(70,48+i*42,430,48+i*42,'var(--line)')+circle(88,48+i*42,6,'var(--blue)')+''.join(circle(190+j*37,48+i*42,7,'var(--bad)' if i==1 and j in(1,3) else 'var(--ok)') for j in range(6)) for i in range(3))
def pc():return box(170,35,145,120)+''.join(line(150,50+i*26,170,50+i*26)+line(315,50+i*26,335,50+i*26) for i in range(4))+f'<path d="M192 120h16l14-35 16 45 20-65 15 55h20" fill="none" stroke="var(--warn)" stroke-width="4"/>'
ART={'lab':network(),'terminal':terminal(),'encrypt':sheet(65,38)+line(160,94,225,94,'var(--blue)')+lock(240,70)+key(370,94),'open':sheet(65,38)+key(235,60)+line(165,116,350,116,'var(--ok)')+box(365,32,70,126)+box(378,55,44,66,'var(--ok)',3),'wifi':wifi(),'buffer':buffering(),'gaming':gaming(),'evening':evening(),'loss':loss(),'pc':pc(),'report':sheet(110,25)+f'<path d="M240 130h30l20-35 20 35 20-70 20 70h50" fill="none" stroke="var(--bad)" stroke-width="4"/>'}
ART['transfer']=box(42,30,65,126)+box(54,53,41,65,'var(--ok)',3)+line(122,94,206,94,'var(--ok)')+sheet(214,35)+line(305,94,365,94,'var(--blue)')+box(375,42,105,76)+line(395,141,460,141,'var(--blue)')+line(427,120,427,141,'var(--blue)')
SHORT={
'network-lab-online-no-install':'Draw a small network. Try a fault. Save it on phone or PC.',
'practise-terminal-commands-online':'Create a file, run commands and try a pipe inside your browser.',
'encrypt-file-without-uploading':'Lock a file locally. Keep the code separate. Know what AES-256 protects.',
'open-adeebo-file-phone-pc':'Code or private key? Find the right way to open the file.',
'fast-speed-test-buffering':'A fast test. A stalled video. Compare what happens during playback.',
'game-lag-or-low-fps':'Frames or connection? Watch the right number before changing settings.',
'internet-slow-every-evening':'Compare a good hour with a bad one. Keep the test fair.',
'wifi-disconnects-ethernet-works':'Same PC. Same service. Change the first link.',
'mtr-one-hop-packet-loss':'One red row is not the whole story. Follow replies to the destination.'}
TOPICS={'network-lab-online-no-install':'lab','practise-terminal-commands-online':'terminal','encrypt-file-without-uploading':'encrypt','open-adeebo-file-phone-pc':'open','fast-speed-test-buffering':'buffer','game-lag-or-low-fps':'gaming','internet-slow-every-evening':'evening','wifi-disconnects-ethernet-works':'wifi','mtr-one-hop-packet-loss':'loss','wifi-or-isp':'wifi','internet-speed-monitor':'evening','pc-monitoring':'pc','isp-report':'report','adeeboshell':'terminal','adeebolab-guide':'lab'}
def svg(kind,cls='card-art'):
 return '<svg class="'+cls+'" viewBox="0 0 510 185" aria-hidden="true" focusable="false">'+ART[kind]+'</svg>'
for lang in ('','bn','fr','es','ar','zh'):
 p=DOCS/lang/'blog/index.html';s=p.read_text(encoding='utf-8')
 if '/blog-index.css' not in s:s=s.replace('</head>','<link rel="stylesheet" href="/blog-index.css">\n</head>',1)
 # Attach an illustration to each whole-card link; keep text first in source for the index schema builder.
 def card(m):
  start,body=m[1],m[2]
  body=re.sub(r'<svg class="card-art".*?</svg>','',body,flags=re.S)
  body=re.sub(r'<ol class="path".*?</ol>','',body,flags=re.S)
  href=re.search(r'href="([^"]+)"',start)[1];slug=href.rstrip('/').split('/')[-1].removesuffix('.html')
  if lang=='' and slug in SHORT:body=re.sub(r'<p>.*?</p>',lambda _: '<p>'+html.escape(SHORT[slug])+'</p>',body,count=1,flags=re.S)
  return start+body+svg(TOPICS.get(slug,'wifi'))+'</a></li>'
 s=re.sub(r'(<li><a href="[^"]+">)(<div><h2>.*?)(?:</a></li>)',card,s,flags=re.S)
 s=re.sub(r'(<li><a href="[^"]+">)(?!<div><h2>)(.*?)(<svg class="card-art".*?</svg>)(</a></li>)',r'\1\2\4',s,flags=re.S)
 if lang=='':
  s=s.replace('<h1>Why is it slow?</h1>','<h1>Learn it. Try it. Fix it.</h1>')
  s=s.replace('A slowdown starts in one of five places: your PC, your Wi-Fi, your router, your provider or the far end. Each guide checks one of them with a test that takes a few minutes.','Build a lab, practise commands, protect a file or find what’s slowing you down. Pick a guide and try one useful thing.')
  s=s.replace('Troubleshooting Blog | AdeeboMeter','Practical Guides: Network Labs, Terminal &amp; File Privacy | AdeeboMeter')
  s=s.replace('Practical Windows troubleshooting guides for slow internet, Wi-Fi dropouts, packet loss, and PC slowdowns.','Visual guides to browser network labs, terminal practice, local file encryption and Windows troubleshooting. Pick a guide and try it.')
 p.write_text(s,encoding='utf-8',newline='\n')
# Add a second explanatory graphic where the article needs a pause, not a decorative banner.
EXPLAIN={
'network-lab-online-no-install':('Keep a portable copy.', 'transfer', 'Your browser cache is local. Export a project file for a backup; a QR link opens a snapshot on another device.'),
'practise-terminal-commands-online':('Your workspace has a boundary.', 'terminal', 'Commands work on virtual /data files. Export moves that workspace as a project; it does not turn the browser into a system shell.'),
'encrypt-file-without-uploading':('Two keys. Two jobs.', 'encrypt', 'The derived encryption key protects the content. The separate HMAC key checks the sealed data. The sharing code is needed to derive both.'),
'open-adeebo-file-phone-pc':('Opening creates an ordinary copy.', 'open', 'A successful code-mode check downloads the original. That downloaded file is no longer protected by the .adeebo wrapper.'),
'fast-speed-test-buffering':('Measure the moment that stalls.', 'buffer', 'A fast speed-test result and a stalled player can happen together. Compare the same stream and quality while the problem is visible.'),
'game-lag-or-low-fps':('Two readings. Two jobs.', 'gaming', 'Frames come from the PC. Game updates arrive over the network. Keep FPS and ping together before changing settings.'),
'internet-slow-every-evening':('Keep the comparison fair.', 'evening', 'Repeat the same test before and during the slowdown. These bars illustrate a pattern; they are not measured results.'),
'wifi-disconnects-ethernet-works':('Change the first link.', 'wifi', 'Compare Wi-Fi and Ethernet on the same PC and service. A cable comparison narrows the question; it does not prove every wireless fault.'),
'mtr-one-hop-packet-loss':('Follow the loss to the end.', 'loss', 'The red middle row shows missing probe replies. Green destination replies show why one intermediate row alone cannot prove forwarded traffic was lost.')}
for slug,(title,kind,caption) in EXPLAIN.items():
 p=DOCS/'blog'/(slug+'.html');s=p.read_text(encoding='utf-8')
 s=re.sub(r'<!--article-explainer:start-->.*?<!--article-explainer:end-->\s*','',s,flags=re.S)
 diagram=svg(kind,'explainer-art')
 if slug=='encrypt-file-without-uploading':diagram='<div class="two-scenes"><div><b>Encryption key</b><p>AES-256-CBC</p><strong>Protects the content</strong></div><div><b>Authentication key</b><p>HMAC-SHA256</p><strong>Checks for changes</strong></div></div>'
 block='<!--article-explainer:start--><figure class="article-visual article-explainer"><h2>'+title+'</h2>'+diagram+'<figcaption>'+caption+'</figcaption></figure><!--article-explainer:end-->\n'
 marker='<aside class="summary"';i=s.find(marker)
 if i<0:raise ValueError('Missing article summary: '+slug)
 s=s[:i]+block+s[i:];p.write_text(s,encoding='utf-8',newline='\n')
print('Illustrated cards on six Blog indexes; contextual explainers in nine articles.')
