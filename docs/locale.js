(function () {
  "use strict";

  var languages = {
    en: "English", fr: "Français", es: "Español", ar: "العربية",
    bn: "বাংলা"
  };   // R2-201: Chinese (73 strings) isn't offered until it's translated - as in the app; a saved "zh" reads as English
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
  var defaultChoice = pageCode === "bn" || pageCode === "fr" || pageCode === "es" || pageCode === "ar" ? pageCode : "auto";
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
    root.lang = pageCode === "bn" || pageCode === "fr" || pageCode === "es" || pageCode === "ar" ? pageLanguage : tags[code] || code;
    root.dataset.locale = code;
    document.querySelectorAll("[data-blog-link]").forEach(function (link) {
      link.href = code === "bn" || code === "fr" || code === "es" || code === "ar" ? "/" + code + "/blog/" : "/blog/";
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
      node.hidden = code === "en" || code === "bn" || code === "fr" || code === "ar" || code === "es";
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
      select.hidden = false;
      var face = select.parentNode.querySelector(".lang-face b");
      if (face) face.textContent = code.toUpperCase();
    });
  }

  function change(value) {
    if (!known(value)) return;
    remember(value);
    var routed = /^\/(bn|fr|es|ar)\//.exec(location.pathname);
    if (routed && value !== routed[1]) {
      var destination = new URL(location.href);
      destination.pathname = destination.pathname.replace(/^\/(bn|fr|es|ar)\//, "/");
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

  root.lang = pageCode === "bn" || pageCode === "fr" || pageCode === "es" || pageCode === "ar" ? pageLanguage : tags[code] || code;
  var mounted = new WeakSet();
  // 1.1.6 (Adeeb): every page has the language picker at the top, so a wrong pick is one click from undone. Pages whose
  // header doesn't carry one get it here: inside their top bar when they have one, otherwise pinned to the top corner.
  function ensurePicker() {
    if (document.querySelector("[data-language-picker]")) return;
    var host = document.querySelector("header nav, nav.top, .sitebar .sb-links, nav.links, header .w, header.top");
    var box = document.createElement("div");
    box.className = "site-language am-lang" + (host ? "" : " am-lang-float");
    box.innerHTML = '<span class="lang-face" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3"/></svg><b>EN</b></span><select data-language-picker aria-label="Language"></select>';
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
    });
    apply();
  }

  window.AdeeboMeterLocale = { t: t, format: format, code: function () { return code; }, mountPickers: mountPickers };
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

