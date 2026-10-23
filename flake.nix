{
  description = "Cap'n Radare's Patchin' Challenge: a pirate-themed binary patching CTF";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs =
    { self, nixpkgs }:
    let
      # The challenge program is x86-64 by design (players are told to use an x86 machine), so this is too.
      system = "x86_64-linux";
      pkgs = nixpkgs.legacyPackages.${system};
    in
    {
      packages.${system} = rec {
        challenge = pkgs.callPackage ./nix/challenge.nix { };
        api = pkgs.callPackage ./nix/api.nix { inherit challenge; };
        frontend = pkgs.callPackage ./nix/frontend.nix { };
        default = api;
      };

      nixosModules = {
        default = self.nixosModules.piratepatch;
        piratepatch = import ./nix/module.nix self;
      };

      overlays.default = final: _prev: {
        piratepatch-api = self.packages.${final.stdenv.hostPlatform.system}.api;
        piratepatch-frontend = self.packages.${final.stdenv.hostPlatform.system}.frontend;
        piratepatch-challenge = self.packages.${final.stdenv.hostPlatform.system}.challenge;
      };

      checks.${system} = {
        inherit (self.packages.${system}) api frontend challenge;
        vm = import ./nix/test.nix { inherit pkgs self; };
        formatting = pkgs.runCommand "check-nix-formatting" { nativeBuildInputs = [ pkgs.nixfmt ]; } ''
          nixfmt --check ${self}/flake.nix ${self}/nix/*.nix
          touch $out
        '';
      };

      formatter.${system} = pkgs.nixfmt;

      devShells.${system}.default = pkgs.mkShell {
        packages = [
          pkgs.dotnetCorePackages.sdk_10_0
          pkgs.nodejs_24
          pkgs.gcc
          pkgs.gnumake
          pkgs.postgresql
          pkgs.nixfmt
          pkgs.radare2
        ];
        DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        shellHook = ''
          echo "PiratePatch dev shell: see README.md ('Development') for the three commands to run."
        '';
      };
    };
}
