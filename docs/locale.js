(function () {
  "use strict";

  var languages = {
    en: "English", fr: "Français", es: "Español", ar: "العربية",
    bn: "বাংলা", zh: "简体中文"
  };
  var tags = { fr: "fr-CA", zh: "zh-Hans" };
  var storageKey = "adeebometer-language";
  var root = document.documentElement;
  root.classList.add("locale-js");   // 24 Nov: hide the plain-link fallback from the first paint, not only after DOMContentLoaded (it flashed "Français  বাংলা" on reload)
  var query = new URLSearchParams(location.search).get("lang");
  var messages = window.AdeeboMeterMessages || {};
  var languageRoute = root.dataset.languageRoute;
  var pageLanguage = languageRoute ? root.lang : null;
  var pageCode = pageLanguage ? pageLanguage.split("-")[0] : "en";
  var routeLanguages = (root.dataset.languageVariants || "en bn").split(" ");

  function known(value) {
    return value === "auto" || Object.prototype.hasOwnProperty.call(languages, value);
  }

  function saved() {
    try { return localStorage.getItem(storageKey); } catch (_) { return null; }
  }

  function remember(value) {
    try { localStorage.setItem(storageKey, value); } catch (_) {}
  }

  function preferred() {
    var list = navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language];
    for (var i = 0; i < list.length; i++) {
      var code = (list[i] || "").toLowerCase().split("-")[0];
      if (known(code) && code !== "auto") return code;
    }
    return "en";
  }

  var stored = saved();
  var defaultChoice = pageCode === "bn" || pageCode === "fr" || pageCode === "es" || pageCode === "ar" || pageCode === "zh" ? pageCode : "auto";
  var choice = known(query) ? query : pageCode !== "en" ? pageCode : known(stored) ? stored : defaultChoice;
  // 1.1.6 (Adeeb): the language is remembered only when the visitor picks it in the picker - not from a link's ?lang=
  // or a translated page they happened to land on. A remembered pick then opens the site in that language.
  var code = choice === "auto" ? preferred() : choice;

  function t(key) {
    var table = messages[code];
    return table && Object.prototype.hasOwnProperty.call(table, key) ? table[key] : key;
  }

  function format(key, values) {
    return t(key).replace(/\{(\w+)\}/g, function (token, name) {
      return Object.prototype.hasOwnProperty.call(values, name) ? values[name] : token;
    });
  }

  function followLanguageRoute() {
    if (!languageRoute || !/^https?:$/.test(location.protocol)) return false;
    var nextLanguage = routeLanguages.indexOf(code) >= 0 ? code : "en";
    if (nextLanguage === pageCode) return false;
    var url = new URL(location.href);
    url.pathname = (nextLanguage === "en" ? "" : "/" + nextLanguage) + languageRoute;
    location.replace(url.href);
    return true;
  }

  if (followLanguageRoute()) return;

  function apply() {
    if (followLanguageRoute()) return;
    root.dir = code === "ar" ? "rtl" : "ltr";
    root.lang = pageCode === "bn" || pageCode === "fr" || pageCode === "es" || pageCode === "ar" || pageCode === "zh" ? pageLanguage : tags[code] || code;
    root.dataset.locale = code;
    document.querySelectorAll("[data-blog-link]").forEach(function (link) {
      link.href = code === "bn" || code === "fr" || code === "es" || code === "ar" || code === "zh" ? "/" + code + "/blog/" : "/blog/";
    });
    document.querySelectorAll("[data-i18n]").forEach(function (node) {
      var key = node.dataset.i18n;
      var text = t(key);
      // pages that draw their own arrow (class arr / arr-ext) drop the typed one
      if (node.classList.contains("arr") || node.classList.contains("arr-ext")) text = text.replace(/\s*[›↗]\s*$/, "");
      node.textContent = text;
      node.lang = t(key) === key ? "en" : root.lang;
      node.dir = "auto";
    });
    document.querySelectorAll("[data-i18n-title]").forEach(function (node) {
      node.title = t(node.dataset.i18nTitle);
    });
    document.querySelectorAll("[data-i18n-label]").forEach(function (node) {
      node.setAttribute("aria-label", t(node.dataset.i18nLabel));
    });
    document.querySelectorAll("[data-i18n-preview]").forEach(function (node) {
      node.hidden = code === "en" || code === "bn" || code === "fr" || code === "ar" || code === "es" || code === "zh";
    });
    document.querySelectorAll("[data-i18n-direction]").forEach(function (node) {
      node.dir = code === "ar" ? "rtl" : "ltr";
    });
    document.querySelectorAll("[data-language-picker]").forEach(function (select) {
      if (!select.options.length) return;
      select.options[0].textContent = languages[preferred()] + " · " + t("Automatic");
      select.setAttribute("aria-label", t("Language"));
      select.title = t("Language");
      select.value = choice;
      select.hidden = !!select._syncPremiumPicker;
      var face = select.parentNode.querySelector(".lang-face b");
      if (face) face.textContent = languages[code] || code.toUpperCase();
      var badge = select.parentNode.querySelector(".lang-face");
      if (badge && !select.parentNode.querySelector('img[src*="canada"]')) {
        var flag = document.createElement("img");
        flag.src = "/img/canada.svg"; flag.width = 20; flag.height = 14;
        flag.alt = "Canada"; flag.title = "Canada";
        var globe = badge.querySelector("svg");
        if (globe) globe.replaceWith(flag); else badge.insertBefore(flag, badge.firstChild);
      }
      if (select._syncPremiumPicker) select._syncPremiumPicker();
    });
  }

  function change(value) {
    if (!known(value)) return;
    remember(value);
    var routed = /^\/(bn|fr|es|ar|zh)\//.exec(location.pathname);
    if (routed && value !== routed[1]) {
      var destination = new URL(location.href);
      destination.pathname = destination.pathname.replace(/^\/(bn|fr|es|ar|zh)\//, "/");
      destination.searchParams.set("lang", value);
      location.assign(destination.href);
      return;
    }
    var url = new URL(location.href);
    if (url.searchParams.has("lang")) {
      url.searchParams.delete("lang");
      history.replaceState(history.state, "", url);
      query = null;
    }
    choice = value;
    code = choice === "auto" ? preferred() : choice;
    apply();
    document.dispatchEvent(new Event("adeebometerlanguagechange"));
  }

  root.lang = pageCode === "bn" || pageCode === "fr" || pageCode === "es" || pageCode === "ar" || pageCode === "zh" ? pageLanguage : tags[code] || code;
  var mounted = new WeakSet();
  // 1.1.6 (Adeeb): every page has the language picker at the top, so a wrong pick is one click from undone. Pages whose
  // header doesn't carry one get it here: inside their top bar when they have one, otherwise pinned to the top corner.
  function ensurePicker() {
    if (document.querySelector("[data-language-picker]")) return;
    var host = document.querySelector("header nav, nav.top, .sitebar .sb-links, nav.links, header .w, header.top");
    var box = document.createElement("div");
    box.className = "site-language am-lang" + (host ? "" : " am-lang-float");
    box.innerHTML = '<span class="lang-face" aria-hidden="true"><img src="/img/canada.svg" width="20" height="14" alt="Canada" title="Canada"><b>English</b></span><select data-language-picker aria-label="Language"></select>';
    if (!document.getElementById("am-lang-style")) {
      var st = document.createElement("style"); st.id = "am-lang-style";
      st.textContent = ".am-lang{position:relative;display:inline-flex;align-items:center;margin-inline-start:8px;vertical-align:middle}" +
        ".am-lang .lang-face{display:inline-flex;align-items:center;gap:6px;height:32px;padding:0 10px;border:1px solid rgba(127,127,127,.45);border-radius:9px;font:600 14px/1 system-ui,sans-serif;color:inherit;pointer-events:none}" +
        ".am-lang .lang-face svg{width:18px;height:18px}" +
        ".am-lang select{position:absolute;inset:0;width:100%;height:100%;opacity:0;cursor:pointer;font-size:16px}" +
        ".am-lang select:focus-visible+.lang-face,.am-lang:focus-within .lang-face{outline:2px solid #1155E0;outline-offset:2px}" +
        ".am-lang-float{position:fixed;top:10px;inset-inline-end:12px;z-index:60;background:Canvas;color:CanvasText;border-radius:10px;box-shadow:0 2px 8px rgba(0,0,0,.18)}";
      document.head.appendChild(st);
    }
    if (host) host.appendChild(box); else document.body.insertBefore(box, document.body.firstChild);
  }
  // A single styled, keyboard-operable menu; the select remains the routing source of truth.
  var pickerSequence = 0;
  function enhancePicker(select) {
    var host = select.parentNode;
    if (!document.getElementById('am-picker-style')) {
      var style = document.createElement('style'); style.id = 'am-picker-style';
      style.textContent = '.site-language.am-picker{position:relative;display:inline-flex;align-items:center}' +
        '.am-picker>select[data-language-picker]{display:none!important}.am-picker>.lang-face{display:none!important}' +
        '.am-picker .am-picker-trigger{display:inline-flex;align-items:center;gap:8px;min-height:36px;padding:7px 11px;border:1px solid var(--line,#373c45);border-radius:10px;background:var(--card,#181b20);color:var(--ink,#eef1f5);font:600 14px/1.2 Barlow,system-ui,sans-serif;cursor:pointer;white-space:nowrap}' +
        '.am-picker .am-picker-trigger:hover,.am-picker .am-picker-trigger[aria-expanded=true]{border-color:var(--blue,#3b7bf5)}' +
        '.am-picker-trigger:focus-visible{outline:2px solid var(--blue,#3b7bf5);outline-offset:3px}' +
        '.am-picker-trigger .lang-face{display:inline-flex!important;gap:7px;align-items:center;padding:0!important;border:0!important;height:auto!important;font:inherit!important;pointer-events:none}' +
        '.am-picker-trigger .lang-face svg{width:18px;height:18px}.am-picker-trigger img{width:20px;height:14px}.am-picker-chevron{width:12px;height:12px;opacity:.7}' +
        '.am-picker-menu{position:fixed;z-index:10000;width:248px;max-width:calc(100vw - 24px);padding:7px;background:var(--card,#181b20);color:var(--ink,#eef1f5);border:1px solid var(--line,#373c45);border-radius:14px;overflow:auto;overscroll-behavior:contain;font:500 16px/1.35 Barlow,system-ui,sans-serif;text-align:start}' +
        '.am-picker-menu[hidden]{display:none!important}.am-picker-menu button{display:flex;align-items:center;justify-content:space-between;gap:16px;width:100%;min-height:44px;padding:10px 12px;border:0;border-radius:8px;background:transparent;color:inherit;font:inherit;text-align:start;cursor:pointer}' +
        '.am-picker-menu button:hover,.am-picker-menu button:focus-visible{background:var(--soft,#252a31);outline:0}.am-picker-menu button[aria-checked=true]{color:var(--blue,#3b7bf5);background:color-mix(in srgb,var(--blue,#3b7bf5) 12%,transparent)}' +
        '.am-picker-menu button:first-child{border-bottom:1px solid var(--line,#373c45);border-radius:8px 8px 0 0;margin-bottom:5px;padding-bottom:12px}.am-picker-menu small{display:block;color:var(--sub,#a9b1bd);font-size:13px;margin-top:2px}.am-picker-check{width:17px;height:17px;flex-shrink:0}';
      document.head.appendChild(style);
    }
    host.classList.add('am-picker');
    var trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'am-picker-trigger';
    var face = host.querySelector('.lang-face');
    if (face) { var clone = face.cloneNode(true); clone.removeAttribute('aria-hidden'); trigger.appendChild(clone); }
    else { var span = document.createElement('span'); span.className='lang-face'; span.appendChild(document.createElement('b')); trigger.appendChild(span); }
    var chevron = document.createElementNS('http://www.w3.org/2000/svg','svg'); chevron.setAttribute('class','am-picker-chevron'); chevron.setAttribute('viewBox','0 0 12 12'); chevron.setAttribute('aria-hidden','true');
    var path = document.createElementNS('http://www.w3.org/2000/svg','path'); path.setAttribute('d','m3 4.5 3 3 3-3');path.setAttribute('fill','none');path.setAttribute('stroke','currentColor');path.setAttribute('stroke-width','1.5');chevron.appendChild(path);trigger.appendChild(chevron);
    var menu=document.createElement('div');menu.id='am-language-menu-'+(++pickerSequence);menu.className='am-picker-menu';menu.setAttribute('role','menu');menu.hidden=true;
    trigger.setAttribute('aria-haspopup','menu');trigger.setAttribute('aria-expanded','false');trigger.setAttribute('aria-controls',menu.id);
    host.insertBefore(trigger,select);document.body.appendChild(menu);select.hidden=true;select.tabIndex=-1;select.setAttribute('aria-hidden','true');
    function buttons(){return Array.from(menu.querySelectorAll('button'));}
    function close(focus){menu.hidden=true;trigger.setAttribute('aria-expanded','false');if(focus)trigger.focus();}
    function sync(){
      select.hidden=true;
      trigger.setAttribute('aria-label',t('Language')+': '+(languages[code]||code));
      trigger.querySelector('b').textContent=languages[code]||code;
      menu.setAttribute('aria-label',t('Language'));menu.replaceChildren();
      Array.from(select.options).forEach(function(option){
        var item=document.createElement('button');item.type='button';item.setAttribute('role','menuitemradio');item.setAttribute('aria-checked',String(option.value===choice));item.tabIndex=-1;
        var label=document.createElement('span');label.textContent=option.value==='auto'?t('Automatic'):option.textContent;
        if(option.value==='auto'){var detail=document.createElement('small');detail.textContent=languages[preferred()];label.appendChild(detail);}else{label.lang=tags[option.value]||option.value;label.dir=option.value==='ar'?'rtl':'ltr';}
        var check=document.createElementNS('http://www.w3.org/2000/svg','svg');check.setAttribute('class','am-picker-check');check.setAttribute('viewBox','0 0 20 20');check.setAttribute('aria-hidden','true');
        if(option.value===choice){var tick=document.createElementNS('http://www.w3.org/2000/svg','path');tick.setAttribute('d','m4 10 4 4 8-8');tick.setAttribute('fill','none');tick.setAttribute('stroke','currentColor');tick.setAttribute('stroke-width','2');check.appendChild(tick);}
        item.append(label,check);item.addEventListener('click',function(){close(true);select.value=option.value;select.dispatchEvent(new Event('change',{bubbles:true}));});menu.appendChild(item);
      });
    }
    function open(last){
      document.dispatchEvent(new CustomEvent('am-close-language-menu'));
      sync();menu.hidden=false;trigger.setAttribute('aria-expanded','true');
      var rect=trigger.getBoundingClientRect(),width=menu.getBoundingClientRect().width;
      menu.style.left=Math.max(12,Math.min(innerWidth-width-12,document.documentElement.dir==='rtl'?rect.left:rect.right-width))+'px';
      menu.style.top=(rect.bottom+8)+'px';menu.style.maxHeight=Math.max(100,innerHeight-rect.bottom-20)+'px';
      var items=buttons();(last?items[items.length-1]:menu.querySelector('[aria-checked=true]')||items[0]).focus();
    }
    trigger.addEventListener('click',function(){if(menu.hidden)open(false);else close(false);});
    trigger.addEventListener('keydown',function(e){if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();open(e.key==='ArrowUp');}});
    menu.addEventListener('keydown',function(e){var items=buttons(),i=items.indexOf(document.activeElement);if(e.key==='Escape'){e.preventDefault();close(true);}else if(e.key==='Tab')close(true);else if(['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();items[e.key==='Home'?0:e.key==='End'?items.length-1:(i+(e.key==='ArrowDown'?1:items.length-1))%items.length].focus();}});
    document.addEventListener('click',function(e){if(!host.contains(e.target)&&!menu.contains(e.target))close(false);});
    document.addEventListener('am-close-language-menu',function(){close(false);});
    window.addEventListener('resize',function(){close(false);});window.addEventListener('scroll',function(){close(false);});
    select._syncPremiumPicker=sync;sync();
  }

  function mountPickers() {
    ensurePicker();
    document.querySelectorAll("[data-language-picker]").forEach(function (select) {
      if (mounted.has(select)) return;
      mounted.add(select);
      select.add(new Option("Automatic", "auto"));
      Object.keys(languages).forEach(function (id) {
        var option = new Option(languages[id], id);
        option.lang = tags[id] || id;
        option.dir = id === "ar" ? "rtl" : "ltr";
        select.add(option);
      });
      select.addEventListener("change", function () { change(select.value); });
      enhancePicker(select);
    });
    apply();
  }

  window.AdeeboMeterLocale = { t: t, format: format, code: function () { return code; }, mountPickers: mountPickers };
  // Retain the displayed language even when a translated route was opened
  // directly, without changing the visitor's saved language preference.
  document.addEventListener("click", function (event) {
    var link = event.target.closest && event.target.closest("a[href]");
    if (!link || event.defaultPrevented || event.button !== 0 || code === "en") return;
    var href = link.getAttribute("href");
    if (!href || href.charAt(0) === "#" || link.hasAttribute("download")) return;
    var target = new URL(href, location.href);
    if (target.origin !== location.origin || target.searchParams.has("lang") || /^\/(bn|fr|es|ar|zh)\//.test(target.pathname)) return;
    if (!/\/$|\.html$/.test(target.pathname)) return;
    target.searchParams.set("lang", code);
    link.href = target.href;
  }, true);
  document.addEventListener("DOMContentLoaded", function () {
    mountPickers();
    root.classList.add("locale-ready");
  });

  window.addEventListener("storage", function (event) {
    if (event.key !== storageKey || known(query)) return;
    choice = known(event.newValue) ? event.newValue : defaultChoice;
    code = choice === "auto" ? preferred() : choice;
    apply();
    document.dispatchEvent(new Event("adeebometerlanguagechange"));
  });

  window.addEventListener("languagechange", function () {
    if (choice === "auto" && preferred() !== code) {
      code = preferred();
      apply();
      document.dispatchEvent(new Event("adeebometerlanguagechange"));
    }
  });
}());

// Public-page visitor analytics. This loader only adds analytics.js on pages with a canonical link;
// analytics.js itself checks the hostname, privacy signals and companion-tool exclusions before
// contacting the analytics provider.
(function () {
  if (!document.querySelector('link[rel="canonical"]')) return;
  var analytics = document.createElement("script");
  analytics.src = "/analytics.js";
  analytics.async = true;
  document.head.appendChild(analytics);
}());

