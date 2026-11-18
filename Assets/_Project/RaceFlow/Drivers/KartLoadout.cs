using System;

namespace CarcavelosCircuit.RaceFlow
{
    // Kart builder (Luc's idea, 18 Nov 2026, like the kart select of a classic 3DS kart racer): a player picks a DRIVER, a
    // KART and TYRES. Any driver in any kart on any tyres, 4 x 4 x 4 = 64 combinations, built from Luc's four kart models
    // at run time (Visuals/Kart/KartAssembler). Looks only: every combination drives exactly the same kart (Role 1's
    // KartController), so the Time Attack leaderboard stays fair and online races stay identical on every screen.
    // Index i of each part list belongs to driver i of DriverRoster: driver i's own ("signature") kart and tyres are
    // KartNames[i] and TyreNames[i] (Rookie = Nova Classic + Slick, Tiago = Beach Buggy + Offroad, ...).
    // Plain C# (no MonoBehaviour), so every rule here has Edit Mode tests (Tests/EditMode/Drivers/KartLoadoutTests).
    public struct KartLoadout : IEquatable<KartLoadout>
    {
        // Luc's part names (Blender export Art/Export, v12). Ids = the model file names (Kart_<Id>.fbx, Tyre_<Id>.fbx).
        public static readonly string[] KartIds = { "Classic", "Buggy", "Orbit", "StreetGT" };
        public static readonly string[] KartNames = { "NOVA CLASSIC", "BEACH BUGGY", "ORBIT", "STREET GT" };
        public static readonly string[] TyreIds = { "Slick", "Offroad", "Balloon", "Retro" };
        public static readonly string[] TyreNames = { "SLICK", "OFFROAD", "BALLOON", "RETRO" };

        // One line per part, shown under the builder's columns while the part is highlighted.
        public static readonly string[] KartLines =
        {
            "The Rookie's racer: a long nose and a big rear wing.",
            "Tiago's beach buggy, with a surfboard in the roll cage.",
            "Nova's rocket kart, fins and a little satellite dish.",
            "Amara's hot rod, built from spare parts in her garage."
        };
        public static readonly string[] TyreLines =
        {
            "Smooth racing slicks on red rims.",
            "Chunky offroad tread for the beach.",
            "Big soft balloon tyres with glowing rims.",
            "Whitewall classics with chrome hubcaps."
        };

        public const int PartCount = 4;

        // Online code (sent as one int in the room, OnlinePlayer.DriverIndex): 0-3 = that driver with their own kart and
        // tyres (what older builds sent, so they still read right), CustomBase + driver * 16 + kart * 4 + tyres = any
        // other combination. Every other number is refused (an old or broken client = the Rookie's own set).
        public const int CustomBase = 100;
        public const int CodeCount = PartCount * PartCount * PartCount;

        public int Driver { get; private set; }
        public int Kart { get; private set; }
        public int Tyres { get; private set; }

        public KartLoadout(int driver, int kart, int tyres)
        {
            Driver = driver;
            Kart = kart;
            Tyres = tyres;
        }

        // The driver with their own kart and tyres. A driver outside the roster = the Rookie's set.
        public static KartLoadout Signature(int driver)
        {
            int d = IsPart(driver) ? driver : DriverRoster.Rookie;
            return new KartLoadout(d, d, d);
        }

        public static KartLoadout Default { get { return Signature(DriverRoster.Rookie); } }

        public bool IsValid { get { return IsPart(Driver) && IsPart(Kart) && IsPart(Tyres); } }

        // Driver in their own kart on their own tyres (the tiles mark this with a star).
        public bool IsSignature { get { return Kart == Driver && Tyres == Driver; } }

        public static bool IsPart(int index)
        {
            return index >= 0 && index < PartCount;
        }

        // The number sent to the online room. An invalid loadout sends the Rookie's set.
        public int Code
        {
            get
            {
                if (!IsValid)
                {
                    return DriverRoster.Rookie;
                }
                if (IsSignature)
                {
                    return Driver;
                }
                return CustomBase + Driver * 16 + Kart * 4 + Tyres;
            }
        }

        // True for every number Code can produce (0-3 and the 64 custom codes, signatures included).
        public static bool IsValidCode(int code)
        {
            if (IsPart(code))
            {
                return true;
            }
            int custom = code - CustomBase;
            return custom >= 0 && custom < CodeCount;
        }

        // Reads a code from the room. Anything that is not a valid code = the Rookie's set.
        public static KartLoadout FromCode(int code)
        {
            if (!IsValidCode(code))
            {
                return Default;
            }
            if (IsPart(code))
            {
                return Signature(code);
            }
            int custom = code - CustomBase;
            return new KartLoadout(custom / 16, (custom / 4) % 4, custom % 4);
        }

        // Model name: the signature set keeps Luc's prefab name (KartVisual_Buggy_Tiago), any other set adds the tyres.
        public string ModelName
        {
            get
            {
                if (!IsValid)
                {
                    return Default.ModelName;
                }
                string name = "KartVisual_" + KartIds[Kart] + "_" + DriverRoster.Get(Driver).Name;
                return IsSignature ? name : name + "_" + TyreIds[Tyres];
            }
        }

        // "TIAGO  /  BEACH BUGGY  /  BALLOON": the builder's combo line.
        public string Title
        {
            get
            {
                KartLoadout safe = IsValid ? this : Default;
                return DriverRoster.Get(safe.Driver).Name.ToUpperInvariant() + "  /  " + KartNames[safe.Kart] + "  /  " + TyreNames[safe.Tyres];
            }
        }

        public KartLoadout WithDriver(int driver)
        {
            return new KartLoadout(driver, Kart, Tyres);
        }

        public KartLoadout WithKart(int kart)
        {
            return new KartLoadout(Driver, kart, Tyres);
        }

        public KartLoadout WithTyres(int tyres)
        {
            return new KartLoadout(Driver, Kart, tyres);
        }

        // Moves up (-1) or down (+1) a column and wraps around at the ends (the arrow keys in the builder).
        public static int Step(int index, int direction)
        {
            if (!IsPart(index))
            {
                return direction < 0 ? PartCount - 1 : 0;
            }
            return ((index + direction) % PartCount + PartCount) % PartCount;
        }

        public bool Equals(KartLoadout other)
        {
            return Driver == other.Driver && Kart == other.Kart && Tyres == other.Tyres;
        }

        public override bool Equals(object obj)
        {
            return obj is KartLoadout && Equals((KartLoadout)obj);
        }

        public override int GetHashCode()
        {
            return Driver * 16 + Kart * 4 + Tyres;
        }

        public override string ToString()
        {
            return Title;
        }
    }
}
