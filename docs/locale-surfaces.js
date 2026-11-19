(function () {
  "use strict";
  // Live results and lesson progress are rendered after the initial page loads.
  var originals = new WeakMap();
  var attributes = new WeakMap();
  var watched = ["placeholder", "aria-label"];
  var root;
  var originalTitle;
  var observer;
  function translate(value) {
    var key = value.trim();
    if (!key) return value;
    var locale = window.AdeeboMeterLocale;
    if (!locale) return value;
    var result = locale.t(key);
    // Generated lesson links add arrows and page-title suffixes to a shared title.
    if (result === key) {
      var title = /^(← )?(.+?)( →| \| Learn \| AdeeboMeter)?$/.exec(key);
      if (title && (title[1] || title[3])) {
        var translated = locale.t(title[2]);
        var suffix = title[3] === " | Learn | AdeeboMeter"
          ? " | " + locale.t("Learn") + " | AdeeboMeter" : (title[3] || "");
        result = (title[1] || "") + translated + suffix;
      }
    }
    return result === key ? value : value.replace(key, function () { return result; });
  }
  function update() {
    observer.disconnect();
    document.title = translate(originalTitle);
    var nodes = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
      acceptNode: function (node) {
        var parent = node.parentElement;
        return !parent || parent.closest("script, style, code, pre, textarea, [contenteditable], [data-i18n]")
          ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT;
      }
    });
    var node;
    while ((node = nodes.nextNode())) {
      var before = originals.get(node);
      var source = before && node.data === before.rendered ? before.source : node.data;
      var rendered = translate(source);
      if (rendered !== source) originals.set(node, { source: source, rendered: rendered });
      else originals.delete(node);
      if (node.data !== rendered) node.data = rendered;
    }
    root.querySelectorAll("[placeholder], [aria-label]").forEach(function (el) {
      var map = attributes.get(el) || {};
      watched.forEach(function (key) {
        var value = el.getAttribute(key);
        if (value === null) return;
        var item = map[key];
        var source = item && value === item.rendered ? item.source : value;
        var rendered = translate(source);
        map[key] = { source: source, rendered: rendered };
        if (rendered !== value) el.setAttribute(key, rendered);
      });
      attributes.set(el, map);
    });
    root.querySelectorAll(".ui-row[data-s]").forEach(function (link) {
      if (!link.dataset.searchBase) link.dataset.searchBase = link.dataset.s;
      link.dataset.s = link.dataset.searchBase + " " + link.textContent.trim().toLowerCase();
    });
    observer.observe(root, { childList: true, characterData: true, subtree: true });
  }
  document.addEventListener("DOMContentLoaded", function () {
    root = document.body;
    originalTitle = document.title;
    observer = new MutationObserver(update);
    update();
    document.addEventListener("adeebometerlanguagechange", update);
  });
}());
