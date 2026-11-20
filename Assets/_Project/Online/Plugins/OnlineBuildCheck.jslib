// A page left open from before a deploy must not join the class room with an old build (20 Nov 2026, presentation
// safety). index.html checks version.json once while the game loads; this checks again when the player opens the
// online lobby, so a tab opened yesterday (or before the last deploy) loads the newest build first.
// This build's id = the ?v=<id> that Role4WebBuild puts on every TemplateData address of index.html.
// C# side: Online/Race/OnlineBuildCheck.cs (polls; nothing here calls into C#).
mergeInto(LibraryManager.library, {
  OnlineBuildCheckStart: function () {
    var state = { newer: "" };
    window.ccOnlineBuild = state;
    try {
      if (!window.fetch || location.protocol === "file:") {
        return;
      }
      var mine = "";
      var links = document.querySelectorAll("link[href*='TemplateData/'], script[src*='TemplateData/']");
      for (var i = 0; i < links.length && !mine; i++) {
        var address = links[i].getAttribute("href") || links[i].getAttribute("src") || "";
        var match = /[?&]v=([A-Za-z0-9]+)/.exec(address);
        if (match) {
          mine = match[1];
        }
      }
      if (!mine) {
        return; // a build without ids (local test build): nothing to compare
      }
      fetch("version.json?t=" + Date.now(), { cache: "no-store" }).then(function (response) {
        return response.ok ? response.json() : null;
      }).then(function (version) {
        var newest = version && version.build ? String(version.build) : "";
        var tried = new URLSearchParams(location.search).get("cc-build");
        if (/^[A-Za-z0-9]{4,40}$/.test(newest) && newest !== mine && tried !== newest) {
          state.newer = newest; // never twice for the same build: no reload loop if the host still serves the old one
        }
      }).catch(function () { });
    } catch (e) { }
  },

  // "" = nothing newer (yet), else the newest build id.
  OnlineBuildCheckNewer: function () {
    var s = window.ccOnlineBuild;
    var text = s && s.newer ? s.newer : "";
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  // Reload with ?cc-build=<id> (a new address = a fresh index.html) and room/name, so the player is back in the
  // lobby by itself. Empty room or name = leave that flag as it is.
  OnlineBuildReload: function (buildPointer, roomPointer, namePointer) {
    try {
      var params = new URLSearchParams(location.search);
      params.set("cc-build", UTF8ToString(buildPointer));
      var room = UTF8ToString(roomPointer);
      var name = UTF8ToString(namePointer);
      if (room) { params.set("room", room); }
      if (name) { params.set("name", name); }
      location.replace(location.pathname + "?" + params.toString() + location.hash);
    } catch (e) {
      location.reload();
    }
  }
});
