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
  }
});
