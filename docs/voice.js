// AdeeboMeter's companion on the website and the phone: the same voices as the app (knowledge/lines-*.txt, as kb/voice/*.json).
// Only the words change with a voice - numbers, facts and lessons stay exactly as they are.
// The choice is kept in this browser only (one setting, "am-voice"); nothing is sent anywhere.
// On the phone remote the PC's own voice is used until you pick one here (AMV.follow).
(function () {
  "use strict";
  var KEY = "am-voice", base = (document.currentScript && document.currentScript.src || "/voice.js").replace(/voice\.js.*$/, "");
  var mine = null, pc = null, packs = {}, list = null, fns = [], last = {};
  try { mine = localStorage.getItem(KEY); } catch (e) {}
  function get(u) { return fetch(base + u).then(function (r) { if (!r.ok) throw 0; return r.json(); }); }
  function index() { return list ? Promise.resolve(list) : get("kb/voice/index.json").then(function (j) { list = j; return j; }); }
  // Surprise me: one persona a day, the same pick as the app makes on that day
  function today() {
    if (!list || !list.personas.length) return "chill";
    var d = new Date(), y = d.getFullYear(), doy = Math.floor((Date.UTC(y, d.getMonth(), d.getDate()) - Date.UTC(y, 0, 0)) / 864e5);
    return list.personas[(Math.imul(y * 400 + doy, 2654435761 | 0) >>> 0) % list.personas.length][0];
  }
  function who() { var w = mine || pc || "surprise"; return w === "surprise" ? today() : w; }
  function chain() { var w = who(), c = [w, "chill", "plain"]; return c.filter(function (x, i) { return c.indexOf(x) === i; }); }
  function addr(w) { return { "epic-fantasy": "friend", shakespeare: "friend", "dark-ages": "friend", bro: "bro", pirate: "matey", gamer: "player", noir: "pal", "space-saga": "pilot", mission: "flight", bestie: "bestie", genz: "bestie" }[w] || "boss"; }
  function cap(s) { return s ? s.charAt(0).toUpperCase() + s.slice(1) : s; }
  function load() {
    return index().then(function () { return Promise.all(chain().map(function (id) { return packs[id] ? 0 : get("kb/voice/" + id + ".json").then(function (p) { packs[id] = p; }).catch(function () { packs[id] = {}; }); })); });
  }
  function fire() { fns.forEach(function (f) { try { f(); } catch (e) {} }); }
  var AMV = {
    ready: null,
    // a line for this moment ("" until the voice has loaded, or when no voice has one) - v fills {v}
    line: function (key, v) {
      var c = chain();
      for (var i = 0; i < c.length; i++) {
        var l = (packs[c[i]] || {})[key]; if (!l) continue;
        var ok = l.filter(function (t) { return v != null || t.indexOf("{v}") < 0; }); if (!ok.length) continue;
        var fresh = ok.filter(function (t) { return t !== last[key]; }), t = (fresh.length ? fresh : ok)[Math.floor(Math.random() * (fresh.length ? fresh : ok).length)]; last[key] = t;
        var a = addr(c[i]);
        return cap(t.replace(/\{(addr|Addr|v)\}/g, function (m, g) { return g === "v" ? String(v) : g === "Addr" ? cap(a) : a; }));
      }
      return "";
    },
    who: who,
    set: function (id) { mine = id || null; try { if (mine) localStorage.setItem(KEY, mine); else localStorage.removeItem(KEY); } catch (e) {} load().then(fire); },
    follow: function (id) { if (!id || id === pc) return; pc = id; load().then(fire); },   // the PC's voice, unless you picked one here
    onchange: function (f) { fns.push(f); },
    // a small "Companion" picker; put it anywhere
    picker: function (el) {
      index().then(function (j) {
        var cur = mine || "", h = '<label style="font-size:14px;color:var(--sub,#4A5568)">Companion <select id="amv-pick" style="font:inherit;font-size:14px;color:inherit;background:transparent;border:1px solid var(--line,#E3E8EF);border-radius:8px;padding:4px 6px;max-width:220px">' +
          '<option value="">' + (pc ? "Same as my PC" : "Surprise me (a new one each day)") + '</option>' + (pc ? '<option value="surprise">Surprise me</option>' : "") +
          '<optgroup label="Moods">' + j.moods.map(function (m) { return '<option value="' + m[0] + '">' + m[1] + (m[0] === "plain" ? " (just the facts)" : "") + '</option>'; }).join("") + '</optgroup>' +
          '<optgroup label="Personas">' + j.personas.map(function (p) { return '<option value="' + p[0] + '">' + p[1] + '</option>'; }).join("") + '</optgroup></select></label>';
        el.innerHTML = h; var s = el.querySelector("select"); s.value = cur; s.onchange = function () { AMV.set(s.value); };
        AMV.onchange(function () { if (s.value !== (mine || "")) s.value = mine || ""; s.options[0].textContent = pc ? "Same as my PC" : "Surprise me (a new one each day)"; });
      }).catch(function () {});
    }
  };
  AMV.ready = load().then(function () { fire(); }).catch(function () {});
  window.AMV = AMV;
})();
