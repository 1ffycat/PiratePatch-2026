{
  lib,
  stdenv,
  patchelf,
}:

# The challenge program players download, with its per-player slots still empty (see challenge/challenge.c).
#
# It is run by players on whatever Linux they have, so it must not point into /nix/store, and
# it keeps its symbols and its unoptimised code on purpose: reading it is the challenge.
stdenv.mkDerivation {
  pname = "piratepatch-challenge";
  version = "1";

  src = lib.fileset.toSource {
    root = ../challenge;
    fileset = ../challenge/challenge.c;
  };

  nativeBuildInputs = [ patchelf ];

  hardeningDisable = [ "all" ];

  # No store paths in the binary: not as the interpreter, and not as an rpath string either.
  env.NIX_DONT_SET_RPATH = "1";
  env.NIX_NO_SELF_RPATH = "1";
  dontStrip = true;
  dontPatchELF = true;

  buildPhase = ''
    runHook preBuild
    $CC -O0 -g0 -fPIE -pie -Wall -Wextra -Werror -o challenge challenge.c
    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall

    # Use the distribution's loader and libc instead of the store's.
    patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 challenge

    install -Dm755 challenge $out/share/piratepatch/challenge-template
    runHook postInstall
  '';

  doInstallCheck = true;
  installCheckPhase = ''
    runHook preInstallCheck
    binary=$out/share/piratepatch/challenge-template

    echo "checking the program is portable..."
    test "$(patchelf --print-interpreter $binary)" = /lib64/ld-linux-x86-64.so.2
    if grep -aq /nix/store $binary; then grep -a -o "/nix/store[^[:space:]]*" $binary; echo "store path leaked into the program"; exit 1; fi
    newest=$(objdump -T $binary | grep -o 'GLIBC_[0-9.]*' | sort -Vu | tail -n1)
    echo "needs $newest"
    # Ubuntu 22.04, the oldest distribution worth supporting, ships 2.35.
    test "$(printf '%s\n' "$newest" GLIBC_2.35 | sort -V | tail -n1)" = GLIBC_2.35

    echo "checking the program is readable..."
    nm $binary | grep -q ' T main$'
    nm $binary | grep -q ' T check_the_chronometer$\| t check_the_chronometer$'

    echo "checking the slots the API patches..."
    test "$(grep -a -o '~PIRATEPATCH~V1~' $binary | wc -l)" = 1

    runHook postInstallCheck
  '';

  meta = {
    description = "Cap'n Radare's patching challenge program, before it is personalised";
    platforms = [ "x86_64-linux" ];
  };
}
