# Networking

How Dino Defenders plays with friends, and what to do so that anything you add keeps working. Up to five heroes share a world. There is no server of ours: every game runs on a player's own machine.

## The one idea

**There is no single-player mode.** The game always runs as a client talking to a server. Alone, that server is on a thread beside the client and is reached through a loopback, with no network involved. Hosting opens the same server up to friends. Joining a friend points the client at their server instead.

```
              alone or hosting                              a friend
  +-----------------------------------+            +----------------------+
  |  GameClient   <--- loopback --->  |            |                      |
  |  (draws, predicts its own hero)   |            |  GameClient          |
  |                                   |            |                      |
  |  GameServer   <-- UDP / Steam --------------------->                  |
  |  (the only place anything is      |            |                      |
  |   decided; its own thread)        |            |                      |
  +-----------------------------------+            +----------------------+
```

**A server holds more than one world.** `GameServer` is everyone connected, the party and the way out of the Haven, and each world someone is in is a `ServerRoom` of its own, with its own entities, ghosts, session and epoch, all run on the same tick on the server's one thread: the Haven, and the land the way is open to, side by side, so each hero steps through when they're ready. Moving someone to another world (`GameServer.Move`, after the tick) takes them out of one room and tells them about the next (`WorldChange`), exactly as a world change always has, and a room nobody's in any more is closed, but for the Haven and a land nobody's stepped into yet. Whatever's sent goes to the room's own heroes; only the party (`Message.Party`: who's in the game, which world each is in, who's hosting and how loot goes) and shared gold cross between rooms. The labs read `GameServer.Ecs`, `Session` and `Heroes` as the world the host is in (`Main`), and `Rooms` and `ServerPlayer.Room` for the rest. A lab's client keeps what it saw of the last world, which the game's clears on entering the next (`WorldScene.Enter`), so a test reading a client's entities after a move can find leftovers.

The folders enforce this. `Dino Defenders/Server/` decides everything and touches nothing of the GPU. `Client/` draws and predicts. `Gameplay/` is what both share. `Dino Engine/Networking/` is the machinery, and knows nothing about the game.

## Why it doesn't desync

Other ARPGs desync because the client keeps its own running idea of the world and has to be right about it. Here it doesn't, and three rules keep it that way.

1. **Clients don't simulate the world.** Enemies, other heroes, loot and arrows are drawn from what the server last said, a fraction of a second in the past, blended between two real states. There is nothing to drift.
2. **A client only ever predicts its own hero**, by running `HeroRules.Step`, the same code on the same numbers as the server. Each snapshot says what the server got for a tick, and the client checks that against what it predicted. If they differ, the server is right: the hero goes back and every command since is run again. A client can be wrong for a moment. It can't stay wrong.
3. **Hero movement depends only on the command, the hero's stats and the fixed shape of the world.** Never on other characters. That is why prediction is almost always exact, and it is the rule most likely to be broken by accident: see below.

World state is never sent as "this changed". Snapshots are differences from a state the client has *confirmed holding*, so a lost packet is simply covered by the next one. Nothing depends on any one packet arriving, including ghosts appearing and leaving.

**A Toggle Book is part of the step**: `HeroSimState.Toggled` and what it does to Mana are predicted like anything else, and only a press flips one. A Trigger Book isn't: what it's waiting for is other people, so it goes off on the server, and `Charged` is sent as one of the things a client takes as it finds them.

**A place can be inside something**: `ItemPlace.In` is a page in a book or a sentence on a page, as one number like any other, so moving them is `HeroAction.Move`. Only what's written on a page and what a book is called are ever sent to the server (`Write`, `Title`), never an item: a client can't say what it has.

**What a hero carries is the other thing a client does ahead of being told**, as a pack that waits for the server at every click is no pleasure to use. `GameClient` makes each change at once, by the same rules the server will use (`HeroState`, `MerchantTable`), and sends it whatever those rules said, as the server has the last word. Each one carries a running number, which comes back with every gear update and market:

