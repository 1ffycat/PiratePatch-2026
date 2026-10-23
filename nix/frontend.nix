{
  lib,
  buildNpmPackage,
  nodejs_24,
}:

buildNpmPackage {
  pname = "piratepatch-frontend";
  version = "1.0.0";

  src = lib.fileset.toSource {
    root = ../PiratePatch.Frontend;
    fileset = ../PiratePatch.Frontend;
  };

  nodejs = nodejs_24;
  # Update with: nix run nixpkgs#prefetch-npm-deps PiratePatch.Frontend/package-lock.json
  npmDepsHash = "sha256-f2tX5I+c93wc6tlO7Ma4Zw7Ql8sAEWasGOn37yOFuWk=";

  doCheck = true;
  checkPhase = ''
    runHook preCheck
    npm run check
    runHook postCheck
  '';

  # The site is static files: serve the output directory as-is.
  installPhase = ''
    runHook preInstall
    cp -r dist $out
    runHook postInstall
  '';

  meta.description = "Static frontend of Cap'n Radare's Patchin' Challenge";
}
