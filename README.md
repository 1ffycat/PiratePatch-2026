# Cap'n Radare's Patchin' Challenge

A tiny, pirate-themed reverse-engineering CTF. Every player downloads a small Linux program built just for them. It
guards a treasure code behind a password nobody knows and a clock that has already run out. Make it talk, submit the
code, and get a signed certificate and a spot in the Hall of Fame.

It started as the hands-on part of a webinar on binary patching and is kept here as an archive. The site explains the
game to newcomers, without giving anything away, and keeps optional hints behind a button.

- **Backend:** ASP.NET Core (.NET 10) minimal API, PostgreSQL, PDFsharp
- **Frontend:** Svelte 5 + TypeScript + Vite, a static site with no third-party requests except Telegram's login widget
- **Challenge:** one C file, compiled once at build time
- **Deployment:** a Nix flake with a NixOS module (nginx, PostgreSQL, systemd sandboxing, rate limits)

## How it works

```
 browser ──► nginx ──► Unix socket ──► API (.NET) ──► PostgreSQL
              │                          │
              └─ static frontend         └─ patches a prebuilt template program per player
```

1. A player signs in with the **Telegram Login Widget**. The API verifies Telegram's HMAC; only a public `@username`
   and the Telegram id are kept.
2. `GET /api/challenge/binary` returns **their** copy of the challenge program. Nothing is compiled: the API copies
   the template built from [challenge/challenge.c](challenge/challenge.c) and overwrites a fixed-size block of slots in it
   (name, password hash, encrypted treasure code). See [ChallengeTemplate.cs](PiratePatch.Api/Challenge/ChallengeTemplate.cs).
   The password is derived from the server key, so downloading again gives the identical program.
