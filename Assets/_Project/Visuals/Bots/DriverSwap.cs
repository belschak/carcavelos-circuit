using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Driver select (Luc, 17 Nov 2026): who the five bots are once the player picked a driver (RaceFlow's
    // DriverChoice). Luc's rule: the three drivers the player did not pick race in their own karts, plus two
    // repainted Rookies in the Nova Classic (Gaivota in red, Sardinha in green), so the grid stays at six karts.
    // The driving style of seat i is BotSettings' profile i (pace, cornering, lane, drifting); only name, colour
    // and kart change.
    public static class DriverSwap
    {
        // The two repainted Rookies: name and colour (made-up names, the seagull and the sardine).
        public static readonly string[] CopyNames = { "Gaivota", "Sardinha" };
        public static readonly Color[] CopyColours = { new Color(0.88f, 0.16f, 0.16f), new Color(0.16f, 0.66f, 0.36f) };

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
            string rookieKart = DriverRoster.Get(DriverRoster.Rookie).KartVisual;
            for (int c = 0; c < CopyNames.Length; c++)
            {
                seats.Add(MakeSeat(seats.Count, CopyNames[c], CopyColours[c], rookieKart, true, settings));
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
