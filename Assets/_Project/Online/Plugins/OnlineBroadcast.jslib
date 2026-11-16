// The browser side of the ?net=local test transport (C# side: Online/Session/BroadcastChannelSession.cs).
// Unity puts every .jslib into the Web build; these functions run in the browser, not in C#.
// A BroadcastChannel delivers a message to every other tab of the SAME browser that opened a channel with the same
// name, never back to the sender. Incoming messages wait in a queue until C# polls them (one per call), so C# only
// ever sees them inside its own frame.
mergeInto(LibraryManager.library, {
  // Opens the channel. 1 = ok, 0 = this browser has no BroadcastChannel.
  OnlineBroadcastOpen: function (channelPointer) {
    var name = UTF8ToString(channelPointer);
    if (typeof BroadcastChannel === "undefined") {
      return 0;
    }
    try {
      if (window.ccOnline && window.ccOnline.channel) {
        window.ccOnline.channel.close();
      }
      var state = { channel: new BroadcastChannel(name), queue: [], leaveMessage: "" };
      state.channel.onmessage = function (event) {
        // Only text, and never more than 4096 lines waiting (if C# stopped polling, the memory stays small).
        if (typeof event.data === "string" && event.data.length <= 800 && state.queue.length < 4096) {
          state.queue.push(event.data);
        }
      };
      if (!window.ccOnlineUnloadHooked) {
        // The tab closes or navigates away: tell the room, so nobody waits 6 s for the heartbeat to stop.
        window.addEventListener("pagehide", function () {
          var s = window.ccOnline;
          if (s && s.channel && s.leaveMessage) {
            try { s.channel.postMessage(s.leaveMessage); } catch (e) { }
          }
        });
        window.ccOnlineUnloadHooked = true;
      }
      window.ccOnline = state;
      return 1;
    } catch (e) {
      return 0;
    }
  },

  OnlineBroadcastClose: function () {
    var state = window.ccOnline;
    if (state && state.channel) {
      try { state.channel.close(); } catch (e) { }
    }
    window.ccOnline = null;
  },

  OnlineBroadcastPost: function (messagePointer) {
    var state = window.ccOnline;
    if (state && state.channel) {
      try { state.channel.postMessage(UTF8ToString(messagePointer)); } catch (e) { }
    }
  },

  // The line the page sends by itself when the tab goes away ("leave|id").
  OnlineBroadcastSetLeaveMessage: function (messagePointer) {
    var state = window.ccOnline;
    if (state) {
      state.leaveMessage = UTF8ToString(messagePointer);
    }
  },

  // The next waiting message, or null (0) when there is none. Unity turns the pointer into a C# string and frees it.
  OnlineBroadcastPoll: function () {
    var state = window.ccOnline;
    if (!state || state.queue.length === 0) {
      return 0;
    }
    var text = state.queue.shift();
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  // The shared clock in seconds: wall time, the same for every tab on this machine (an offset keeps the number small).
  OnlineBroadcastNow: function () {
    return (performance.timeOrigin + performance.now() - 1700000000000) / 1000;
  },

  // A random player id 1 .. 2147483646 for this tab.
  OnlineBroadcastRandomId: function () {
    var words = new Uint32Array(1);
    if (window.crypto && window.crypto.getRandomValues) {
      window.crypto.getRandomValues(words);
    } else {
      words[0] = Math.floor(Math.random() * 4294967295);
    }
    return (words[0] % 2147483646) + 1;
  }
});
