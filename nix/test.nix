{ pkgs, self }:

# Boots the real stack (PostgreSQL, the API, nginx), checks the OIDC redirect and uses a development login,
# digs the treasure code out of the downloaded program, and claims the certificate.
pkgs.testers.runNixOSTest {
  name = "piratepatch";

  nodes.server =
    { pkgs, lib, ... }:
    {
      imports = [ self.nixosModules.default ];

      services.piratepatch = {
        enable = true;
        domain = "ahoy.test";
        telegram = {
          botUsername = "piratepatch_test_bot";
          botTokenFile = "/etc/piratepatch-test-token";
          clientId = "123456789";
          clientSecretFile = "/etc/piratepatch-test-secret";
        };
        publicUrl = "http://ahoy.test";
      };

      # Exercise the rest of the stack with the development-only login; OIDC is tested separately.
      systemd.services.piratepatch.environment.ASPNETCORE_ENVIRONMENT = lib.mkForce "Development";
      systemd.services.piratepatch.environment.Site__DevLogin = "true";
      environment.etc."piratepatch-test-secret".text = "test-secret";

      # Plain HTTP, no certificate: the standard nginx options override the module's defaults.
      services.nginx.virtualHosts."ahoy.test" = {
        enableACME = false;
        forceSSL = false;
      };

      # Another service already owns a database on the same PostgreSQL instance (think Immich).
      services.postgresql = {
        ensureDatabases = [ "neighbour" ];
        ensureUsers = [
          {
            name = "neighbour";
            ensureDBOwnership = true;
          }
        ];
      };

      environment.etc."piratepatch-test-token".text = "123456789:TEST-ONLY-token-for-the-vm-test-0000000";
      environment.systemPackages = [
        pkgs.curl
        pkgs.jq
        (pkgs.python3.withPackages (ps: [ ps.cryptography ]))
      ];
      # The offline verification procedure from the README, verbatim.
      environment.etc."verify.py".source = ./verify.py;
    };

  testScript = ''

    import base64, json, shlex

    BASE = "http://127.0.0.1"
    HOST = "-H 'Host: ahoy.test'"
    ORIGIN = "-H 'Origin: http://ahoy.test'"

    def post(path, body, extra=""):
        return server.succeed(
            f"curl -s -o /dev/stderr -w '%{{http_code}}' {HOST} {ORIGIN} {extra} "
            f"-H 'Content-Type: application/json' -d {shlex.quote(json.dumps(body))} {BASE}{path} 2>/tmp/body"
        )

    def session_cookie(headers_file):
        # The cookie is Secure, so curl would not send it back over plain HTTP: pass it by hand.
        value = server.succeed(f"grep -i ^set-cookie: {headers_file} | sed -E 's/^[^:]+: ([^;]+).*/\\1/'").strip()
        return f"-H 'Cookie: {value}'"

    def status_of(cmd):
        return server.succeed(f"curl -s -o /dev/null -w '%{{http_code}}' {cmd}").strip()

    start_all()
    server.wait_for_unit("postgresql.target")
    try:
        # The unit is "active" the moment it forks, so wait until the API actually answers.
        server.wait_for_unit("piratepatch.service")
        server.wait_until_succeeds("curl -sf -H 'Host: ahoy.test' --unix-socket /run/piratepatch/api.sock http://localhost/api/health", timeout=120)
    except Exception:
        print(server.execute("systemctl status piratepatch.service --no-pager; journalctl -u piratepatch --no-pager | tail -n 60")[1])
        raise
    server.wait_for_unit("nginx.service")
    server.wait_for_open_port(80)

    with subtest("the site and its security headers are served"):
        page = server.succeed(f"curl -si {HOST} {BASE}/")
        assert "Patchin" in page, page
        for header in ["content-security-policy", "x-content-type-options: nosniff", "x-frame-options: deny"]:
            assert header in page.lower(), f"missing {header}"
        assert "server: nginx/" not in page.lower(), "nginx version leaked"
        assert status_of(f"{HOST} {BASE}/challenge") == "200", "SPA fallback broken"

    with subtest("the existing PostgreSQL instance is shared, not replaced"):
        databases = server.succeed("sudo -u postgres psql -Atc 'SELECT datname FROM pg_database'").split()
        assert "piratepatch" in databases and "neighbour" in databases, databases
        owner = server.succeed("sudo -u postgres psql -Atc \"SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = 'piratepatch'\"").strip()
        assert owner == "piratepatch", owner

    with subtest("only nginx is reachable from outside"):
        server.fail("ss -ltn | grep -E ':(5000|5001|8080) '")
        print(server.succeed("journalctl -u piratepatch --no-pager | tail -n 25; ls -la /run/piratepatch; ss -lxp | head -n 20"))
        server.succeed("test -S /run/piratepatch/api.sock")

    with subtest("config exposes the public key"):
        config = json.loads(server.succeed(f"curl -s {HOST} {BASE}/api/config"))
        assert config["botUsername"] == "piratepatch_test_bot"
        assert len(config["publicKey"]) == 44
        assert "BEGIN PUBLIC KEY" in server.succeed(f"curl -s {HOST} {BASE}/api/signing-key")

    with subtest("unauthenticated users get nothing"):
        assert status_of(f"{HOST} {BASE}/api/challenge/binary") == "401"
        assert status_of(f"{HOST} {ORIGIN} -X POST -H 'Content-Type: application/json' -d '{{\"code\":\"x\"}}' {BASE}/api/challenge/submit") == "401"

    with subtest("state-changing requests from another origin are refused"):
        code = server.succeed(f"curl -s -o /dev/null -w '%{{http_code}}' {HOST} -H 'Origin: https://evil.example' -X POST -H 'Content-Type: application/json' -d '{{}}' {BASE}/api/auth/logout").strip()
        assert code == "403", code

    with subtest("OIDC starts with PKCE and rejects an unsolicited callback"):
        headers = server.succeed(f"curl -si {HOST} {BASE}/api/auth/telegram")
        assert "oauth.telegram.org/auth" in headers and "code_challenge_method=S256" in headers, headers
        assert status_of(f"{HOST} '{BASE}/api/auth/telegram/callback?code=forged&state=forged'") == "302"
        me = json.loads(server.succeed(f"curl -s {HOST} {BASE}/api/me"))
        assert me["player"] is None, me

    with subtest("a development login starts a session"):
        assert post("/api/auth/dev", {"handle": "blackbeard"}, "-D /tmp/h1").strip() == "200"
        JAR = session_cookie("/tmp/h1")
        me = json.loads(server.succeed(f"curl -s {HOST} {JAR} {BASE}/api/me"))
        assert me["player"]["handle"] == "blackbeard" and me["solve"] is None, me

    with subtest("the personalised program is a real executable that greets the player"):
        server.succeed(f"curl -sf {HOST} {JAR} -o /tmp/challenge {BASE}/api/challenge/binary")
        server.succeed("chmod +x /tmp/challenge && head -c4 /tmp/challenge | grep -q ELF")
        # It exits 1 on purpose: its time bomb has long gone off. That is the first thing to defuse.
        _, greeting = server.execute("${pkgs.glibc}/lib/ld-linux-x86-64.so.2 /tmp/challenge < /dev/null")
        assert "closed since the end of 2024" in greeting, greeting
        assert "Ahoy there, blackbeard!" in greeting, greeting
        # Downloading again yields the very same program.
        server.succeed(f"curl -sf {HOST} {JAR} -o /tmp/challenge2 {BASE}/api/challenge/binary && cmp /tmp/challenge /tmp/challenge2")

    # Stand-in for the player's reverse engineering: the code is XOR-ed with a known key, so find it directly.
    data = base64.b64decode(server.succeed("base64 -w0 /tmp/challenge"))
    key, prefix = b"CAPN_HOOK", b"P1R4T3-C0D3{"
    start = next(i for i in range(len(data) - len(prefix)) if all(data[i + j] ^ key[j % len(key)] == prefix[j] for j in range(len(prefix))))
    end = next(i for i in range(start, len(data)) if data[i] ^ key[(i - start) % len(key)] == ord("}"))
    flag = bytes(data[i] ^ key[(i - start) % len(key)] for i in range(start, end + 1)).decode()
    assert flag.startswith("P1R4T3-C0D3{") and flag.endswith("}"), flag
    with subtest("the documented offline verification accepts the code and rejects a forgery"):
        server.succeed(f"curl -sf {HOST} {BASE}/api/signing-key -o /tmp/signing-key.pem")
        out = server.succeed(f"cd /tmp && python3 /etc/verify.py {shlex.quote(flag)}")
        assert "blackbeard" in out, out
        forged = flag[:30] + ("A" if flag[30] != "A" else "B") + flag[31:]
        server.fail(f"cd /tmp && python3 /etc/verify.py {shlex.quote(forged)}")

    with subtest("wrong and foreign codes are refused, the right one is accepted"):
        tampered = flag[:30] + ("A" if flag[30] != "A" else "B") + flag[31:]
        assert post("/api/challenge/submit", {"code": tampered}, JAR).strip() == "200"
        assert json.loads(server.succeed("cat /tmp/body"))["success"] is False
        assert post("/api/challenge/submit", {"code": "not a code"}, JAR).strip() == "400"

        # A valid code minted for someone else must not work for this player.
        post("/api/auth/dev", {"handle": "anne_bonny"}, "-D /tmp/h2")
        JAR2 = session_cookie("/tmp/h2")
        server.succeed(f"curl -sf {HOST} {JAR2} -o /tmp/challenge_other {BASE}/api/challenge/binary")
        assert post("/api/challenge/submit", {"code": flag}, JAR2).strip() == "200"
        assert json.loads(server.succeed("cat /tmp/body"))["success"] is False, "a code worked for the wrong player"

        assert post("/api/challenge/submit", {"code": flag}, JAR).strip() == "200"
        result = json.loads(server.succeed("cat /tmp/body"))
        assert result["success"] is True and result["solve"]["rank"] == 1, result

        # Submitting again is harmless and does not change the rank.
        post("/api/challenge/submit", {"code": flag}, JAR)
        assert json.loads(server.succeed("cat /tmp/body"))["solve"]["rank"] == 1

    with subtest("the certificate is a PDF and the leaderboard lists the solver"):
        url = result["solve"]["certificateUrl"]
        server.succeed(f"curl -sf {HOST} -o /tmp/cert.pdf {BASE}{url}")
        server.succeed("head -c5 /tmp/cert.pdf | grep -q '%PDF-'")
        assert int(server.succeed("stat -c %s /tmp/cert.pdf")) > 100_000
        raw_board = server.succeed(f"curl -s -w '\\n%{{http_code}}' {HOST} '{BASE}/api/leaderboard?limit=10'")
        print(raw_board)
        board = json.loads(raw_board.rsplit("\n", 1)[0])
        assert board["total"] == 1 and board["entries"][0]["handle"] == "blackbeard", board
        assert status_of(f"{HOST} {BASE}/api/certificates/00000000000000000000") == "404", "unknown certificate"
        # nginx normalises the path and falls back to the SPA; what matters is that no file leaks.
        traversal = server.succeed(f"curl -s {HOST} --path-as-is {BASE}/api/certificates/..%2f..%2f..%2fetc%2fpasswd")
        assert "root:" not in traversal, "path traversal leaked a file"

    with subtest("a flood is throttled instead of served"):
        codes = server.succeed(
            f"for i in $(seq 1 120); do curl -s -o /dev/null -w '%{{http_code}}\\n' {HOST} {BASE}/api/config; done"
        ).split()
        assert "429" in codes, "no request was rate limited"
        assert "200" in codes

    with subtest("the service is sandboxed"):
        print(server.succeed("systemd-analyze security piratepatch.service --no-pager | tail -n 3"))
        score = server.succeed("systemd-analyze security piratepatch.service --no-pager | tail -n1")
        assert "UNSAFE" not in score and "EXPOSED" not in score, score
  '';
}