- A gear update is only believed if it has heard everything asked so far. An earlier one says the pack is as it was, and showing that would make things jump back for a moment.
- A market goes by the last thing asked of a table rather than the last thing asked at all, or a move right behind a refused purchase would hold up the one message that puts it right.
- The server answers every one of these with the truth, allowed or not (`ServerPlayer.Heard`). That's the whole of what corrects a client that was wrong, so a client can only be wrong for a moment here too.

Everyone else only ever hears what the server decided. `Dino NetLab pack` holds all of this.

## Adding things

**A new component clients should see**: mark it `[Replicated]`, and put a `NetIdentityComponent` on the entity. That's all. It reaches every client it's relevant to, as the same component type, and goes when it's removed. Fields go unless marked `[NotReplicated]`. `[NetStep(x)]` sets how finely a number is sent, and `[NetSnap]` stops it being blended.

**Something new to draw**: give it a `VisualComponent` with a model name, and teach `CharacterFactory.Resolve` the name.

**Something flying in a straight line**: use `BallisticComponent`, not a moving position. It is sent once, and clients work out the rest, so hundreds cost almost nothing.

**Something coming**: `Combat.Warn` puts a `TelegraphComponent` on the ground and `Combat.Perform` says what a body's doing, as an `ActionComponent`. Both are components and not events, as a player can't be allowed to miss them, and both are moments in ticks, so anyone can pick them up part way through. They're read against a client's own clock: see `ANIMATION.md`.

**A spark, a sound, a number**: mark a struct `[NetEvent]`, send it with `GameServer.Broadcast`, and handle it with `client.Events.On<T>`. Events are for show. They can be missed, so nothing that matters may depend on one: put that in a component.

**Something with a name that changes**: a find is the example. Everything that doesn't need the name goes in the component, and what does comes by a reliable message keyed by a number of its own (`GroundItem`), as replicated strings are a table that only grows. Either can arrive first, so nothing can rely on the order.

**A new rule**: write it as a system under `Server/`. It runs on the server and its results replicate. It must not touch `GameSession`, `Engine` or anything GL.

**A new phrase**: it's data, in `Phrases`, and needs no networking. A book goes over the wire as phrase numbers (`Protocol.WriteBook`), and `Scribe.Fix` makes sense of whatever comes in. `HeroRules.Step` already handles anything on the bar: cost, cooldown, timing and moving. What it does to the world happens in `Combat`, on the server. See `BOOKS.md`.

**A new stat, or one that depends on something**: needs nothing either. Both ends compile the same sheet from the same hero, and `HeroSimState` carries the state and counters that pick which sheet is in force.

### What will break it

- **Reading other entities in `HeroRules.Step`.** A client doesn't know where they really are, so it would guess wrong. This is what body-blocking would need, and it is the classic cause of ARPG desync. Knockback, stuns and slows should be things the server applies and the client hears about, which the correction path already handles.
- **Using anything but `HeroCommand` as input to the step.** Both ends have to start from the same numbers.
- **Adding up a hero's modifiers in a different order on one end.** Floats don't add up the same way in every order, so `HeroBuild` gathers them in a fixed one.
- **Changing a component's fields on one build only.** The schema hash is compared at the door, so two builds refuse each other, with a clear message. So is the build's version (`Protocol.Build`): releases go out often, and different rules with the same components would otherwise connect and disagree about every step. Builds made by hand all count as the same one.
- **Dynamic text in a replicated string field.** Those go through a table that only grows. Use a number, or an event.
- **Changing what's carried on the client any other way than `AskGear`, or on the server without `Heard`.** The first is never put right, and the second leaves the client ignoring every answer from then on.
- **Touching anything on the server from the client's thread** without `ServerHost.Post`.

## What crosses the network

Everything is one message on a connection, reliable or not.

