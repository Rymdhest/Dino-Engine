using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using OpenTK.Mathematics;

namespace Dino_Defenders
{
    /// <summary>
    /// What the game was started with. Playing needs none of it; these jump straight to a world for development, and let a
    /// scripted run look at one spot, take a screenshot and quit.
    ///   --seed N  --archetype id  --site Kind  --at x,z  --look distance  --angle degrees  --shot file.png  --frames N
    ///   --tour folder   Visits one of every kind of site, and a wood, a road and a shore, photographing each.
    ///   --level N       How dangerous the first world is.
    ///   --bot true      Plays the hero: it fights whatever is near. For watching the game run with nobody at the keyboard.
    ///   --sweep true    With the bot: when nothing is near, goes to the next place still held, until the world is free.
    ///   --walk x,z      With the bot: runs straight there and does nothing else, for whatever needs someone on foot. It gets about by being moved otherwise.
    ///   --hero N        Starts the hero at level N.
    ///   --talents a,b   Walks the talent tree to these nodes, by id, taking everything on the way. Skills they teach go on the bar.
    ///   --equip a,b     Puts these uniques on the hero, by id.
    ///   --parade all    Stands one of every creature in front of the hero for looking at, or those named, as wolf,boar.
    ///   --loot all      Drops one find of every grade in front of the hero, or everything a tier gives, as boss, elite, chest or hoard.
    ///   --panel name    Opens map, hero, chronicle, merchant, elder, waygate, friends, pause or sound once the world is up.
    ///   --travel N      Steps through a waygate after N frames, to a world drawn from the current one, with whatever offering is below.
    ///   --peril N  --teeming N  --overrun N  --omens a,b   What's been paid on the first world, or on the way through a waygate with --travel.
    ///   --gold N        Starts the hero with this much gold.
    ///   --proven N      Starts the hero having come through this much peril.
    ///   --savefile path Keeps the save here, and uses it even on a scripted run.
    ///   --host true     Opens the game to others straight away. --port N and --secret text say where and with what.
    ///   --join place    Joins a game: an invite code, or address:port with --secret.
    ///   --name text     What this hero is called in company.
    ///   --netsim l,j,p  Plays through a bad line: l ms round trip, up to j ms of jitter, p percent of packets lost.
    ///   --dedicated true  Runs a server with no window, for as long as it's left running.
    ///   --render a=b,c=d  Sets values of the engine's LightingSettings, such as sun=0,bloom=0,exposure=0.8.
    ///   --film folder   Saves a numbered run of frames, for anything that has to be judged moving. Each frame lasts exactly 1 / --filmrate
    ///                   seconds, however long saving it takes. --filmstart N and --filmframes N say which frames.
    ///   --clock s       Starts as if the game had been running s seconds already: some faults only show late in a session.
    ///   --wind f:s:d;f:s:d  From frame f on, the region's wind is s m/s towards d degrees, in place of what the land gives.
    ///   --guide N       Calls the guiding wind after N frames.
    ///   --record file.wav  Saves everything the mixer sends out, to listen to or measure afterwards.
    ///   --sound true    Plays on the real device even on a scripted run, which otherwise keeps quiet.
    ///   --volume v      Overall level from 0 to 1, in place of what the player has chosen.
    ///   --music v       The music's level from 0 to 1, likewise, which at 0 leaves everything else to be measured.
    ///   --hidden true   Draws with no window on screen and never takes the focus, for a scripted run beside someone playing.
    ///   --weather name  Holds the weather everywhere: clear, cloudy, overcast, wet, drizzle, rain, storm, snow, blizzard, dust, ash or fog.
    ///   --weatherclock s  Starts this many seconds into the world's own weather.
    /// A scripted run otherwise neither reads nor writes the player's save.
    /// </summary>
    internal sealed class GameOptions
    {
        public int? Seed;
        public string? Archetype;
        public string? Site;
        public Vector2? At;
        public float? Look;         // Metres above the player for a high look at a place.
        public float? Angle;        // Degrees above the horizon the camera looks down from, for a low look at a place.
        public string? Shot;
        public string? Tour;
        public int ShotAfterFrames = 150;
        public int Level = 1;
        public bool Bot;
        public bool Sweep;
        public Vector2? Walk;
        public int HeroLevel = 1;
        public readonly List<string> Talents = new();
        public readonly List<string> Equip = new();
        public string? Parade;

