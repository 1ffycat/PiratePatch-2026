{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  challenge,
}:

buildDotnetModule {
  pname = "piratepatch-api";
  version = "1.0.0";

  src = lib.fileset.toSource {
    root = ../.;
    fileset = lib.fileset.unions [
      ../Directory.Build.props
      ../.editorconfig
      ../PiratePatch.Api
      ../PiratePatch.Api.Tests
    ];
  };

  projectFile = "PiratePatch.Api/PiratePatch.Api.csproj";
  testProjectFile = "PiratePatch.Api.Tests/PiratePatch.Api.Tests.csproj";
  # Regenerate after changing packages: nix build .#api.passthru.fetch-deps && ./result nix/api-deps.json
  nugetDeps = ./api-deps.json;

  dotnet-sdk = dotnetCorePackages.sdk_10_0;
  dotnet-runtime = dotnetCorePackages.aspnetcore_10_0;
  executables = [ "PiratePatch.Api" ];

  doCheck = true;

  # The template lives in the store; the API finds it through configuration (Assets:ChallengeTemplate).
  makeWrapperArgs = [
    "--set-default"
    "Assets__ChallengeTemplate"
    "${challenge}/share/piratepatch/challenge-template"
  ];

  meta = {
    description = "API of Cap'n Radare's Patchin' Challenge";
    mainProgram = "PiratePatch.Api";
    platforms = [ "x86_64-linux" ];
  };
}