| Message | How | What |
|---|---|---|
| `Commands` | unreliable, every tick | Inputs not yet confirmed, and which snapshots arrived |
| `Snapshot` | unreliable, 30 Hz | Your hero exactly, then differences for what's near you, within one packet |
| `Hello` `Welcome` `WorldReady` `Arrived` | reliable | Joining |
| `HeroUpdate` `Progress` `Notice` `Market` | reliable | Things said once and meant, including talents and what's buffing a hero |
| `Party` | reliable | Who's in the game, by their hero's name, which world each is in and whether it's a land, who's hosting, and how loot goes |
| `GroundItem` | reliable | What a find on the ground is, and that it's gone. Sent to everyone as it drops, and to whoever arrives later |
| `Worn` | reliable | What a hero has on: a seed and traits for each place. Sent to everyone when it changes, and to whoever arrives later. No mesh is ever sent: see `GEAR.md` |

Worlds are never sent: the seed is, with the land's terms (`LandTerms`: its omens, any unique orders, and how many places it asks to be freed), and each machine grows its own. Some omens decide how many stand on the land, so `LandTerms.Shape` has to be what makes the request on both ends. The Haven has no terms and is the same everywhere. Both ends compare a hash of the result. Sites and spawns must match exactly. Ground only has to match closely, as worlds are grown with floating point and the C runtime can round differently from one machine to the next, which at worst costs a correction.

The hero's own data lives on the server during a game and is copied to the client, which is what ends up in that player's save. World progress belongs to the host. The stash is the player's too: it comes in their hello, the server keeps a copy for as long as they're there and runs it by the pack's rules (`Stow`, `Unstow` and `Shift` go through `AskGear` and are answered with the truth), and their own machine keeps it. The climb (`BestLand`, `Offers` and each level's `Rerolls`, deepest first) is in the hero summary, the way opens at a level with as many orders as the table takes (`HeroAction.OpenWay`, through `AskGear`), other lands are bought with `HeroAction.Reroll`, which only the server changes, and what a land meant for it is settled on the server the moment it happens, and sent before any world changes, so that closing the game can't take it back.

## Time

The server runs at a fixed 60 ticks a second. A client's clock runs ahead of it, so each command arrives just before its tick. The server reports how much spare time commands arrive with, and the client speeds up or slows down by up to 5% to keep that small. Everything else is drawn behind the newest snapshot, by a margin measured from how the line actually behaves.

That margin is the most dangerous number in the game, as whatever it learns is how late every blow, health bar and enemy swing is seen. So `InterpolationClock` learns only from the last second or so, and a stall on either end must teach it nothing. A long frame delivers everything sent during it at once, and a server that falls behind has lost that time for good: neither says anything about the line. The first frames in a world can take seconds, so this comes up every time someone plays. When it was got wrong the round trip still read 17 ms and the hero still moved perfectly, with the whole world drawn 3 s in the past. `drawn N ms behind` in the `Net:` line of a scripted run shows it.

An arrow is drawn moving from the past into the player's own moment over its first quarter second, so that when it reaches them it really has. A hero's blow is judged against where enemies were on the attacker's screen, up to a quarter of a second back.

## Inviting friends

**Today: invite codes.** "Travel with friends" in the pause menu opens the game, asks the router to pass the port on by UPnP or NAT-PMP, and gives a code to paste into a chat. It carries the address and a secret, because with no server of ours there's nothing to look a game up in.

Before the port opens, `WindowsFirewall.LetFriendsIn` adds a firewall rule for the game, which is one Yes on a Windows box, once per install. Without it a friend's packets stop at the host's own PC, with no sign of it if firewall notifications are off. Two windows on one PC can't show this, as local traffic skips the firewall. Joining needs no rule. Scripted runs skip it, as they skip the router.

