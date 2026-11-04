# Launcher

How Dino Defenders gets from this repository to the people testing it, and what to do so that keeps working.

## The one idea

**A release is a folder of files on a web server, and a signature that says it's real.** There's no server of ours. The launcher fetches one small signed file, makes the game on disk match what it says, and lights the Play button. Installing, updating and repairing are the same operation, so anything broken on a tester's machine fixes itself the next time they open it.

```
  you                         GitHub Releases                    a tester
  release.cmd  ---------->    manifest.signed   <-------------   DinoDefendersLauncher.exe
  builds, starts it,          game.zip     (1 MB)                checks the signature,
  signs, uploads              runtime.zip  (33 MB, once)         fetches what changed,
                              launcher                           swaps it in, Play
```

## Releasing

Commit, then run `release.cmd`, or:

```
dotnet run --project "Dino Release" -c Release -- publish
```

It takes under a minute. It refuses uncommitted changes, builds a self-contained game, **starts the build and plays a few seconds of it**, signs it, uploads it and then looks at the result as a stranger would. Nothing is visible until the last step. Testers get it the next time they open the launcher.

| | |
|---|---|
| `publish --notes "text"` | Say what changed. Otherwise the notes are the commit subjects since the last release. |
| `publish --dry-run` | Everything except the upload. The result is in `out/release/`. |
| `publish --version 0.2.0` | Jump to a version. Otherwise the last number counts up by itself. |
| `publish --allow-dirty` | Release uncommitted work. The manifest records that it was. |
| `rollback --to 0.1.7` | Send everyone back to an older build, as a new release. |
| `status` | What's out there now. |

The GitHub token is the one git already pushes with, so there's nothing to set up. Set `GITHUB_TOKEN` to use another; it needs `contents: write`.

**A bad release:** run `rollback`. Launchers follow the newest release whatever is in it, so this reaches everyone as fast as an update does. Never delete a release to undo one: anyone who already has it would ignore the older one as stale, and stay on the bad build. Never delete a `runtime-*` release at all, as every release after it downloads from there.

## Testers

Send them this link. It never changes, and always gives the newest launcher:

```
https://github.com/Rymdhest/Dino-Engine/releases/latest/download/DinoDefendersLauncher.exe
```

They run it once. It installs itself, with no administrator rights, adds a desktop shortcut, and downloads the game. After that they open the shortcut and press Play. It needs Windows 10 or 11 and nothing else: the launcher uses the .NET Framework that comes with Windows, and the game brings its own runtime.

**Windows will warn them once**, because the launcher isn't code signed: *Windows protected your PC*, then **More info**, then **Run anyway**. Browsers may also say the download isn't commonly downloaded. It only happens for the file they downloaded. Updates arrive through the launcher, so they aren't marked as downloads and never warn. This is also why the launcher is kept small and changes rarely: a file that stays the same builds up reputation with SmartScreen and antivirus, and the game's files never go through either check.

If a tester has **Smart App Control** on, there's no Run anyway button. It blocks unsigned programs outright, so they would have to turn it off under Windows Security, App & browser control. Only a code signing certificate fixes this for them. If an antivirus deletes something, the launcher puts it back; if that keeps happening, report the file as a false positive to its maker.

**When something goes wrong** they press **Open logs** and send `game.log`, which has the build, the GPU and driver, and the crash if there was one. `game.previous.log` is the run before. Saves are in `%APPDATA%\DinoDefenders`, and the launcher copies the save to `backups` there before any update, keeping the last five, because a build can misread a save written by an older one. Uninstalling is under Installed Apps, and leaves saves alone.

## How it works

**The feed** is the only thing a launcher has to know the address of: `Feed.Url`. It's GitHub Releases because that's free, has no bandwidth limit, needs nothing run, and `releases/latest/download/<file>` is a stable address. Anything serving the same paths would do.

**A build is split into two packs.** `game.zip` is everything from this repository and changes every time. `runtime.zip` is .NET and the libraries, and only changes when they do, so it goes up once under its own `runtime-<hash>` tag, as a prerelease, and every release after it points there. `Publisher.PackOf` decides which is which. Changing .NET version or a NuGet package needs nothing from you: the hash changes and it goes up by itself.

**`manifest.signed`** lists every file with its hash, which pack it's in, and which launcher goes with it. Its first line is the signature of the rest. The hashes vouch for the packs and files, so that one signature covers everything, and it's checked before anything in the file is read. GitHub being compromised, or your token being stolen, can't send testers anything: only the key can.

**Updating** (`GameInstall`): hash every file, fetch the packs holding wrong ones, build a complete new folder beside the old, hash that too, and swap the two with two renames. Those renames are the only moment anything changes. A crash at any point leaves the old game or the new one, and `Recover` sorts out which. The folder keeps the same path for good, as Windows Firewall remembers a program by its path, and a folder per version would ask again after every update.

