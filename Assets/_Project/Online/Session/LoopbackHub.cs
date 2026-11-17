using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CarcavelosCircuit.Online
{
    // The "network" of LoopbackSession: rooms that live in this process. Several sessions in one process share a
    // room, so Edit Mode tests can play a whole online race (join, master, control, poses, finish, leave) without a
    // browser, and the Editor can open the lobby alone. Plain C#.
    // Clock: a stopwatch by default; tests set Clock to their own function to move time by hand.
    public static class LoopbackHub
    {
        private static readonly Dictionary<string, LoopbackRoom> rooms = new Dictionary<string, LoopbackRoom>();
        private static readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private static int nextId = 1;

        // The shared clock in seconds. null = the stopwatch.
        public static Func<double> Clock;

        public static double Now
        {
            get { return Clock != null ? Clock() : stopwatch.Elapsed.TotalSeconds; }
        }

        // The room with that name, made on first use.
        public static LoopbackRoom RoomOf(string name)
        {
            LoopbackRoom room;
            if (!rooms.TryGetValue(name, out room))
            {
                room = new LoopbackRoom(name);
                rooms[name] = room;
            }
            return room;
        }

        // How many players a room has right now (0 for a room nobody opened).
        public static int CountIn(string name)
        {
            LoopbackRoom room;
            return rooms.TryGetValue(name, out room) ? room.Count : 0;
        }

        // Each session gets the next number: join order and a unique id in one.
        public static int NextId()
        {
            return nextId++;
        }

        // Tests start from nothing: no rooms, ids from 1, the stopwatch clock.
        public static void Reset()
        {
            rooms.Clear();
            nextId = 1;
            Clock = null;
        }
    }
}