**Meeting is what gets friends in from anywhere** (`Net/Meeting.cs`), with nothing to set on either router, as other games do it. Waiting for a friend to knock can't work for most people: a router lets in only answers to what went out, and a provider sharing one address among many customers (carrier-grade NAT, as Telia does) lets in nothing nobody asked for, UPnP or not. So both ends go out to each other:

1. The host asks a public STUN server (Google's, then Cloudflare's) how the internet sees its port, through the game's own socket (`StunLayer`, a LiteNetLib packet layer, as a second socket would have a mapping of its own), and leaves that and its home addresses in a mailbox, every 20 seconds, which also keeps the router from forgetting the port.
2. A friend's game does the same for itself, leaves word that it's coming, reads where the host is, and knocks at every way to it at once, with the invite's own addresses (`UdpTransport.Join(InviteCode)`), for twelve seconds. The first to get through is kept.
3. The host hears that word and sends to the friend's addresses for ten seconds (`MeetingHost`). Those packets going out are what open the host's router and provider to the friend's knocks coming in, as the friend's going out opens theirs.

The mailbox is ntfy.sh, which is free and takes no account, under a topic made from the invite's secret, and every note is sealed with that secret (AES-GCM), so ntfy holds nothing anyone can read without the invite. It shows a note to readers about a second after it's left, so a friend reads three times before giving up on the host. It needs routers that keep one public port for the game whoever it's sending to ("endpoint-independent mapping"), as nearly all home routers and providers do: Telia's does. Where both ends have one that doesn't (a "symmetric" NAT, some mobile networks), only a relay can help, which this isn't: Steam's is below. Scripted runs never meet, as they never touch the router. `Dino NetLab meet` meets through the real mailbox and STUN server on one machine, and `game` does it with stand-ins for both, but a router can't be put in between on one machine, so getting through one has only been seen between two homes.

UPnP is still asked, as an opened port is one more way in. The panel says what meeting found once it knows (`MeetingHost.Status`), and the router's word only before that or without meeting. The invite code still carries the router's idea of the address and the home address, for a host whose mailbox can't be reached.

**On Steam: use Steam's relay.** This is the real answer, and the reason the transport is an interface. It needs no port forwarding, works behind any NAT and hides players' IP addresses, and it runs on Valve's machines, not yours. Inviting becomes the friends list: nobody sees a code.

1. Add Steamworks.NET to `Dino Defenders`, as LiteNetLib is. The engine stays free of it.
2. Write `Net/SteamTransport.cs` implementing `INetTransport`, about the size of `UdpTransport.cs`. The mapping is direct:
   - Host: `ISteamNetworkingSockets.CreateListenSocketP2P`. Join: `ConnectP2P` with the host's Steam ID.
   - `Send`: `SendMessageToConnection`, with `k_nSteamNetworkingSend_Reliable`, or `Unreliable` plus `NoNagle` for the hot path.
   - `Poll`: `ReceiveMessagesOnPollGroup`, and raise `OnConnected` and `OnDisconnected` from `SteamNetConnectionStatusChangedCallback_t`, accepting incoming connections there.
   - Run `SteamAPI.RunCallbacks` every frame.
3. Hosting: `ISteamMatchmaking.CreateLobby`, then `ISteamFriends.ActivateGameOverlayInviteDialog`.
4. Joining: handle `GameLobbyJoinRequested_t`, and read `+connect_lobby <id>` from the command line for when an invite starts the game. The lobby owner's Steam ID is what to `ConnectP2P` to.

Nothing else changes. That hasn't been built or tested, as it needs a Steam app ID.

Cheating isn't prevented. A host controls their own server, and a player's hero comes from their own save. Clients can't cheat against the rest of the game beyond that, as the server decides what happens, but it isn't a goal.

## When a friend can't get in

Ask both of them for `network.log`, in `%LOCALAPPDATA%\DinoDefenders\logs` beside `game.log`. It keeps every hosting and joining line from one start to the next, trimmed to its last half once it passes a megabyte, where `game.log` only keeps the last two runs, and a friend who couldn't get in has usually started the game again since. Every line starts with a UTC time, so the two machines' lines go side by side: read the joiner's first, then the host's at the same moment. Both start with the build, protocol, build hash and schema hash, which have to match. The labs print these lines too, but only the game writes the file (`GameLog.Network`).

