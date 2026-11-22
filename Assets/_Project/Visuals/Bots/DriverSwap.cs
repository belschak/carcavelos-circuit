using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Driver select (Luc, 17 Nov 2026): who the five bots are once the player picked a driver (RaceFlow's
    // DriverChoice). Luc's rule: the three drivers the player did not pick race in their own karts, plus two
    // repainted Nova Classics (red and green), so the grid stays at six karts.
    // Names (David, 20 Nov 2026; Luc, 21 Nov; David + Luc, 22 Nov): the roster drivers race under their own names
    // (Ricardo, Cláudio, Luísa, Paula) and the two repainted Classics are always Luís (red) and Paulo (green), the
    // two teachers whose names the female drivers carry in the female form. The Classic is Luc's male Rookie model,
    // so it only ever carries a man's name (before, the picked driver's name went on the red Classic, which put a
    // woman's name on the Rookie). No name races twice. The
    // driving style of seat i is BotSettings' profile i (pace, cornering, lane, drifting); only name, colour and kart
    // change.
    public static class DriverSwap
    {
        // The Classics' drivers, red then green: the teachers Luís and Paulo (RaceFlow's TributeRules, credits order).
        public static readonly string[] ClassicNames = { TributeRules.FirstName(2), TributeRules.FirstName(3) };
        public static readonly Color[] CopyColours = { new Color(0.88f, 0.16f, 0.16f), new Color(0.16f, 0.66f, 0.36f) };

        // Who drives the repainted Classics for this pick: Luís (red) and Paulo (green). A pick outside the roster
        // (never happens, DriverChoice checks it) leaves all four drivers in their own karts, so only Luís (green).
        public static string[] CopyNames(int picked)
        {
            return DriverRoster.IsValid(picked)
                ? new[] { ClassicNames[0], ClassicNames[1] }
                : new[] { ClassicNames[0] };
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
