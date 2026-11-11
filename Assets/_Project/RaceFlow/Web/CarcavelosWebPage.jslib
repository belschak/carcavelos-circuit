// The game's two messages to the Web page (task R10/R11). C# side: RaceFlow/Web/WebPage.cs.
// Unity puts every .jslib into the Web build; these functions run in the browser, not in C#.
mergeInto(LibraryManager.library, {
  // racing: 1 while a race runs (countdown or racing), 0 on the start screen and the results.
  // The page template listens for "cc-race-state" and shows its driving buttons only during a race.
  CarcavelosRaceState: function (racing) {
    window.dispatchEvent(new CustomEvent("cc-race-state", { detail: { racing: racing !== 0 } }));
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