**The joiner's**, in order: what the invite held, how the internet sees this game's port (`the internet sees this game's port as`, or no STUN server answered), `left word in the mailbox`, where the host left word it is (or no word from the host), `knocking at` every way at once, each way that failed and why, and `got through at` one, then `the host let this in`, or `failed after ...: why` and `gave up`, with what that comes to. Once in, `arrived in` a world and `the party is`, which says who's in which world whenever that changes. A code that isn't one, or was cut short, has a line of its own.

**The host's**: `opening to friends` with this machine's addresses, the firewall rule, how the internet sees its port and `left word in the mailbox` (again whenever that changes), `a friend's game ... is on its way from ...` when one leaves word, the router (`answered by`, then `opened UDP port ... Its internet address is ...: public`, `shared by the provider` or `another router's`, or `wouldn't open`, or nothing had opened it after ten seconds), `invite copied` with exactly what the code held and what the panel said, a line for every knock at the port (`let in`, or `turned away` and why, which is also what the friend is told), and the server's `joined from ... into ...`, `went from ... to ...` as heroes step between worlds, `turned away for running another version` and `left`.

| The joiner's log | The host's, at the same time | What's wrong |
|---|---|---|
| `no word from the host in the mailbox` | No `left word in the mailbox`, or `couldn't reach the mailbox` | The mailbox couldn't be reached from one end, or the host is on a build from before meeting |
| `Nobody answered` everywhere, after `left word` | No `is on its way` | The host never heard the friend was coming, so never opened the way: its mailbox line says why |
| `Nobody answered` everywhere | `is on its way` and no knock | Both went out to each other and still nothing got through: a symmetric NAT on at least one end, which needs a relay. Compare the port each STUN line gave with the one the other end's knocks come from, if any |
| `Nobody answered` | A knock, let in | The host heard, but its answers didn't get back to the friend |
| `different versions` | `turned away: ... different versions`, or the server's `another version` | One of them needs to update, through the launcher |
| `Ask for a new one.` | `turned away: That invite is from another game` | The host opened the game again after copying the code: copy a new one |
| The invite holds `internet address none`, and `no word from the host` | The router never opened the port, and the mailbox couldn't be reached | Only friends in the host's home can join. With word from the host, what the invite holds hardly matters |
| `let this in`, `arrived in The Haven`, and the party has the rest in a land | `joined from ... into The Haven, while ...` | It worked. They're in the Haven, and the waygate goes to the rest |

## Testing

`Dino NetLab` runs real servers and clients in one process over a simulated line, on a simulated clock, so a minute passes in seconds and every run goes the same way.

```
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" core     # Replication alone, and fuzzing
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" game     # The whole game, on every kind of line
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" loot     # What's on the ground, with real clients on the other end of a line
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" pack     # What's carried: shown at once, the server has the last word, and nothing lost or doubled on an awful line
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" offering # What's paid at a waygate, and that a friend who turns up later is in the same land
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" combat   # What a fight promises: what's marked is what's hit, on the tick it says
```

Run both after touching anything here. A test of timing has to measure against the server itself, as a client's own idea of how far behind it is can be as wrong as the rest of it, and a stalled client needs its line kept running (`ImpairedTransport.Carry`), or what was sent meanwhile doesn't arrive at once as it really does. To feel a bad line in the real game, or play across two windows on one machine:

```
"Dino Defenders.exe" --host true --secret test
"Dino Defenders.exe" --join 127.0.0.1:27815 --secret test --netsim 100,20,2
```

`--netsim` is round trip in ms, jitter in ms, and percent lost. A window with no keyboard can use `--bot true`. `--dedicated true` runs a server with no window.