**The launcher replaces itself** (`LauncherUpdate`) before anything else, by renaming itself aside and starting the new one. This is the one update that can't be repaired afterwards, so the new one has to prove it works by opening its window, within 30 seconds. If it doesn't, the old one puts itself back, remembers never to try that build again, and carries on. A release with a broken launcher costs nobody anything.

**Nothing stops someone playing what they have.** No connection, a bad release, a full disk or a failed update all end with the installed game playable and a sentence saying what happened.

## The signing key

`%USERPROFILE%\.dino-release\signing-key.pem`, and nowhere near the repository. The public half is compiled into the launcher, in `Feed.PublicKey`.

**Back it up now, somewhere like a password manager.** To release from another machine, copy it there. If it's lost, launchers already out there can never be updated again: you would make a new key with `keygen`, and every tester would have to download the launcher by hand once. If it leaks, do the same, soon, as whoever has it can send testers anything. `keygen` won't replace a key that exists, so delete the old one yourself if you really mean to.

## What will break it

- **Anything varying from build to build in the launcher.** Launchers replace themselves when the hash differs, so a timestamp or commit in the exe would send every tester a new launcher with every release, and throw away its reputation. It's why the project turns off PDBs and the source revision. Same source, same bytes: check by rebuilding and comparing. That includes line endings in anything embedded as it stands: `.gitattributes` pins `app.manifest` for this reason, as git otherwise gives a checkout different bytes from the working folder. To check, build a fresh clone and compare the hash.
- **Changing what `schema` or `launcher` mean in the manifest.** Launchers read those forever. Everything else can change, as long as `Manifest.CurrentSchema` goes up: old launchers replace themselves first, and leave what they can't read alone.
- **Writing into the game's folder from the game.** It's replaced whole by every update, and anything the manifest doesn't list goes with it. Keep to `%APPDATA%` and `%LOCALAPPDATA%`.
- **Naming a file `manifest.signed` in the build**, which is the name the installed manifest is kept under. Or anything else that needs renaming for GitHub: asset names stay plain letters, digits, dots and dashes.
- **Trimming the build.** The game finds components by reflection. It would go 33 MB down, and fail at runtime.
- **Releasing without thinking about saves.** Testers carry theirs across builds. A new field is fine. Changing a field's type makes the game start a new journey, and the only copy of the old hero is then in `backups`.

## Testing it

`Dino LaunchLab` tests all of this with no real network, and touches nothing outside a temporary folder:

- `core`: the update logic against a server that misbehaves on purpose, with cut connections, corruption, stalls, forged releases, power cuts at every step, locked files and full disks.
- `release`: the real `Dino Release` against a stand-in for GitHub that holds it to the documented API, then the launcher's code reading what it made on its own framework.
- `live`: the real exe, with real windows and real self-replacement. It opens windows for about a minute and needs the real key, to sign for a local feed.

Run them after any change to the launcher or release tool: `"Dino LaunchLab/bin/Release/net48/Dino LaunchLab.exe" core`, and `release` and `live`. To try a launcher by hand against a local feed:

```
dotnet run --project "Dino Release" -c Release -- publish --folder feed --allow-dirty
"Dino LaunchLab/bin/Release/net48/Dino LaunchLab.exe" serve feed
DinoDefendersLauncher.exe --root C:\Temp\try --feed http://127.0.0.1:<port>/
```

`--root` keeps it away from a real install, and leaves shortcuts and the registry alone. Pressing Play there still uses your real save.

## Moving somewhere else

**To another host:** copy the feed there (`publish --folder` lays one out), change `Feed.Url`, and release once from GitHub. Every launcher replaces itself with one that looks in the new place.

**To Steam:** the game doesn't know the launcher exists, so `out/release/<version>/game` is what goes in a depot, and Steam takes over updating. Two things change on the way. `OutputType` in `Dino Defenders.csproj` should become `WinExe`, or Steam players see a console window, which the launcher only hides by starting it hidden; `--dedicated` then needs `AllocConsole`, or a build of its own. And the launcher can stay for anyone who wants the game without Steam: it's the same build.

**Code signing,** if the warning ever matters more than the cost: sign `DinoDefendersLauncher.exe` after `BuildLauncher` and everything else stays as it is. A certificate doesn't remove SmartScreen's warning at once, as reputation still has to build. Azure Artifact Signing is cheap, but it's for organizations in the USA, Canada, the EU and the UK, and individuals only in the USA and Canada. Elsewhere, an individual needs an OV certificate from a certificate authority.
