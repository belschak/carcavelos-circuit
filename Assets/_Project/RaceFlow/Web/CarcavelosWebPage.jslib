// The game's two messages to the Web page (task R10/R11). C# side: RaceFlow/Web/WebPage.cs.
// Unity puts every .jslib into the Web build; these functions run in the browser, not in C#.
mergeInto(LibraryManager.library, {
  // state: 1 while a race runs (countdown or racing), 2 while that race is paused, 0 on the start screen and the
  // results (WebPage.PageRaceState). The page template listens for "cc-race-state" and shows its driving buttons
  // only during a running race.
  CarcavelosRaceState: function (state) {
    // state: 0 menus, 1 racing, 2 paused, plus 4 in an online race (WebPage.PageOnline).
    var race = state & 3;
    window.dispatchEvent(new CustomEvent("cc-race-state", { detail: { racing: race === 1, paused: race === 2, online: (state & 4) !== 0 } }));
  },

  // 1 while the page shows its touch buttons (phone or tablet, index.html useTouchMode adds "cc-touch-mode"), else 0.
  // The HUD then makes room for the thumbs (RaceHud.uss ".hud--touch") and the hints name buttons instead of keys.
  CarcavelosTouchMode: function () {
    return document.documentElement.classList.contains("cc-touch-mode") ? 1 : 0;
  },

  // Android name fix (23 Nov): on phones the name is typed in a real text box of the page, laid exactly over the
  // game's name field (C# side: RaceFlow/Web/PageNameBox.cs). A real finger tap on a real <input> opens the phone
  // keyboard in every phone browser; the game's own field and window.prompt did not on a Pixel 9 Pro (Chrome Android).
  // owner: which game field uses the box (one box for the whole page). left/top/width/height: the field's place as a
  // share (0 to 1) of the game canvas. current: the text to start with, used when reset is 1 (the box opens, or the
  // game changed the field itself) or the box changes owner.
  CarcavelosNameBoxShow: function (owner, left, top, width, height, currentPointer, maxLength, reset) {
    var box = window.ccNameBox;
    if (!box) {
      box = document.createElement("input");
      box.type = "text";
      box.id = "cc-name-box";
      box.setAttribute("autocomplete", "off");
      box.setAttribute("autocorrect", "off");
      box.setAttribute("autocapitalize", "characters");
      box.setAttribute("spellcheck", "false");
      box.setAttribute("enterkeyhint", "done");
      box.setAttribute("aria-label", "Your name");
      box.placeholder = "TAP TO TYPE";
      // Same look as the game's name field (Leaderboard.uss .lb-name-field): cream box, dark ink, orange edge.
      box.style.cssText = "position:fixed;z-index:2147483000;box-sizing:border-box;margin:0;padding:0 12px;" +
        "border:3px solid #FF7A1A;border-radius:10px;background:#FFF8EC;color:#0E2238;font-weight:800;" +
        "font-family:var(--font-ui, sans-serif);text-transform:uppercase;outline:none;touch-action:manipulation;" +
        "-webkit-appearance:none;appearance:none;-webkit-user-select:text;user-select:text;pointer-events:auto;";
      // Typed keys belong to the box only: the game and the page's own key listeners (touch mode off on a key,
      // "Click to play") never see them. Enter or the keyboard's Done closes the keyboard.
      var keep = function (event) {
        event.stopPropagation();
        if (event.type === "keydown" && event.key === "Enter") {
          event.preventDefault();
          box.blur();
        }
      };
      box.addEventListener("keydown", keep);
      box.addEventListener("keypress", keep);
      box.addEventListener("keyup", keep);
      document.body.appendChild(box);
      window.ccNameBox = box;
    }
    var canvas = document.getElementById("cc-canvas") || document.querySelector("canvas");
    var rect = canvas ? canvas.getBoundingClientRect() : { left: 0, top: 0, width: window.innerWidth, height: window.innerHeight };
    if (reset || box.ccOwner !== owner || box.hidden) {
      box.ccOwner = owner;
      box.value = UTF8ToString(currentPointer);
      box.maxLength = maxLength;
    }
    var h = height * rect.height;
    box.style.left = (rect.left + left * rect.width) + "px";
    box.style.top = (rect.top + top * rect.height) + "px";
    box.style.width = (width * rect.width) + "px";
    box.style.height = h + "px";
    box.style.fontSize = Math.max(16, Math.round(h * 0.45)) + "px"; // 16 px or more: iPhone Safari never zooms in on it
    box.hidden = false;
  },

  // Takes the box away when this owner's field is gone or hidden (a newer owner keeps it).
  CarcavelosNameBoxHide: function (owner) {
    var box = window.ccNameBox;
    if (box && box.ccOwner === owner && !box.hidden) {
      box.blur();
      box.hidden = true;
    }
  },

  // What is in the box right now ("" when the box belongs to another field or is closed).
  CarcavelosNameBoxValue: function (owner) {
    var box = window.ccNameBox;
    var text = box && box.ccOwner === owner && !box.hidden ? String(box.value).slice(0, 200) : "";
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  // 1 while the phone keyboard types into the box (the game keeps its keys off then).
  CarcavelosNameBoxFocused: function (owner) {
    var box = window.ccNameBox;
    return box && box.ccOwner === owner && !box.hidden && document.activeElement === box ? 1 : 0;
  },

  // Load time P8 (18 Nov): the address of the campus world file the page picked for this GPU, "" when there is none
  // (index.html window.ccWorldBundleUrl). C# side: WebPage.WorldBundleUrl, RaceFlow/WorldStream.cs.
  CarcavelosWorldBundleUrl: function () {
    var url = typeof window.ccWorldBundleUrl === "string" ? window.ccWorldBundleUrl : "";
    var size = lengthBytesUTF8(url) + 1;
    var buffer = _malloc(size);
    stringToUTF8(url, buffer, size);
    return buffer;
  },

  // state: 0 the world is downloading (progress 0 to 1), 1 the track is ready, 2 the world could not be loaded.
  CarcavelosWorldState: function (state, progress) {
    window.dispatchEvent(new CustomEvent("cc-world", { detail: { state: state, progress: progress } }));
  },

  // Kart builder, 18 Nov: the page's first screen. 0 = gone (the game has the screen), 1 = it covers the game
  // (loading, or no room for the line-up), 2 = "Click to play" is up and shows the live game on its right half,
  // where the game draws its four drivers (index.html adds "cc-live"). C# side: WebPage.FirstScreen, UI/StartLineup.cs.
  CarcavelosFirstScreen: function () {
    var loader = document.getElementById("cc-loader");
    if (!loader || loader.hidden || loader.classList.contains("cc-done")) {
      return 0;
    }
    return loader.classList.contains("cc-live") ? 2 : 1;
  },

  // Where the page's controls legend starts, in thousandths of the window height from the top (1000 = no legend).
  // The first-screen line-up keeps its names above it (the legend wraps to two rows on narrower windows).
  CarcavelosFirstScreenFloor: function () {
    var dock = document.querySelector("#cc-loader .cc-controls");
    var height = window.innerHeight || 1;
    if (!dock || dock.offsetParent === null) {
      return 1000;
    }
    var top = dock.getBoundingClientRect().top / height * 1000;
    return Math.round(Math.max(0, Math.min(1000, top)));
  }
});
