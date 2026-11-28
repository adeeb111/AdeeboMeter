"""Connect the published traceroute tutorial to the existing English lesson and MTR guide."""
from pathlib import Path
import re,json
D=Path(__file__).resolve().parents[1]/'docs'
URL='https://www.youtube.com/watch?v=n7oN_BJuePs'
TITLE='What Is Traceroute? How TTL Reveals Each Hop'
WORK={'@type':'CreativeWork','name':TITLE,'genre':'Video tutorial','url':URL,'inLanguage':'en','datePublished':'2026-11-27','publisher':{'@type':'Organization','name':'AdeeboMeter'}}
for route in ('learn/icmp.html','blog/mtr-one-hop-packet-loss.html'):
 p=D/route;s=p.read_text(encoding='utf-8')
 s=re.sub(r'<!--traceroute-tutorial:start-->.*?<!--traceroute-tutorial:end-->\s*','',s,flags=re.S)
 card='''<!--traceroute-tutorial:start--><section class="card example" id="traceroute-tutorial" aria-labelledby="traceroute-tutorial-heading"><h2 id="traceroute-tutorial-heading">Watch how traceroute finds each hop</h2><p>Follow the probes, TTL limits and replies. See why a silent intermediate hop can still lead to a replying destination.</p><p>English audio · about 3 minutes · real AdeeboMeter MTR example.</p><p><a href="https://www.youtube.com/watch?v=n7oN_BJuePs">Watch: What Is Traceroute? How TTL Reveals Each Hop</a></p><p><a href="https://www.youtube.com/watch?v=n7oN_BJuePs&amp;t=21s">TTL and router replies</a> · <a href="https://www.youtube.com/watch?v=n7oN_BJuePs&amp;t=51s">Read the timing columns</a> · <a href="https://www.youtube.com/watch?v=n7oN_BJuePs&amp;t=77s">Understand missing replies</a></p></section><!--traceroute-tutorial:end-->
'''
 if route.startswith('learn/'):
  s,n=re.subn(r'(<div class="card one">.*?</div>\s*)',lambda m:m[1]+card,s,count=1,flags=re.S)
  if not n:raise ValueError('Lesson introduction not found')
  old=re.search(r'<meta name="description" content="([^"]*)"',s)[1]
  desc='Learn ping, ICMP and traceroute: how TTL reveals hops, what round-trip timings mean, and why a missing hop reply need not mean a broken connection.'
  s=s.replace(old,desc)
 else:
  s,n=re.subn(r'(<figure class="article-visual".*?</figure>\s*)',lambda m:m[1]+card,s,count=1,flags=re.S)
  if not n:raise ValueError('Article introduction not found')
 def relate(m):
  obj=json.loads(m[2]);typ=obj.get('@type')
  if typ in ('LearningResource','BlogPosting'):
   previous=obj.get('subjectOf',[]);previous=previous if isinstance(previous,list) else [previous]
   previous=[x for x in previous if x.get('url')!=URL];obj['subjectOf']=previous+[WORK]
  return m[1]+json.dumps(obj,ensure_ascii=False,separators=(',',':'))+'</script>'
 s=re.sub(r'(<script type="application/ld\+json"[^>]*>)(.*?)</script>',relate,s,flags=re.S)
 p.write_text(s,encoding='utf-8',newline='\n')
p=D/'sitemap.xml';s=p.read_text(encoding='utf-8')
for route in ('learn/icmp.html','blog/mtr-one-hop-packet-loss.html'):
 s=re.sub(r'(<loc>https://adeebometer.com/'+re.escape(route)+r'</loc>)(?:<lastmod>[^<]+</lastmod>)?',lambda m:m[1]+'<lastmod>2026-11-27</lastmod>',s)
p.write_text(s,encoding='utf-8',newline='\n')
print('Traceroute video linked from ICMP lesson and MTR guide, with source metadata.')
