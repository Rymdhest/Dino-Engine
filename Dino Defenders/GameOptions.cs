using Dino_Defenders.Net;
using OpenTK.Mathematics;

namespace Dino_Defenders
{
    /// <summary>
    /// What the game was started with. Playing needs none of it; these jump straight to a world for development, and let a
    /// scripted run look at one spot, take a screenshot and quit.
    ///   --seed N  --archetype id  --site Kind  --at x,z  --look distance  --shot file.png  --frames N
    ///   --tour folder   Visits one of every kind of site, and a wood, a road and a shore, photographing each.
    ///   --level N       How dangerous the first world is.
    ///   --bot true      Plays the hero: it fights whatever is near. For watching the game run with nobody at the keyboard.
    ///   --sweep true    With the bot: when nothing is near, goes to the next place still held, until the world is free.
    ///   --hero N        Starts the hero at level N, with the attributes to match.
    ///   --panel name    Opens map, hero, chronicle, merchant, elder, waygate, friends or pause once the world is up.
    ///   --travel N      Steps through a waygate after N frames, to a world drawn from the current one.
    ///   --savefile path Keeps the save here, and uses it even on a scripted run.
    ///   --host true     Opens the game to others straight away. --port N and --secret text say where and with what.
    ///   --join place    Joins a game: an invite code, or address:port with --secret.
    ///   --name text     What this hero is called in company.
    ///   --netsim l,j,p  Plays through a bad line: l ms round trip, up to j ms of jitter, p percent of packets lost.
    ///   --dedicated true  Runs a server with no window, for as long as it's left running.
    /// A scripted run otherwise neither reads nor writes the player's save.
    /// </summary>
    internal sealed class GameOptions
    {
        public int? Seed;
        public string? Archetype;
        public string? Site;
        public Vector2? At;
        public float? Look;         // Metres above the player for a high look at a place.
        public string? Shot;
        public string? Tour;
        public int ShotAfterFrames = 150;
        public int Level = 1;
        public bool Bot;
        public bool Sweep;
        public int HeroLevel = 1;
        public string? Panel;
        public int TravelAfterFrames = -1;
        public string? SaveFile;
        public bool Host;
        public int Port = UdpTransport.DefaultPort;
        public string? Secret;
        public string? Join;
        public string? Name;
        public string? NetSim;
        public bool Dedicated;

        /// <summary>Whether this run plays on from a save, as someone playing does, rather than being a throwaway.</summary>
        public bool Saves => SaveFile != null || !(Seed != null || Archetype != null || Tour != null || Shot != null || Bot);

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
                    case "shot": options.Shot = value; break;
                    case "tour": options.Tour = value; break;
                    case "frames": options.ShotAfterFrames = int.Parse(value); break;
                    case "level": options.Level = int.Parse(value); break;
                    case "bot": options.Bot = value == "true"; break;
                    case "sweep": options.Sweep = value == "true"; break;
                    case "hero": options.HeroLevel = int.Parse(value); break;
                    case "panel": options.Panel = value; break;
                    case "travel": options.TravelAfterFrames = int.Parse(value); break;
                    case "savefile": options.SaveFile = value; break;
                    case "host": options.Host = value == "true"; break;
                    case "port": options.Port = int.Parse(value); break;
                    case "secret": options.Secret = value; break;
                    case "join": options.Join = value; break;
                    case "name": options.Name = value; break;
                    case "netsim": options.NetSim = value; break;
                    case "dedicated": options.Dedicated = value == "true"; break;
                }
            }
            return options;
        }
    }
}
