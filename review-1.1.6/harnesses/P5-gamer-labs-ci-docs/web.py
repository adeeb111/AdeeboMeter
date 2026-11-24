import json, sys
from playwright.sync_api import sync_playwright
B="http://127.0.0.1:8765/"
pages=sys.argv[1].split(",")
widths=[int(x) for x in sys.argv[2].split(",")]
schemes=sys.argv[3].split(",")
JS=r"""
() => {
 const out={};
 const de=document.documentElement;
 out.dir=de.getAttribute('dir')||getComputedStyle(document.body).direction; out.lang=de.lang;
 out.sw=de.scrollWidth; out.cw=de.clientWidth;
 const wide=[]; for (const e of document.querySelectorAll('body *')) { const r=e.getBoundingClientRect(); if (r.width>0 && (r.right>de.clientWidth+1 || r.left < -1) && getComputedStyle(e).position!=='fixed') { let p=e.parentElement, clipped=false; while(p){const s=getComputedStyle(p); if(s.overflowX!=='visible'&&p!==document.body&&p!==de){clipped=true;break;} p=p.parentElement;} if(!clipped) wide.push(e.tagName+'.'+(typeof e.className==='string'?e.className:'')+'#'+e.id+' L='+Math.round(r.left)+' R='+Math.round(r.right)+' "'+(e.textContent||'').trim().slice(0,40)+'"'); } }
 out.wide=wide.slice(0,6); out.wideN=wide.length;
 const ids=new Set([...document.querySelectorAll('[id],[name]')].map(e=>e.id||e.getAttribute('name')));
 out.badAnchors=[...document.querySelectorAll('a[href^="#"]')].map(a=>a.getAttribute('href')).filter(h=>h.length>1 && !ids.has(decodeURIComponent(h.slice(1))));
 // text that overflows its own box (clipped words)
 const clip=[]; for (const e of document.querySelectorAll('button,a,.pill,.chip,h1,h2,h3,label,span')) { const s=getComputedStyle(e); if (s.overflow==='hidden' && e.scrollWidth>e.clientWidth+2 && s.textOverflow!=='ellipsis' && e.clientWidth>0) clip.push(e.tagName+'.'+(typeof e.className==='string'?e.className:'')+' "'+e.textContent.trim().slice(0,30)+'" '+e.scrollWidth+'>'+e.clientWidth); }
 out.clip=clip.slice(0,6);
 // untranslated: visible text nodes in Latin letters on bn/ar pages
 return out;
}"""
res={}
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1243/chrome-linux64/chrome", args=["--no-sandbox"])
    for scheme in schemes:
        for w in widths:
            ctx=br.new_context(viewport={"width":w,"height":800}, color_scheme=scheme, service_workers="block")
            for pg in pages:
                page=ctx.new_page(); errs=[]; ext=[]
                def route(r, req, ext=ext):
                    u=req.url
                    if u.startswith(B): return r.continue_()
                    ext.append(u[:100]); return r.abort()
                page.route("**/*", route)
                page.on("console", lambda m, errs=errs: errs.append(m.type+": "+m.text[:200]) if m.type in ("error",) else None)
                page.on("pageerror", lambda e, errs=errs: errs.append("pageerror: "+str(e)[:200]))
                page.on("response", lambda r, errs=errs: errs.append("HTTP %d %s"%(r.status, r.url[:120])) if r.status>=400 else None)
                try:
                    page.goto(B+pg, wait_until="load", timeout=30000); page.wait_for_timeout(1200)
                    o=page.evaluate(JS)
                except Exception as e:
                    o={"exc":str(e)[:200]}
                o["errs"]=[e for e in errs if "net::ERR_FAILED" not in e][:8]; o["ext"]=sorted(set(x.split('/')[2] for x in ext))
                key="%s %d %s"%(scheme,w,pg)
                flag = o.get("errs") or o.get("wideN") or o.get("badAnchors") or o.get("clip") or o.get("exc") or (o.get("sw",0)>o.get("cw",0))
                if flag or w==360: print(key, json.dumps(o, ensure_ascii=False)[:1500])
                page.close()
            ctx.close()
    br.close()
