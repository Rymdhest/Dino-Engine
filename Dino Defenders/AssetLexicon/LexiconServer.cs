using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World.Lexicon;
using OpenTK.Mathematics;

namespace Dino_Defenders.AssetLexicon
{
    /// <summary>What a client asks of the server about the asset lexicon, as one <see cref="Net.HeroAction.Lexicon"/>.</summary>
    public enum LexiconAsk : byte
    {
        /// <summary>From the Haven, everyone into the lexicon: the beta vendor's button.</summary>
        Visit,
        /// <summary>From the lexicon, everyone home to the Haven.</summary>
        Leave,
        /// <summary>In the lexicon, the one asking to a point, as the index sends someone to what they chose.</summary>
        GoTo,
    }

    /// <summary>
    /// The server's side of the asset lexicon (LEXICON.md). The room is a world like any other, grown from <see cref="LexiconWorld.Request"/>, and what's in it that the world doesn't build is
    /// stood here: every creature on show, on its plot, as soon as the room has grown. They're only to be looked at: a body with nothing behind it, no brain, health or place it belongs to,
    /// so nothing can fight it, push it about or count it, and it never moves from where it was stood. Nothing about the room is saved, and nothing done in it counts for anything.
    /// </summary>
    public static class LexiconServer
    {
        /// <summary>Whether the beta vendor offers the way into the lexicon, which he only does while he stands in the Haven at all.</summary>
        public static bool Open => BetaVendor.Standing;

        /// <summary>Stands every creature on show where its plot is. Asked once, when the room's world is ready.</summary>
        public static void Stock(GameServer server, ServerSession session)
        {
            int stood = 0;
            foreach (PlacedExhibit exhibit in LexiconLayout.Current.Exhibits)
            {
                if (exhibit.Exhibit.Kind != LexiconKind.Creature) continue;
                Vector2 at = exhibit.Centre;
                var position = new Vector3(at.X, session.World.Height.Sample(at), at.Y);
                server.Ecs.CreateEntity(exhibit.Exhibit.Name,
                    new NetIdentityComponent(server.Ghosts.NextId(), importance: 0.6f),
                    new PositionComponent(position),
                    new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, exhibit.Exhibit.Yaw)),
                    new ScaleComponent(new Vector3(exhibit.Exhibit.Scale)),
                    new VisualComponent(exhibit.Exhibit.Look, exhibit.Exhibit.Prop ? VisualKind.Prop : VisualKind.Character));
                stood++;
            }
            LexiconPlan plan = LexiconLayout.Current;
            Console.WriteLine($"The lexicon has {stood} creatures on show, {plan.Exhibits.Count} exhibits in all, its landing at {plan.Arrival.X:0},{plan.Arrival.Y:0}: "
                + string.Join(", ", plan.Sections.Select(section => $"{section.Section.Name} from {section.Sign.X:0},{section.Sign.Y:0}")));
        }

        /// <summary>A client's ask, read whole whatever's done with it, so that what comes after it in the message is read right.</summary>
        public static void Act(GameServer server, ServerPlayer player, BitReader reader)
        {
            var ask = (LexiconAsk)reader.ReadByte();
            ServerSession? session = server.Session;
            switch (ask)
            {
                case LexiconAsk.Visit:
                    if (session == null || !session.IsHaven || !Open) break;
                    // An open way is the Haven's, and what was given for it is gone: leaving would shut it for nothing.
                    if (session.Portal != null)
                    {
                        server.Note(player, "The way is open. Step through it, or close it at the map table, before going to the asset test level.");
                        break;
                    }
                    Console.WriteLine($"{player.Name} has gone to the asset lexicon, and everyone with them.");
                    server.Travel(LexiconWorld.Request);
                    break;

                case LexiconAsk.Leave:
                    if (session == null || !session.IsLexicon) break;
                    server.Travel(Lands.Haven);
                    break;

                case LexiconAsk.GoTo:
                    var to = new Vector2(reader.ReadFloat(), reader.ReadFloat());
                    if (session == null || !session.IsLexicon || !float.IsFinite(to.X) || !float.IsFinite(to.Y) || !session.World.InBounds(to)) break;
                    // Onto the open ground nearest it, a step clear of anything built or set down, and into the middle of a place if it's on one.
                    server.Carry(player, LexiconWorld.StandFor(session.World, to) ?? to);
                    break;

                default:
                    throw new NetReadException($"No such ask of the lexicon as {ask}.");
            }
        }
    }
}
