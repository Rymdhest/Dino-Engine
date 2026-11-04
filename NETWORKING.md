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

The folders enforce this. `Dino Defenders/Server/` decides everything and touches nothing of the GPU. `Client/` draws and predicts. `Gameplay/` is what both share. `Dino Engine/Networking/` is the machinery, and knows nothing about the game.

## Why it doesn't desync

Other ARPGs desync because the client keeps its own running idea of the world and has to be right about it. Here it doesn't, and three rules keep it that way.

1. **Clients don't simulate the world.** Enemies, other heroes, loot and arrows are drawn from what the server last said, a fraction of a second in the past, blended between two real states. There is nothing to drift.
2. **A client only ever predicts its own hero**, by running `HeroRules.Step`, the same code on the same numbers as the server. Each snapshot says what the server got for a tick, and the client checks that against what it predicted. If they differ, the server is right: the hero goes back and every command since is run again. A client can be wrong for a moment. It can't stay wrong.
3. **Hero movement depends only on the command, the hero's stats and the fixed shape of the world.** Never on other characters. That is why prediction is almost always exact, and it is the rule most likely to be broken by accident: see below.

World state is never sent as "this changed". Snapshots are differences from a state the client has *confirmed holding*, so a lost packet is simply covered by the next one. Nothing depends on any one packet arriving, including ghosts appearing and leaving.

## Adding things

**A new component clients should see**: mark it `[Replicated]`, and put a `NetIdentityComponent` on the entity. That's all. It reaches every client it's relevant to, as the same component type, and goes when it's removed. Fields go unless marked `[NotReplicated]`. `[NetStep(x)]` sets how finely a number is sent, and `[NetSnap]` stops it being blended.

**Something new to draw**: give it a `VisualComponent` with a model name, and teach `CharacterFactory.Resolve` the name.

**Something flying in a straight line**: use `BallisticComponent`, not a moving position. It is sent once, and clients work out the rest, so hundreds cost almost nothing.

**A spark, a sound, a number**: mark a struct `[NetEvent]`, send it with `GameServer.Broadcast`, and handle it with `client.Events.On<T>`. Events are for show. They can be missed, so nothing that matters may depend on one: put that in a component.

**A new rule**: write it as a system under `Server/`. It runs on the server and its results replicate. It must not touch `GameSession`, `Engine` or anything GL.

**A new hero ability**: add it to `HeroRules.Step` if it affects movement or cooldowns, and to `GameServer.Act` for what it does to the world.

### What will break it

- **Reading other entities in `HeroRules.Step`.** A client doesn't know where they really are, so it would guess wrong. This is what body-blocking would need, and it is the classic cause of ARPG desync. Knockback, stuns and slows should be things the server applies and the client hears about, which the correction path already handles.
- **Using anything but `HeroCommand` as input to the step.** Both ends have to start from the same numbers.
- **Changing a component's fields on one build only.** The schema hash is compared at the door, so two builds refuse each other, with a clear message.
- **Dynamic text in a replicated string field.** Those go through a table that only grows. Use a number, or an event.
- **Touching anything on the server from the client's thread** without `ServerHost.Post`.

## What crosses the network

Everything is one message on a connection, reliable or not.

| Message | How | What |
|---|---|---|
| `Commands` | unreliable, every tick | Inputs not yet confirmed, and which snapshots arrived |
| `Snapshot` | unreliable, 30 Hz | Your hero exactly, then differences for what's near you, within one packet |
| `Hello` `Welcome` `WorldReady` `Arrived` | reliable | Joining |
| `HeroUpdate` `Progress` `Notice` `Market` | reliable | Things said once and meant |

Worlds are never sent: the seed is, and each machine grows its own. Both ends compare a hash of the result. Sites and spawns must match exactly. Ground only has to match closely, as worlds are grown with floating point and the C runtime can round differently from one machine to the next, which at worst costs a correction.

The hero's own data lives on the server during a game and is copied to the client, which is what ends up in that player's save. World progress belongs to the host.

## Time

The server runs at a fixed 60 ticks a second. A client's clock runs ahead of it, so each command arrives just before its tick. The server reports how much spare time commands arrive with, and the client speeds up or slows down by up to 5% to keep that small. Everything else is drawn behind the newest snapshot, by a margin measured from how the line actually behaves.

An arrow is drawn moving from the past into the player's own moment over its first quarter second, so that when it reaches them it really has. A hero's blow is judged against where enemies were on the attacker's screen, up to a quarter of a second back.

## Inviting friends

**Today: invite codes.** "Travel with friends" in the pause menu opens the game, asks the router to pass the port on by UPnP or NAT-PMP, and gives a code to paste into a chat. It carries the address and a secret, because with no server of ours there's nothing to look a game up in.

This works for friends on the same network, and across the internet when the router allows UPnP, as that is also how the game learns its public address. It can't work behind carrier-grade NAT, which some providers use, and the panel says so when it can tell. Without UPnP the code only holds the local address: a port forwarded by hand isn't enough yet, as nothing puts the public address into the code. Steam makes all of this go away.

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

## Testing

`Dino NetLab` runs real servers and clients in one process over a simulated line, on a simulated clock, so a minute passes in seconds and every run goes the same way.

```
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" core     # Replication alone, and fuzzing
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" game     # The whole game, on every kind of line
```

Run both after touching anything here. To feel a bad line in the real game, or play across two windows on one machine:

```
"Dino Defenders.exe" --host true --secret test
"Dino Defenders.exe" --join 127.0.0.1:27815 --secret test --netsim 100,20,2
```

`--netsim` is round trip in ms, jitter in ms, and percent lost. A window with no keyboard can use `--bot true`. `--dedicated true` runs a server with no window.