        /// <summary>The same as <see cref="Parade"/>, but they're awake and want a fight.</summary>
        public string? Brawl;
        public string? Loot;
        public string? Panel;
        public string? Wear;
        public int TravelAfterFrames = -1;
        public readonly Offering Offering = new();
        public int Gold;
        public int Proven;
        public string? SaveFile;
        public bool Host;
        public int Port = UdpTransport.DefaultPort;
        public string? Secret;
        public string? Join;
        public string? Name;
        public string? NetSim;
        public bool Dedicated;
        public bool Hidden;
        public string? Weather;
        public double WeatherClock;
        public string? Render;
        public string? Film;
        public int FilmStart = 120;
        public int FilmFrames = 240;
        public float FilmRate = 30f;
        public float Clock;
        public readonly List<(int frame, float speed, float direction)> Wind = new();
        public int GuideAfterFrames = -1;
        public string? Record;
        public bool? Sound;
        public float? Volume;
        public float? Music;

        /// <summary>Whether this run plays on from a save, as someone playing does, rather than being a throwaway.</summary>
        public bool Saves => SaveFile != null || !(Seed != null || Archetype != null || Tour != null || Shot != null || Film != null || Bot);

        /// <summary>Whether anyone is listening. A script running in the background shouldn't suddenly fill the room.</summary>
        public bool Audible => Sound ?? !(Tour != null || Shot != null || Film != null || Bot);

        public static GameOptions Parse(string[] args)
        {
            var options = new GameOptions();
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                string value = args[i + 1];
                switch (args[i].TrimStart('-'))
                {
                    case "seed": options.Seed = int.Parse(value); break;
                    case "archetype": options.Archetype = value; break;
                    case "site": options.Site = value; break;
                    case "at":
                        string[] parts = value.Split(',');
                        options.At = new Vector2(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case "look": options.Look = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "angle": options.Angle = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "shot": options.Shot = value; break;
                    case "tour": options.Tour = value; break;
                    case "frames": options.ShotAfterFrames = int.Parse(value); break;
                    case "level": options.Level = int.Parse(value); break;
                    case "bot": options.Bot = value == "true"; break;
                    case "sweep": options.Sweep = value == "true"; break;
                    case "walk":
                        string[] walk = value.Split(',');
                        options.Walk = new Vector2(float.Parse(walk[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(walk[1], System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case "hero": options.HeroLevel = int.Parse(value); break;
                    case "talents": options.Talents.AddRange(value.Split(',')); break;
                    case "equip": options.Equip.AddRange(value.Split(',')); break;
                    case "parade": options.Parade = value; break;
                    case "brawl": options.Brawl = value; break;
                    case "loot": options.Loot = value; break;
                    case "panel": options.Panel = value; break;
                    case "wear": options.Wear = value; break;
                    case "travel": options.TravelAfterFrames = int.Parse(value); break;
                    case "peril": options.Offering.Peril = int.Parse(value); break;
                    case "teeming": options.Offering.Teeming = int.Parse(value); break;
                    case "overrun": options.Offering.Overrun = int.Parse(value); break;
                    case "omens": options.Offering.Omens.AddRange(value.Split(',')); break;
                    case "gold": options.Gold = int.Parse(value); break;
                    case "proven": options.Proven = int.Parse(value); break;
                    case "savefile": options.SaveFile = value; break;
                    case "host": options.Host = value == "true"; break;
                    case "port": options.Port = int.Parse(value); break;
                    case "secret": options.Secret = value; break;
                    case "join": options.Join = value; break;
                    case "name": options.Name = value; break;
                    case "netsim": options.NetSim = value; break;
                    case "dedicated": options.Dedicated = value == "true"; break;
                    case "hidden": options.Hidden = value == "true"; break;
                    case "weather": options.Weather = value; break;
                    case "weatherclock": options.WeatherClock = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "render": options.Render = value; break;
                    case "film": options.Film = value; break;
                    case "filmstart": options.FilmStart = int.Parse(value); break;
                    case "filmframes": options.FilmFrames = int.Parse(value); break;
                    case "filmrate": options.FilmRate = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "clock": options.Clock = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "guide": options.GuideAfterFrames = int.Parse(value); break;
                    case "record": options.Record = value; break;
                    case "sound": options.Sound = value == "true"; break;
                    case "volume": options.Volume = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "music": options.Music = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "wind":
                        foreach (string change in value.Split(';'))
                        {
                            string[] wind = change.Split(':');
                            options.Wind.Add((int.Parse(wind[0]), float.Parse(wind[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(wind[2], System.Globalization.CultureInfo.InvariantCulture)));
                        }
                        break;
                }
            }
            options.Offering.Sanitize();
            return options;
        }
    }
}
