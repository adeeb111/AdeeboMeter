import json, sys
from playwright.sync_api import sync_playwright
B="http://127.0.0.1:8765/"
pages=["index.html","privacy.html","hall-of-fame.html"]
JS_CHECK=r"""
() => {
 const out={};
 out.sw=document.documentElement.scrollWidth; out.cw=document.documentElement.clientWidth;
 const wide=[]; for (const e of document.querySelectorAll('body *')) { const r=e.getBoundingClientRect(); if (r.width>0 && (r.right>document.documentElement.clientWidth+1) && getComputedStyle(e).position!=='fixed') { let p=e.parentElement, clipped=false; while(p){const s=getComputedStyle(p); if(s.overflowX!=='visible'&&p!==document.body&&p!==document.documentElement){clipped=true;break;} p=p.parentElement;} if(!clipped) wide.push(e.tagName+'.'+e.className+'#'+e.id+' r='+Math.round(r.right)+' '+(e.textContent||'').trim().slice(0,40)); } }
 out.wide=wide.slice(0,8);
 const ids=new Set([...document.querySelectorAll('[id]')].map(e=>e.id));
 out.badAnchors=[...document.querySelectorAll('a[href^="#"]')].map(a=>a.getAttribute('href')).filter(h=>h.length>1 && !ids.has(h.slice(1)));
 out.local=[...document.querySelectorAll('a[href],img[src],video[src],source[src],link[href],script[src],video[poster]')].map(e=>e.getAttribute('href')||e.getAttribute('src')||e.getAttribute('poster')).filter(h=>h && !/^(https?:|mailto:|#|data:|javascript:)/.test(h));
 function lum(c){const m=c.match(/[\d.]+/g); if(!m) return null; let [r,g,b,a]=m.map(Number); if(a===0) return null; return [r,g,b].map(v=>{v/=255;return v<=0.03928?v/12.92:Math.pow((v+0.055)/1.055,2.4)}).reduce((s,v,i)=>s+v*[0.2126,0.7152,0.0722][i],0);}
 function bg(e){while(e){const c=getComputedStyle(e).backgroundColor; const m=c.match(/[\d.]+/g); if(m && (m.length<4||Number(m[3])>0.5) && getComputedStyle(e).backgroundImage==='none') return c; if(getComputedStyle(e).backgroundImage!=='none') return null; e=e.parentElement;} return 'rgb(255,255,255)';}
 const low=[]; for(const e of document.querySelectorAll('body *')){ if(![...e.childNodes].some(n=>n.nodeType===3&&n.textContent.trim())) continue; const s=getComputedStyle(e); if(s.visibility==='hidden'||s.display==='none'||e.getClientRects().length===0) continue; const b=bg(e); if(!b) continue; const L1=lum(s.color),L2=lum(b); if(L1==null||L2==null) continue; const cr=(Math.max(L1,L2)+0.05)/(Math.min(L1,L2)+0.05); const big=parseFloat(s.fontSize)>=24||(parseFloat(s.fontSize)>=18.6&&parseInt(s.fontWeight)>=700); if(cr<(big?3:4.5)) low.push(cr.toFixed(2)+' '+e.tagName+'.'+e.className+' "'+e.textContent.trim().slice(0,40)+'" '+s.color+' on '+b);}
 out.low=low.slice(0,12); out.lowN=low.length;
 return out;
}"""
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/usr/bin/google-chrome", args=["--no-sandbox"])
    for scheme in ["light","dark"]:
        for w in [360,768,1366]:
            ctx=br.new_context(viewport={"width":w,"height":800}, color_scheme=scheme)
            for pg in pages:
                page=ctx.new_page(); errs=[]
                page.on("console", lambda m, errs=errs: errs.append(m.type+": "+m.text[:160]) if m.type in ("error","warning") else None)
                page.on("pageerror", lambda e, errs=errs: errs.append("pageerror: "+str(e)[:160]))
                page.on("requestfailed", lambda r, errs=errs: errs.append("reqfail: "+r.url[:120]))
                page.goto(B+pg, wait_until="networkidle" if False else "load", timeout=30000); page.wait_for_timeout(800)
                r=page.evaluate(JS_CHECK)
                print(f"== {scheme} {w} {pg}: sw={r['sw']} cw={r['cw']} wide={r['wide'][:4]} badAnchors={r['badAnchors']} lowContrast={r['lowN']}")
                if w==1366 or r['lowN']: 
                    for l in r['low'][:6]: print("   low:",l)
                for e in errs[:6]: print("   ",e)
                if scheme=="light" and w==1366:
                    print("   local refs:", sorted(set(r['local'])))
                page.close()
            ctx.close()
    br.close()
