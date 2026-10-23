self:
{
  config,
  lib,
  pkgs,
  ...
}:

let
  inherit (lib)
    mkDefault
    mkEnableOption
    mkIf
    mkMerge
    mkOption
    mkPackageOption
    types
    ;

  cfg = config.services.piratepatch;
  flakePackages = self.packages.${pkgs.stdenv.hostPlatform.system};

  socket = "/run/piratepatch/api.sock";
  stateDir = "/var/lib/piratepatch";
  generatedKey = "${stateDir}/signing.key";

  localDatabase = cfg.database.createLocally;

  # Nothing the API does needs the network unless it talks to a remote database or sends admin messages.
  needsNetwork = cfg.telegram.adminChatId != null || !(lib.hasPrefix "/" cfg.database.host);

  secretFileType = types.addCheck types.str (path: !lib.hasPrefix builtins.storeDir path) // {
    description = "path to a file outside the Nix store";
  };
in
{
  options.services.piratepatch = {
    enable = mkEnableOption "Cap'n Radare's Patchin' Challenge, a pirate-themed binary patching CTF";

    package = mkOption {
      type = types.package;
      default = flakePackages.api;
      defaultText = lib.literalExpression "piratepatch.packages.\${system}.api";
      description = "The API package. It bundles the challenge program players download.";
    };

    frontendPackage = mkOption {
      type = types.package;
      default = flakePackages.frontend;
      defaultText = lib.literalExpression "piratepatch.packages.\${system}.frontend";
      description = "The static frontend that nginx serves.";
    };

    user = mkOption {
      type = types.str;
      default = "piratepatch";
      description = "User the API runs as. With a local database it is also the database user and name.";
    };

    group = mkOption {
      type = types.str;
      default = "piratepatch";
      description = "Group the API runs as. nginx joins it to reach the API's socket.";
    };

    domain = mkOption {
      type = types.str;
      example = "ahoy.example.org";
      description = "Domain the site is served on. Also the origin that state-changing requests must come from.";
    };

    publicUrl = mkOption {
      type = types.strMatching "https?://[^/]+";
      default = "https://${cfg.domain}";
      defaultText = lib.literalExpression ''"https://''${config.services.piratepatch.domain}"'';
      description = ''
        The origin visitors use, without a trailing slash. State-changing requests must come from exactly this
        origin, and it is printed on certificates. Change it if TLS is terminated elsewhere or the site is served
        on a port other than 443. The session cookie only works over HTTPS (plain HTTP is for local testing).
      '';
    };

    sessionHours = mkOption {
      type = types.ints.between 1 720;
      default = 12;
      description = "How long a login stays valid.";
    };

    telegram = {
      botUsername = mkOption {
        type = types.str;
        example = "ahoy_ctf_bot";
        description = ''
          Username of the Telegram bot behind the login widget, without the `@`.
          Create the bot with @BotFather and set its domain there with `/setdomain`.
        '';
      };

      botTokenFile = mkOption {
        type = secretFileType;
        example = "/run/secrets/piratepatch-bot-token";
        description = ''
          File containing the bot's token. It verifies Telegram logins and, if `adminChatId` is set,
          sends notifications. Passed to the service as a systemd credential, so it can be root-only.
        '';
      };

      adminChatId = mkOption {
        type = types.nullOr types.int;
        default = null;
        description = "Chat to notify about new players and solves. No notifications when null.";
      };
    };

    signing.privateKeyFile = mkOption {
      type = types.nullOr secretFileType;
      default = null;
      example = "/run/secrets/piratepatch-signing-key";
      description = ''
        File containing the base64 of a 32-byte Ed25519 seed (`openssl rand -base64 32`). Every treasure
        code is signed with it, and the per-player passwords derive from it.

        When null, a key is generated once into `${generatedKey}`. Either way, back it up:
        losing it means certificates can no longer be issued under the same public key.
      '';
    };

    database = {
      createLocally = mkOption {
        type = types.bool;
        default = true;
        description = "Create the PostgreSQL database and user on this machine, and talk to them over the socket.";
      };

      host = mkOption {
        type = types.str;
        default = "/run/postgresql";
        description = "Database host, or the directory of its Unix socket.";
      };

      port = mkOption {
        type = types.port;
        default = 5432;
        description = "Database port.";
      };

      name = mkOption {
        type = types.str;
        default = "piratepatch";
        description = "Database name. Must equal the user's name when the database is created locally.";
      };

      user = mkOption {
        type = types.str;
        default = cfg.user;
        defaultText = lib.literalExpression "config.services.piratepatch.user";
        description = "Database user.";
      };

      passwordFile = mkOption {
        type = types.nullOr secretFileType;
        default = null;
        description = "File containing the database password. Not needed for socket (peer) authentication.";
      };
    };

    nginx = {
      enable = mkOption {
        type = types.bool;
        default = true;
        description = ''
          Add a virtual host for `domain` to `services.nginx`. It is an ordinary virtual host with defaults
          (ACME certificate, HTTPS redirect) you can override like any other:

          ```nix
          services.nginx.virtualHosts."ahoy.example.org" = {
            enableACME = false;
            useACMEHost = "example.org"; # e.g. a wildcard certificate
          };
          ```

          Nothing else of your nginx configuration is touched. Without this option, point your own proxy at
          the API's Unix socket (`${socket}`, group-accessible to `group`) and serve the frontend package yourself.
        '';
      };

      requestsPerSecond = mkOption {
        type = types.ints.positive;
        default = 10;
        description = "Sustained API request rate allowed per client address.";
      };

      burst = mkOption {
        type = types.ints.positive;
        default = 30;
        description = "How many API requests a client address may send in a burst above the sustained rate.";
      };

      maxConnections = mkOption {
        type = types.ints.positive;
        default = 30;
        description = "Simultaneous connections allowed per client address.";
      };
    };
  };

  config = mkIf cfg.enable (mkMerge [
    {
      assertions = [
        {
          assertion = !localDatabase || cfg.database.name == cfg.database.user;
          message = "services.piratepatch.database.name must equal database.user when createLocally is set (ensureDBOwnership).";
        }
        {
          assertion = !localDatabase || cfg.database.user == cfg.user;
          message = "services.piratepatch.database.user must equal services.piratepatch.user when createLocally is set (peer authentication).";
        }
      ];

      users.users.${cfg.user} = {
        isSystemUser = true;
        inherit (cfg) group;
        description = "Cap'n Radare's Patchin' Challenge";
      };
      users.groups.${cfg.group} = { };

      services.postgresql = mkIf localDatabase {
        enable = true;
        ensureDatabases = [ cfg.database.name ];
        ensureUsers = [
          {
            name = cfg.database.user;
            ensureDBOwnership = true;
          }
        ];
      };

      # The key is generated before the API starts, so it can be handed over as a credential like any other secret.
      systemd.services.piratepatch-keygen = mkIf (cfg.signing.privateKeyFile == null) {
        description = "Generate the signing key of Cap'n Radare's Patchin' Challenge";
        before = [ "piratepatch.service" ];
        requiredBy = [ "piratepatch.service" ];
        unitConfig.ConditionPathExists = "!${generatedKey}";
        path = [ pkgs.openssl ];
        script = ''
          umask 0077
          openssl rand -base64 32 > "$STATE_DIRECTORY/signing.key"
        '';
        serviceConfig = {
          Type = "oneshot";
          RemainAfterExit = true;
          User = cfg.user;
          Group = cfg.group;
          StateDirectory = "piratepatch";
          StateDirectoryMode = "0700";
          NoNewPrivileges = true;
          PrivateTmp = true;
          ProtectSystem = "strict";
          ProtectHome = true;
        };
      };

      systemd.services.piratepatch = {
        description = "Cap'n Radare's Patchin' Challenge";
        wantedBy = [ "multi-user.target" ];
        wants = [ "network-online.target" ];
        after = [
          "network-online.target"
        ]
        ++ lib.optional localDatabase "postgresql.target";
        requires = lib.optional localDatabase "postgresql.target";

        environment = {
          ASPNETCORE_ENVIRONMENT = "Production";
          AllowedHosts = cfg.domain;
          Kestrel__Endpoints__Socket__Url = "http://unix:${socket}";
          Site__PublicUrl = cfg.publicUrl;
          Site__SessionHours = toString cfg.sessionHours;
          Site__KeysPath = "${stateDir}/keys";
          Telegram__BotUsername = cfg.telegram.botUsername;
          Database__Host = cfg.database.host;
          Database__Port = toString cfg.database.port;
          Database__Name = cfg.database.name;
          Database__User = cfg.database.user;

          # No debugger or diagnostics endpoints, and the invariant culture is all the site needs.
          DOTNET_EnableDiagnostics = "0";
          DOTNET_gcServer = "0";
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        }
        // lib.optionalAttrs (cfg.telegram.adminChatId != null) {
          Telegram__AdminChatId = toString cfg.telegram.adminChatId;
        };

        serviceConfig = {
          ExecStart = lib.getExe cfg.package;
          Restart = "on-failure";
          RestartSec = "5s";

          User = cfg.user;
          Group = cfg.group;
          StateDirectory = "piratepatch";
          StateDirectoryMode = "0700";
          RuntimeDirectory = "piratepatch";
          RuntimeDirectoryMode = "0750";
          # The socket must be writable by the group, so that nginx can connect to it.
          UMask = "0007";

          LoadCredential = [
            "Telegram__BotToken:${cfg.telegram.botTokenFile}"
            "Signing__PrivateKey:${
              if cfg.signing.privateKeyFile != null then cfg.signing.privateKeyFile else generatedKey
            }"
          ]
          ++ lib.optional (
            cfg.database.passwordFile != null
          ) "Database__Password:${cfg.database.passwordFile}";

          # Sandboxing. The one thing .NET cannot live without is a writable+executable JIT heap.
          CapabilityBoundingSet = "";
          AmbientCapabilities = "";
          NoNewPrivileges = true;
          PrivateTmp = true;
          PrivateDevices = true;
          ProtectSystem = "strict";
          ProtectHome = true;
          ProtectProc = "invisible";
          ProcSubset = "pid";
          ProtectClock = true;
          ProtectControlGroups = true;
          ProtectHostname = true;
          ProtectKernelLogs = true;
          ProtectKernelModules = true;
          ProtectKernelTunables = true;
          LockPersonality = true;
          MemoryDenyWriteExecute = false;
          RestrictAddressFamilies = [
            "AF_UNIX"
          ]
          ++ lib.optionals needsNetwork [
            "AF_INET"
            "AF_INET6"
          ];
          RestrictNamespaces = true;
          RestrictRealtime = true;
          RestrictSUIDSGID = true;
          RemoveIPC = true;
          SystemCallArchitectures = "native";
          SystemCallFilter = [
            "@system-service"
            "~@privileged"
          ];
          IPAddressDeny = mkIf (!needsNetwork) "any";

          # A runaway or flooded API must not take the rest of the machine with it.
          MemoryHigh = mkDefault "384M";
          MemoryMax = mkDefault "768M";
          TasksMax = mkDefault 256;
          CPUQuota = mkDefault "200%";
          LimitNOFILE = mkDefault 4096;
        };
      };
    }

    (mkIf cfg.nginx.enable {
      # nginx reaches the API through its socket.
      users.users.${config.services.nginx.user}.extraGroups = [ cfg.group ];

      services.nginx = {
        enable = true;

        appendHttpConfig = ''
          limit_req_zone $binary_remote_addr zone=piratepatch_api:10m rate=${toString cfg.nginx.requestsPerSecond}r/s;
          limit_conn_zone $binary_remote_addr zone=piratepatch_conn:10m;
        '';

        virtualHosts.${cfg.domain} = {
          # Defaults only: override them with the usual services.nginx.virtualHosts options.
          enableACME = mkDefault true;
          forceSSL = mkDefault true;
          root = cfg.frontendPackage;

          # add_header is not inherited into locations that use it themselves, so none of the locations below do.
          extraConfig = ''
            client_max_body_size 16k;
            client_body_timeout 10s;
            client_header_timeout 10s;
            limit_conn piratepatch_conn ${toString cfg.nginx.maxConnections};
            limit_conn_status 429;
            gzip_static on;

            ${lib.optionalString config.services.nginx.virtualHosts.${cfg.domain}.forceSSL ''
              add_header Strict-Transport-Security "max-age=31536000" always;
            ''}
            add_header X-Content-Type-Options "nosniff" always;
            add_header X-Frame-Options "DENY" always;
            add_header Referrer-Policy "no-referrer" always;
            add_header Permissions-Policy "camera=(), microphone=(), geolocation=(), payment=()" always;
            add_header Cross-Origin-Opener-Policy "same-origin" always;
            # Telegram's login widget is the only third party: its script, and its popup frame.
            add_header Content-Security-Policy "default-src 'none'; script-src 'self' https://telegram.org; style-src 'self' 'unsafe-inline'; font-src 'self'; img-src 'self' data:; connect-src 'self'; frame-src https://oauth.telegram.org; form-action 'self'; base-uri 'none'; frame-ancestors 'none'" always;
          '';

          locations."/" = {
            tryFiles = "$uri $uri/ /index.html";
            extraConfig = "expires epoch;";
          };

          # Vite fingerprints these files, so they can be cached for good.
          locations."/assets/" = {
            tryFiles = "$uri =404";
            extraConfig = "expires 1y;";
          };

          locations."/api/" = {
            # Pass the URI through untouched, and make X-Forwarded-For exactly the client's address:
            # the API's rate limits trust it, so nothing the client sent may survive.
            proxyPass = "http://unix:${socket}";
            recommendedProxySettings = false;
            extraConfig = ''
              limit_req zone=piratepatch_api burst=${toString cfg.nginx.burst} nodelay;
              limit_req_status 429;

              proxy_set_header Host $host;
              proxy_set_header X-Forwarded-For $remote_addr;
              proxy_set_header X-Forwarded-Proto $scheme;
              proxy_set_header Connection "";
              proxy_connect_timeout 3s;
              proxy_read_timeout 20s;
              proxy_send_timeout 20s;
            '';
          };
        };
      };
    })
  ]);
}
