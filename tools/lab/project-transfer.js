/* Project snapshots live in the URL fragment, never in a server request. */
(function (root) {
  "use strict";
  const LIMIT = 2000000, LINK_LIMIT = 12000;
  function base64(bytes) {
    let text = "";
    for (let i = 0; i < bytes.length; i += 8192) text += String.fromCharCode(...bytes.subarray(i, i + 8192));
    return btoa(text).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  }
  async function bounded(stream) {
    const reader = stream.getReader(), parts = []; let total = 0;
    try {
      while (true) {
        const next = await reader.read(); if (next.done) break;
        total += next.value.length;
        if (total > LIMIT) { await reader.cancel(); throw new Error("Project is too large. Use a project file."); }
        parts.push(next.value);
      }
    } finally { reader.releaseLock(); }
    const all = new Uint8Array(total); let at = 0;
    for (const part of parts) { all.set(part, at); at += part.length; }
    return all;
  }
  async function encode(project) {
    const copy = JSON.parse(JSON.stringify(project)); delete copy.fid;
    let bytes = new TextEncoder().encode(JSON.stringify(copy)), kind = "j";
    if (bytes.length > LIMIT) throw new Error("Project is too large. Use a project file.");
    if (typeof CompressionStream !== "undefined") {
      bytes = await bounded(new Blob([bytes]).stream().pipeThrough(new CompressionStream("gzip"))); kind = "g";
    }
    const token = kind + "." + base64(bytes);
    if (token.length > LINK_LIMIT) throw new Error("This project is too detailed for a link. Save a project file instead.");
    return token;
  }
  async function decode(token) {
    if (typeof token !== "string" || token.length > LINK_LIMIT || !/^[jg]\.[A-Za-z0-9_-]+$/.test(token)) throw new Error("This project link is invalid.");
    const encoded = token.slice(2).replace(/-/g, "+").replace(/_/g, "/");
    let bytes = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
    if (token[0] === "g") {
      if (typeof DecompressionStream === "undefined") throw new Error("This browser cannot open compressed project links. Import the project file instead.");
      bytes = await bounded(new Blob([bytes]).stream().pipeThrough(new DecompressionStream("gzip")));
    }
    if (bytes.length > LIMIT) throw new Error("Project is too large.");
    return JSON.parse(new TextDecoder("utf-8", {fatal: true}).decode(bytes));
  }
  root.AMLabTransfer = {encode, decode};
})(typeof window !== "undefined" ? window : globalThis);
