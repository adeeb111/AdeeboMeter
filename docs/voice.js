// AdeeboMeter's companion on the website and the phone: the same voices as the app (knowledge/lines-*.txt, as kb/voice/*.json),
// the same choices (voice, how chatty, what it calls you) and a face. Only the words change with a voice - numbers, facts and
// lessons stay exactly as they are. The choices are kept in this browser only (am-voice*); nothing is sent anywhere.
// On the phone remote the PC's own voice is used until you pick one here (AMV.follow).
(function () {
  "use strict";
  var base = (document.currentScript && document.currentScript.src || "/voice.js").replace(/voice\.js.*$/, "");
  function rd(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
  function wr(k, v) { try { if (v) localStorage.setItem(k, v); else localStorage.removeItem(k); } catch (e) {} }
  var mine = rd("am-voice"), chat = rd("am-voice-chat") || "normal", call = rd("am-voice-addr") || "neutral", name = rd("am-voice-name") || "";
  var pc = null, packs = {}, list = null, fns = [], last = {}, sticky = {};
  function get(u) { return fetch(base + u).then(function (r) { if (!r.ok) throw 0; return r.json(); }); }
  function index() { return list ? Promise.resolve(list) : get("kb/voice/index.json").then(function (j) { list = j; return j; }); }
  // Surprise me: one persona a day - the same pick the app makes that day
  function today() {
    if (!list || !list.personas.length) return "chill";
    var d = new Date(), y = d.getFullYear(), doy = Math.floor((Date.UTC(y, d.getMonth(), d.getDate()) - Date.UTC(y, 0, 0)) / 864e5);
    return list.personas[(Math.imul(y * 400 + doy, 2654435761 | 0) >>> 0) % list.personas.length][0];
  }
  function who() { var w = mine || pc || "surprise"; return w === "surprise" ? today() : w; }
  function nameOf(id) { if (!list) return id; var all = list.moods.concat(list.personas); for (var i = 0; i < all.length; i++) if (all[i][0] === id) return all[i][1]; return id; }
  function chain() { var w = who(), c = [w, "chill", "plain"]; return c.filter(function (x, i) { return c.indexOf(x) === i; }); }
  // what it calls you, in the voice's own words (the app's rules); a name stays in this browser
  function addr(w) {
    if (call === "name" && name.trim()) return name.trim();
    var epic = w === "epic-fantasy" || w === "shakespeare" || w === "dark-ages";
    if (call === "sir") return epic ? "my lord" : w === "bro" || w === "genz" ? "bro" : w === "pirate" ? "captain" : "sir";
    if (call === "maam") return epic ? "my lady" : w === "bro" ? "sis" : w === "genz" ? "bestie" : w === "pirate" ? "captain" : "ma'am";
    return { "epic-fantasy": "friend", shakespeare: "friend", "dark-ages": "friend", bro: "bro", pirate: "matey", gamer: "player", noir: "pal", "space-saga": "pilot", mission: "flight", bestie: "bestie", genz: "bestie" }[w] || "boss";
  }
  function cap(s) { return s ? s.charAt(0).toUpperCase() + s.slice(1) : s; }
  // how chatty, like the app: face only / quiet (greetings and problems) / normal / chatty (+ idle chatter)
  var KIND = { "app.open": "greet", morning: "greet", "late.night": "greet", "back.again": "greet", "storm.near": "problem", "game.lag": "problem", "trade.slow": "problem", "net.none": "problem", "inet.slow": "problem", "gw.down": "problem", "isp.down": "problem", idle: "idle" };
  function speaks(key) { var k = KIND[key] || "state"; return chat === "face" ? false : chat === "quiet" ? k === "problem" || k === "greet" : chat === "chatty" ? true : k !== "idle"; }
  // a greeting or a state line stays for 5 minutes (a line that changes every time you look is noise); reactions are fresh
  var STICK = { "app.open": 1, morning: 1, "late.night": 1, "all.good": 1, idle: 1, "market.open": 1, "trade.closed": 1, "storm.near": 1 };
  function load() { return index().then(function () { return Promise.all(chain().map(function (id) { return packs[id] ? 0 : get("kb/voice/" + id + ".json").then(function (p) { packs[id] = p; }).catch(function () {}); })); }); }   // a failed pack isn't kept as empty: it's tried again (AM-CG-381)
  if (typeof addEventListener === "function") addEventListener("online", function () { load().then(fire, function () {}); });
  function fire() { sticky = {}; fns.forEach(function (f) { try { f(); } catch (e) {} }); }
  function save() { wr("am-voice", mine); wr("am-voice-chat", chat === "normal" ? "" : chat); wr("am-voice-addr", call === "neutral" ? "" : call); wr("am-voice-name", call === "name" ? name : ""); }

  // ---- the face: one small drawing, its look set by the moment (calm, happy, worried, sleepy, party, thinking)
  function faceSvg(look, size) {
    var s = size || 44, eyes, mouth, extra = "", brow = "";
    switch (look) {
      case "happy": eyes = '<path d="M21 28q4-5 8 0M35 28q4-5 8 0" stroke="#fff" stroke-width="3.2" fill="none" stroke-linecap="round"/>'; mouth = '<path d="M22 38q10 10 20 0" stroke="#fff" stroke-width="3.4" fill="none" stroke-linecap="round"/>'; break;
      case "worried": eyes = '<circle cx="25" cy="29" r="3.2" fill="#fff"/><circle cx="39" cy="29" r="3.2" fill="#fff"/>'; brow = '<path d="M20 22l8 3M44 22l-8 3" stroke="#fff" stroke-width="2.6" stroke-linecap="round"/>'; mouth = '<path d="M24 44q8-6 16 0" stroke="#fff" stroke-width="3.2" fill="none" stroke-linecap="round"/>'; break;
      case "sleepy": eyes = '<path d="M21 30h8M35 30h8" stroke="#fff" stroke-width="3" stroke-linecap="round"/>'; mouth = '<circle cx="32" cy="41" r="3" fill="none" stroke="#fff" stroke-width="2.6"/>'; extra = '<text x="47" y="16" font-size="11" font-weight="700" fill="#F2CC60" font-family="system-ui">z</text>'; break;
      case "party": eyes = '<path d="M21 28q4-5 8 0M35 28q4-5 8 0" stroke="#fff" stroke-width="3.2" fill="none" stroke-linecap="round"/>'; mouth = '<path d="M21 37h22q-2 11-11 11t-11-11z" fill="#fff"/>'; extra = '<path d="M50 4l3 7M58 12l-6 3M9 8l4 6" stroke="#F2CC60" stroke-width="2.6" stroke-linecap="round"/>'; break;
      case "thinking": eyes = '<circle cx="25" cy="28" r="3.2" fill="#fff"/><circle cx="40" cy="26" r="3.2" fill="#fff"/>'; mouth = '<path d="M26 42h12" stroke="#fff" stroke-width="3.2" stroke-linecap="round"/>'; break;
      default: eyes = '<circle cx="25" cy="29" r="3.4" fill="#fff"/><circle cx="39" cy="29" r="3.4" fill="#fff"/>'; mouth = '<path d="M24 39q8 6 16 0" stroke="#fff" stroke-width="3.2" fill="none" stroke-linecap="round"/>';
    }
    return '<svg viewBox="0 0 64 64" width="' + s + '" height="' + s + '" aria-hidden="true" style="flex:none;display:block"><rect x="2" y="2" width="60" height="60" rx="18" fill="#1F6FEB"/><path d="M10 52A26 26 0 0 1 14 20" stroke="#3FB950" stroke-width="4" fill="none" opacity=".55" stroke-linecap="round"/><path d="M50 20A26 26 0 0 1 54 52" stroke="#FF7B72" stroke-width="4" fill="none" opacity=".45" stroke-linecap="round"/>' + brow + eyes + mouth + extra + '</svg>';
  }
  function lookNow() { var h = new Date().getHours(); return h >= 23 || h < 5 ? "sleepy" : "calm"; }

  var AMV = {
    ready: null,
    // a line for this moment ("" when this kind of moment is quiet, or until the voice has loaded) - v fills {v}
    line: function (key, v) {
      if (!speaks(key)) return "";
      // AM-CG-382: the same wording sticks for five minutes, but the numbers in it are always today's
      function fill(t, ci) { var a = addr(ci); return cap(t.replace(/\{(addr|Addr|v)\}/g, function (m, g) { return g === "v" ? String(v) : g === "Addr" ? cap(a) : a; })); }
      var st = STICK[key] && sticky[key]; if (st && Date.now() - st.at < 300000 && (v != null || st.t.indexOf("{v}") < 0)) return fill(st.t, st.c);
      var c = chain();
      for (var i = 0; i < c.length; i++) {
        var l = (packs[c[i]] || {})[key]; if (!l) continue;
        var ok = l.filter(function (t) { return v != null || t.indexOf("{v}") < 0; }); if (!ok.length) continue;
        var fresh = ok.filter(function (t) { return t !== last[key]; }), pool = fresh.length ? fresh : ok, t = pool[Math.floor(Math.random() * pool.length)]; last[key] = t;
        if (STICK[key]) sticky[key] = { at: Date.now(), t: t, c: c[i] };
        return fill(t, c[i]);
      }
      return "";
    },
    who: who,
    // today's companion (Surprise me), for the home page: [id, name]
    today: function () { return index().then(function () { var id = who(); return [id, nameOf(id), !!(mine && mine !== "surprise")]; }); },
    face: function (el, look) { if (el) el.innerHTML = faceSvg(look || lookNow(), +el.getAttribute("data-size") || 44); },
    // for the phone app's companion sheet: the list of voices, what's chosen now, and how much it talks
    voices: function () { return index(); },
    state: function () { return { mine: mine, chat: chat, call: call, pc: pc }; },
    talk: function (c) { if (["face", "quiet", "normal", "chatty"].indexOf(c) >= 0) { chat = c; save(); fire(); } },
    set: function (id) { mine = id || null; save(); load().then(fire); },
    follow: function (id) { if (!id || id === pc) return; pc = id; load().then(fire); },   // the PC's voice, unless you picked one here
    onchange: function (f) { fns.push(f); },
    // the choices, the same as the app's: voice, how chatty, what it calls you
    picker: function (el) {
      index().then(function (j) {
        var S = 'style="font:inherit;font-size:14px;color:inherit;background:transparent;border:1px solid var(--line,#E3E8EF);border-radius:8px;padding:4px 6px;max-width:210px"';
        el.innerHTML = '<div style="display:flex;flex-wrap:wrap;gap:8px 14px;align-items:center;font-size:14px;color:var(--sub,#4A5568)">' +
          '<label>Companion <select data-v ' + S + '><option value="">' + (pc ? "Same as my PC" : "Surprise me (a new one each day)") + '</option>' + (pc ? '<option value="surprise">Surprise me</option>' : "") +
          '<optgroup label="Moods">' + j.moods.map(function (m) { return '<option value="' + m[0] + '">' + m[1] + (m[0] === "plain" ? " (just the facts)" : "") + '</option>'; }).join("") + '</optgroup>' +
          '<optgroup label="Personas">' + j.personas.map(function (p) { return '<option value="' + p[0] + '">' + p[1] + '</option>'; }).join("") + '</optgroup></select></label>' +
          '<label>Talks <select data-c ' + S + '><option value="face">Face only</option><option value="quiet">Quiet</option><option value="normal">Normal</option><option value="chatty">Chatty</option></select></label>' +
          '<label>Calls me <select data-a ' + S + '><option value="neutral">Boss (neutral)</option><option value="sir">Sir</option><option value="maam">Ma\'am</option><option value="name">My name</option></select></label>' +
          '<input data-n placeholder="Your name" maxlength="24" ' + S.replace("max-width:210px", "width:130px") + '></div>';
        var sv = el.querySelector("[data-v]"), sc = el.querySelector("[data-c]"), sa = el.querySelector("[data-a]"), sn = el.querySelector("[data-n]");
        function sync() { sv.value = mine || ""; sc.value = chat; sa.value = call; sn.value = name; sn.style.display = call === "name" ? "" : "none"; sv.options[0].textContent = pc ? "Same as my PC" : "Surprise me (a new one each day)"; }
        sync();
        sv.onchange = function () { AMV.set(sv.value); };
        sc.onchange = function () { chat = sc.value; save(); fire(); };
        sa.onchange = function () { call = sa.value; sync(); save(); fire(); };
        sn.oninput = function () { name = sn.value.replace(/[<>{}]/g, "").slice(0, 24); save(); fire(); };
        AMV.onchange(sync);
      }).catch(function () {});
    }
  };
  AMV.ready = load().then(function () { fire(); }).catch(function () {});
  window.AMV = AMV;
})();
