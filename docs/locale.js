(function () {
  "use strict";

  var languages = {
    en: "English", fr: "Français", es: "Español", ar: "العربية",
    bn: "বাংলা"
  };   // R2-201: Chinese (73 strings) isn't offered until it's translated - as in the app; a saved "zh" reads as English
  var tags = { fr: "fr-CA", zh: "zh-Hans" };
  var storageKey = "adeebometer-language";
  var root = document.documentElement;
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
  if (known(query)) remember(query);
  else if (pageCode !== "en") remember(pageCode);
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
      node.textContent = t(key);
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
  function mountPickers() {
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

