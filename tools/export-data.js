// Copies the game/drill/quote data out of the website's JavaScript into data.json for the Windows app,
// so both always show the same drills, ranks, quotes, and project ideas.
// Usage: node tools/export-data.js <website folder> <output data.json>
const fs = require("fs"), path = require("path");
const web = process.argv[2], out = process.argv[3];
const localStorage = { getItem: () => null, setItem: () => {} };
const src = fs.readFileSync(path.join(web, "genres.js"), "utf8") + "\n" +
  fs.readFileSync(path.join(web, "content.js"), "utf8").replace(/var CONTENT[^\n]*\n/, "");
const X = new Function("localStorage", src + "; return {GENRES,GENRE_ORDER,GAMES,GAME_FOCUS,RANKS,DEFAULT_CONTENT,VERSE_TEXT,PROJECTS};")(localStorage);
fs.writeFileSync(out, JSON.stringify(X));
console.log("Wrote " + out + " (" + fs.statSync(out).size + " bytes)");
