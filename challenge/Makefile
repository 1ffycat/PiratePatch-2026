# Local build of the challenge template for development. Production builds it with Nix (nix/challenge.nix),
# which also makes it portable; this one links against the local libc, which is fine for trying things out.
CFLAGS ?= -O0 -g0 -fPIE -pie -Wall -Wextra -Werror

build/challenge: challenge.c
	mkdir -p build
	$(CC) $(CFLAGS) -o $@ challenge.c

.PHONY: clean
clean:
	rm -rf build
