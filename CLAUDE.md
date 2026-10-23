# CLAUDE.md

Guidance for working in this repository. See [README.md](README.md) for what the project is and how it is deployed.

## Shape of the project

A single-challenge CTF. `challenge/challenge.c` is built once into a template program; the API (`PiratePatch.Api`)
personalises a copy of it per player by overwriting fixed-size slots, and signs treasure codes with Ed25519. The
frontend (`PiratePatch.Frontend`) is a static Svelte 5 site. `nix/` holds the packages, the NixOS module and a VM test.

## Invariants that are easy to break

- **No player input reaches a shell, a compiler or a file path.** Player values only go into `ChallengeTemplate.Render`
  (validated, copied into fixed slots), PostgreSQL parameters, and the PDF. Keep it that way.
- **The slot layout is a contract** between `challenge/challenge.c` (`struct slots`, with `_Static_assert`s) and
  `PiratePatch.Api/Challenge/ChallengeTemplate.cs`. Change both, plus the tests. `nix/challenge.nix` checks the marker
  occurs exactly once.
- **The challenge program must stay unoptimised, unstripped and portable** (`-O0`, symbols kept, loader
  `/lib64/ld-linux-x86-64.so.2`, no store paths, glibc <= 2.35). Reading it is the challenge. `installCheckPhase` enforces this.
- **No hints in the public copy beyond the opt-in ones** in `Hints.svelte`. The project is meant to stay hard.
- **Flag format is versioned and signed**: `FlagCodec` and `frontend/src/lib/seal.ts` implement the same format, which the
  README documents for offline verification. Changing it invalidates every issued certificate.
- **Secrets never live in the repo.** They arrive through systemd credentials (`Telegram__BotToken`,
  `Signing__PrivateKey`, `Database__Password`) read by `AddKeyPerFile`. `appsettings.Development.json` holds throwaway
  placeholders only and is not read in production.
- **The API trusts `X-Forwarded-For`** (rate limits key on it) because only nginx can reach its Unix socket and nginx
  overwrites the header. Don't expose the socket or add a TCP listener without revisiting `ForwardedHeadersOptions`.

## Commands

```sh
dotnet test                                   # unit tests
npm --prefix PiratePatch.Frontend run check   # svelte-check
nix flake check                               # builds everything, formatting, and the VM test
nix fmt
```

Flakes only see git-tracked files: `git add` new files before running nix. After changing NuGet or npm dependencies,
refresh `nix/api-deps.json` / the `npmDepsHash` (commands in the README).

## Conventions

- C# 14 / .NET 10, nullable and warnings-as-errors on; vulnerability-audit warnings are surfaced, not fatal.
- Minimal APIs with `TypedResults`; request validation via DataAnnotations and `AddValidation()`.
- Options classes live in `Configuration/` and are validated on start. Prefer failing at startup over failing on a request.
- Frontend: Svelte 5 runes (`$state`, `$derived`, `$effect`), snippets and `{@attach}`; no UI framework, no router library.
  Fonts are self-hosted; the only third-party origin is Telegram's login widget (and the CSP says so).
- Nix: `nixfmt`, module options documented, hardening via systemd, secrets as `*File` options.
- Pirate voice in user-facing copy; plain language everywhere else.