3. The treasure code is `P1R4T3-C0D3{base64url(payload ‖ signature)}`: the payload is the player's Telegram id and
   handle, signed with **Ed25519**. A code is therefore bound to one player and can be verified by anyone who has the
   public key, with or without this service ([below](#verifying-a-code-without-the-site)).
4. `POST /api/challenge/submit` checks the signature and that the code belongs to the logged-in player, records the solve
   in PostgreSQL, and notifies the admin chat if configured.
5. The certificate PDF is rendered on demand from the stored solve ([template.pdf](PiratePatch.Api/Assets/template.pdf)).

PostgreSQL holds players, solves (which drive the leaderboard and the rank), and nothing else. There is no object
storage: everything else is derived from the signing key.

## Deploying on NixOS

Add the flake and the module:

```nix
{
  inputs.piratepatch.url = "git+https://git.example.org/you/PiratePatch";

  outputs = { nixpkgs, piratepatch, ... }: {
    nixosConfigurations.server = nixpkgs.lib.nixosSystem {
      system = "x86_64-linux";
      modules = [
        piratepatch.nixosModules.default
        {
          services.piratepatch = {
            enable = true;
            domain = "ahoy.example.org";
            telegram = {
              botUsername = "ahoy_ctf_bot";
              botTokenFile = "/run/secrets/piratepatch-bot-token"; # sops-nix, agenix, ...
              adminChatId = 123456789; # optional: new players and solves are sent here
            };
          };
        }
      ];
    };
  };
}
```

That alone gives you: a local PostgreSQL database (peer authentication over its socket), the API behind a Unix socket, an
nginx virtual host with ACME and the security headers, and an auto-generated signing key.

Before the first start, create the bot with [@BotFather](https://t.me/BotFather) and run `/setdomain` for it with your domain;
the login widget refuses to work otherwise.

### Options worth knowing

| Option | Purpose |
| --- | --- |
| `signing.privateKeyFile` | Supply your own Ed25519 seed (`openssl rand -base64 32`) instead of the generated `/var/lib/piratepatch/signing.key`. |
| `database.createLocally` / `host` / `passwordFile` | Use an existing or remote PostgreSQL instead. |
| `nginx.enable`, `nginx.enableACME`, `nginx.forceSSL` | Manage the virtual host yourself, or run without TLS for local testing. |
| `nginx.requestsPerSecond` / `burst` / `maxConnections` | Per-client-address limits at the edge. |
| `sessionHours` | Login lifetime. |

Everything is documented in [nix/module.nix](nix/module.nix).

### Back up the signing key

`/var/lib/piratepatch/signing.key` (or your own file) signs every treasure code. If you lose it, certificates already
issued stay verifiable against the old public key, but new ones cannot be issued under it and existing players' programs
change. Keep a copy of the key, and of the public key (`/api/signing-key`).

### Protecting the machine

The service is built so that a flood or a bug stays contained:

- No compiler and no player-controlled input in any command or file path. Player values are validated twice and only
  ever copied into fixed-size slots.
- Rate limits at three layers: nginx (`limit_req`, `limit_conn`), a global concurrency cap and per-address budgets in the
  API, and per-player budgets on the downloading and submitting endpoints.
- The API runs unprivileged with no capabilities, a read-only file system, a syscall filter, no network access unless
  admin notifications or a remote database need it, and `MemoryMax`/`CPUQuota`/`TasksMax` limits.
- Kestrel listens on a Unix socket, so there is no port to hit directly.
- CSRF: state-changing requests must carry the site's own `Origin`; the site is single-origin so there is no CORS.
- Cookies are `HttpOnly`, `Secure`, `SameSite=Lax` and `__Host-` prefixed.

For a public deployment on a machine that also hosts other things, consider putting Cloudflare (or similar) in front. If
you do, tell nginx the real client address (`set_real_ip_from` with their ranges and `real_ip_header CF-Connecting-IP`),
or all visitors will share one rate-limit bucket.

## Verifying a code without the site

Codes are plain Ed25519 signatures, so they stay verifiable after the site is gone as long as you kept the public key.
The signed message is `"piratepatch/flag/v1\0"` followed by the payload; the payload is one version byte (`1`), the
Telegram id as a big-endian signed 64-bit integer, and the ASCII handle.

```python
import base64, sys
from cryptography.hazmat.primitives.serialization import load_pem_public_key

code = sys.argv[1]  # P1R4T3-C0D3{...}
raw = base64.urlsafe_b64decode(code[len("P1R4T3-C0D3{"):-1] + "==")
payload, signature = raw[:-64], raw[-64:]

load_pem_public_key(open("signing-key.pem", "rb").read()).verify(signature, b"piratepatch/flag/v1\0" + payload)  # raises if forged
print("genuine:", int.from_bytes(payload[1:9], "big", signed=True), payload[9:].decode())
```

The in-browser inspector on the site's *Inspect a seal* page does the same using WebCrypto.

## Development

```sh
nix develop                                   # .NET 10, Node 24, gcc, PostgreSQL, radare2

make -C challenge                             # builds challenge/build/challenge (the template)
createdb piratepatch                          # a local PostgreSQL; see appsettings.Development.json
dotnet run --project PiratePatch.Api          # http://localhost:5001, dev login enabled
npm --prefix PiratePatch.Frontend run dev     # http://localhost:5173, proxies /api to the API
```

In the Development environment `appsettings.Development.json` supplies throwaway placeholders (a fake bot token and an
all-zero signing key) and enables `POST /api/auth/dev`, which logs in as any handle without Telegram. Neither exists in
production.

```sh
dotnet test                                   # unit tests
npm --prefix PiratePatch.Frontend run check   # svelte-check
nix flake check                               # packages, formatting, and the NixOS VM test
nix fmt                                       # format the Nix files
```

The VM test boots PostgreSQL, the API and nginx, logs in with a correctly signed Telegram payload, extracts the treasure
code from the downloaded program, claims the certificate, and checks rate limiting and sandboxing.

After changing NuGet packages: `nix build .#api.passthru.fetch-deps && ./result nix/api-deps.json`. After changing npm
packages: `nix run nixpkgs#prefetch-npm-deps PiratePatch.Frontend/package-lock.json` and update `nix/frontend.nix`.

## Layout

| Path | What |
| --- | --- |
| `challenge/` | The C program players receive, and the slot layout it shares with the API |
| `PiratePatch.Api/` | The API: `Auth`, `Challenge`, `Crypto`, `Players` (PostgreSQL), `Certificates`, `Endpoints`, `Hosting` |
| `PiratePatch.Api.Tests/` | Unit tests for the parts where a mistake would hurt |
| `PiratePatch.Frontend/` | The Svelte site |
| `nix/` | Packages, the NixOS module, and the VM test |

## Limitations

- The challenge program is x86-64 only, and so is the flake.
- The treasure code is only XOR-obfuscated inside the program (that is the design: the intended solution is patching, not
  extraction), so a player could also pull it out statically. It is still bound to them.
- Telegram accounts need a public username.
