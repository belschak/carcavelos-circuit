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

  // The browser's own text box for the leaderboard name (phones: their keyboard opens reliably there).
  // Returns the typed text, or the old name when the player cancels. The game checks the answer itself.
  CarcavelosPromptName: function (currentPointer) {
    var current = UTF8ToString(currentPointer);
    var answer = window.prompt("Your name for the leaderboard (2 to 16 letters, digits, space, - or _)", current);
    if (answer === null) {
      answer = current;
    }
    answer = String(answer).slice(0, 200); // the game refuses anything longer anyway
    var size = lengthBytesUTF8(answer) + 1;
    var buffer = _malloc(size);
    stringToUTF8(answer, buffer, size);
    return buffer; // Unity turns this into a C# string and frees the memory
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
  }
});
