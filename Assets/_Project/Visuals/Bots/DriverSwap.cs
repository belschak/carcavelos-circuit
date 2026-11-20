using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Driver select (Luc, 17 Nov 2026): who the five bots are once the player picked a driver (RaceFlow's
    // DriverChoice). Luc's rule: the three drivers the player did not pick race in their own karts, plus two
    // repainted Nova Classics (red and green), so the grid stays at six karts.
    // Names (David, 20 Nov 2026): every bot is a first name of the teaching staff (RaceFlow's TributeRules.GivenNames)
    // and no name races twice. The teacher whose kart the player took drives the red Classic, and João (the second
    // given name of Ricardo João Gil Pereira) the green one, so the five bots are always Ricardo, João, Cláudio, Luís
    // and Paulo. The driving style of seat i is BotSettings' profile i (pace, cornering, lane, drifting); only name,
    // colour and kart change.
    public static class DriverSwap
    {
        // The green Classic's driver: the one given name of the staff that is not a driver in DriverRoster.
        public const string ExtraName = "João";
        public static readonly Color[] CopyColours = { new Color(0.88f, 0.16f, 0.16f), new Color(0.16f, 0.66f, 0.36f) };

        // Who drives the repainted Classics for this pick: the picked teacher (red), then João (green). A pick outside
        // the roster (never happens, DriverChoice checks it) leaves all four teachers in their own karts, so only João.
        public static string[] CopyNames(int picked)
        {
            return DriverRoster.IsValid(picked)
                ? new[] { DriverRoster.Get(picked).Name, ExtraName }
                : new[] { ExtraName };
        }

        // One bot on the grid: its profile (name, colour, style), its kart model, and whether the kart gets repainted.
        public class Seat
        {
            public BotProfile Profile;
            public string KartVisual;
            public bool Repaint;
        }

        // The five seats for this pick, in grid order. settings may be null (then a default style is used).
        public static List<Seat> Lineup(int picked, BotSettings settings)
        {
            var seats = new List<Seat>();
            for (int d = 0; d < DriverRoster.Count; d++)
            {
                if (d == picked)
                {
                    continue;
                }
                DriverInfo driver = DriverRoster.Get(d);
                seats.Add(MakeSeat(seats.Count, driver.Name, driver.Colour, driver.KartVisual, false, settings));
            }
            string classic = DriverRoster.Get(DriverRoster.Rookie).KartVisual;
            string[] copies = CopyNames(picked);
            for (int c = 0; c < copies.Length; c++)
            {
                Color colour = copies.Length == 1 ? CopyColours[1] : CopyColours[c];
                seats.Add(MakeSeat(seats.Count, copies[c], colour, classic, true, settings));
            }
            return seats;
        }

        private static Seat MakeSeat(int index, string name, Color colour, string kartVisual, bool repaint, BotSettings settings)
        {
            BotProfile style = settings != null ? settings.Bot(index) : null;
            BotProfile profile = style != null
                ? new BotProfile(name, colour, style.pace, style.cornering, style.lane, style.drifts)
                : new BotProfile(name, colour, 0.95f, 0.93f, 0f, false);
            return new Seat { Profile = profile, KartVisual = kartVisual, Repaint = repaint };
        }
    }
}
