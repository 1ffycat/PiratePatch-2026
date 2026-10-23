// Cap'n Radare's Patchin' Challenge - template program.
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <time.h>
#include <stddef.h>
#include <stdint.h> // For uint8_t, uint32_t

/**************************** DATA TYPES ****************************/
typedef unsigned char BYTE; // 8-bit byte
typedef unsigned int WORD;  // 32-bit word, change to unsigned long for 16-bit machines

typedef struct
{
    BYTE data[64];
    WORD datalen;
    unsigned long long bitlen;
    WORD state[8];
} SHA256_CTX;

/*********************** FUNCTION DECLARATIONS **********************/
void sha256_init(SHA256_CTX *ctx);
void sha256_update(SHA256_CTX *ctx, const BYTE data[], size_t len);
void sha256_final(SHA256_CTX *ctx, BYTE hash[]);

/****************************** MACROS ******************************/
#define ROTLEFT(a, b) (((a) << (b)) | ((a) >> (32 - (b))))
#define ROTRIGHT(a, b) (((a) >> (b)) | ((a) << (32 - (b))))

#define CH(x, y, z) (((x) & (y)) ^ (~(x) & (z)))
#define MAJ(x, y, z) (((x) & (y)) ^ ((x) & (z)) ^ ((y) & (z)))
#define EP0(x) (ROTRIGHT(x, 2) ^ ROTRIGHT(x, 13) ^ ROTRIGHT(x, 22))
#define EP1(x) (ROTRIGHT(x, 6) ^ ROTRIGHT(x, 11) ^ ROTRIGHT(x, 25))
#define SIG0(x) (ROTRIGHT(x, 7) ^ ROTRIGHT(x, 18) ^ ((x) >> 3))
#define SIG1(x) (ROTRIGHT(x, 17) ^ ROTRIGHT(x, 19) ^ ((x) >> 10))

/**************************** VARIABLES *****************************/
static const WORD k[64] = {
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2};

/*********************** FUNCTION DEFINITIONS ***********************/
void sha256_transform(SHA256_CTX *ctx, const BYTE data[])
{
    WORD a, b, c, d, e, f, g, h, i, j, t1, t2, m[64];

    for (i = 0, j = 0; i < 16; ++i, j += 4)
        m[i] = (data[j] << 24) | (data[j + 1] << 16) | (data[j + 2] << 8) | (data[j + 3]);
    for (; i < 64; ++i)
        m[i] = SIG1(m[i - 2]) + m[i - 7] + SIG0(m[i - 15]) + m[i - 16];

    a = ctx->state[0];
    b = ctx->state[1];
    c = ctx->state[2];
    d = ctx->state[3];
    e = ctx->state[4];
    f = ctx->state[5];
    g = ctx->state[6];
    h = ctx->state[7];

    for (i = 0; i < 64; ++i)
    {
        t1 = h + EP1(e) + CH(e, f, g) + k[i] + m[i];
        t2 = EP0(a) + MAJ(a, b, c);
        h = g;
        g = f;
        f = e;
        e = d + t1;
        d = c;
        c = b;
        b = a;
        a = t1 + t2;
    }

    ctx->state[0] += a;
    ctx->state[1] += b;
    ctx->state[2] += c;
    ctx->state[3] += d;
    ctx->state[4] += e;
    ctx->state[5] += f;
    ctx->state[6] += g;
    ctx->state[7] += h;
}

void sha256_init(SHA256_CTX *ctx)
{
    ctx->datalen = 0;
    ctx->bitlen = 0;
    ctx->state[0] = 0x6a09e667;
    ctx->state[1] = 0xbb67ae85;
    ctx->state[2] = 0x3c6ef372;
    ctx->state[3] = 0xa54ff53a;
    ctx->state[4] = 0x510e527f;
    ctx->state[5] = 0x9b05688c;
    ctx->state[6] = 0x1f83d9ab;
    ctx->state[7] = 0x5be0cd19;
}

void sha256_update(SHA256_CTX *ctx, const BYTE data[], size_t len)
{
    WORD i;

    for (i = 0; i < len; ++i)
    {
        ctx->data[ctx->datalen] = data[i];
        ctx->datalen++;
        if (ctx->datalen == 64)
        {
            sha256_transform(ctx, ctx->data);
            ctx->bitlen += 512;
            ctx->datalen = 0;
        }
    }
}

void sha256_final(SHA256_CTX *ctx, BYTE hash[])
{
    WORD i;

    i = ctx->datalen;

    // Pad whatever data is left in the buffer.
    if (ctx->datalen < 56)
    {
        ctx->data[i++] = 0x80;
        while (i < 56)
            ctx->data[i++] = 0x00;
    }
    else
    {
        ctx->data[i++] = 0x80;
        while (i < 64)
            ctx->data[i++] = 0x00;
        sha256_transform(ctx, ctx->data);
        memset(ctx->data, 0, 56);
    }

    // Append to the padding the total message's length in bits and transform.
    ctx->bitlen += ctx->datalen * 8;
    ctx->data[63] = ctx->bitlen;
    ctx->data[62] = ctx->bitlen >> 8;
    ctx->data[61] = ctx->bitlen >> 16;
    ctx->data[60] = ctx->bitlen >> 24;
    ctx->data[59] = ctx->bitlen >> 32;
    ctx->data[58] = ctx->bitlen >> 40;
    ctx->data[57] = ctx->bitlen >> 48;
    ctx->data[56] = ctx->bitlen >> 56;
    sha256_transform(ctx, ctx->data);

    // Since this implementation uses little endian byte ordering and SHA uses big endian,
    // reverse all words when copying the final state to the output hash.
    for (i = 0; i < 4; ++i)
    {
        hash[i] = (ctx->state[0] >> (24 - i * 8)) & 0x000000ff;
        hash[i + 4] = (ctx->state[1] >> (24 - i * 8)) & 0x000000ff;
        hash[i + 8] = (ctx->state[2] >> (24 - i * 8)) & 0x000000ff;
        hash[i + 12] = (ctx->state[3] >> (24 - i * 8)) & 0x000000ff;
        hash[i + 16] = (ctx->state[4] >> (24 - i * 8)) & 0x000000ff;
        hash[i + 20] = (ctx->state[5] >> (24 - i * 8)) & 0x000000ff;
        hash[i + 24] = (ctx->state[6] >> (24 - i * 8)) & 0x000000ff;
        hash[i + 28] = (ctx->state[7] >> (24 - i * 8)) & 0x000000ff;
    }
}

// Helper to convert byte array to hex string
void bytes_to_hex_string(const BYTE bytes[], char hex_string[], size_t num_bytes)
{
    for (size_t i = 0; i < num_bytes; i++)
    {
        sprintf(hex_string + (i * 2), "%02X", bytes[i]); // Use %02X for uppercase
    }
    hex_string[num_bytes * 2] = '\0'; // Null-terminate
}
// --- END SHA256 HASH IMPLEMENTATION ---

/*
 * PER-PLAYER SLOTS
 *
 * The API never compiles anything. It takes the binary built from this file and overwrites
 * this struct in a copy of it. The layout is therefore a contract with
 * PiratePatch.Api/Challenge/ChallengeTemplate.cs - keep both in sync (the asserts below and
 * the build in nix/challenge.nix check it).
 *
 *   magic     16 B  marker the API searches for; overwritten with zeros in the issued binary
 *   name      64 B  player's Telegram handle, NUL-terminated
 *   pw_hash   65 B  uppercase hex SHA-256 of the player's password, NUL-terminated
 *   (pad)      3 B
 *   flag_len   4 B  length of the encrypted flag
 *   flag     192 B  flag XOR-ed with xor_key
 */
#define SLOT_MAGIC "~PIRATEPATCH~V1~" /* exactly 16 characters, no NUL */
#define SLOT_NAME_CAP 64
#define SLOT_HASH_CAP 65
#define SLOT_FLAG_CAP 192

struct slots
{
    char magic[16] __attribute__((nonstring));
    char name[SLOT_NAME_CAP];
    char pw_hash[SLOT_HASH_CAP];
    unsigned int flag_len;
    unsigned char flag[SLOT_FLAG_CAP];
};

_Static_assert(offsetof(struct slots, name) == 16, "slot layout changed");
_Static_assert(offsetof(struct slots, pw_hash) == 80, "slot layout changed");
_Static_assert(offsetof(struct slots, flag_len) == 148, "slot layout changed");
_Static_assert(offsetof(struct slots, flag) == 152, "slot layout changed");
_Static_assert(sizeof(struct slots) == 344, "slot layout changed");

// Not const on purpose: it has to live in .data so the API can find and overwrite it.
struct slots slots = {
    .magic = SLOT_MAGIC,
    .name = "Landlubber",
    .pw_hash = "",
    .flag_len = 0,
};

// Key used to XOR the treasure code. Kept simple for the CTF; it is known to this program.
static const unsigned char xor_key[] = "CAPN_HOOK";

static void reveal_the_treasure(const unsigned char *encrypted_code, unsigned int len)
{
    char *decrypted_code = malloc(len + 1);
    if (!decrypted_code)
    {
        printf("Shiver me timbers! Couldn't allocate memory for the treasure map!\n");
        return;
    }
    size_t key_len = strlen((const char *)xor_key);
    for (unsigned int i = 0; i < len; ++i)
    {
        decrypted_code[i] = encrypted_code[i] ^ xor_key[i % key_len];
    }
    decrypted_code[len] = '\0';

    printf("\n\nBlast it all, ye cracked it! Here be yer treasure code, ye cunning corsair:\n");
    printf("==============================================================\n");
    printf("%s\n", decrypted_code);
    printf("==============================================================\n");
    printf("Now take this code back to the Captain's website to claim yer reward!\n");

    free(decrypted_code);
}

// Time bomb
static int check_the_chronometer(void)
{
    time_t current_time = time(NULL);
    struct tm *time_info = localtime(&current_time);

    int current_year = time_info->tm_year + 1900;
    printf("The ship's chronometer reads: Year %d\n", current_year);

    if (current_year > 2024)
    { // The deadline
        printf("Avast ye! The sands of time have run out! This challenge be closed since the end of 2024!\n");
        printf("No treasure for latecomers, ye scallywag!\n");
        return 0; // Bomb exploded
    }
    printf("Still time on the clock, carry on, matey!\n");
    return 1; // OK
}

int main(void)
{
    char input_passphrase[100];
    BYTE input_hash_bytes[32];     // SHA256 produces 32 bytes
    char input_hash_hex_upper[65]; // 32 bytes * 2 hex chars + null terminator

    printf("Ahoy there, %s!\n", slots.name);
    printf("Welcome aboard 'The Patching Pelican'!\n");
    printf("I be Captain Radare, and I've hidden a secret treasure code in this here program.\n");
    printf("But only a true code-cracking buccaneer can find it!\n\n");

    if (!check_the_chronometer())
    {
        return 1; // Time bomb exploded
    }

    printf("\nTo prove yer worth, speak the secret passphrase, savvy?\n");
    printf("Enter passphrase: ");

    if (fgets(input_passphrase, sizeof(input_passphrase), stdin) == NULL)
    {
        printf("No input? Feelin' lily-livered, are ye?\n");
        return 1;
    }
    input_passphrase[strcspn(input_passphrase, "\n")] = 0;

    SHA256_CTX ctx;
    sha256_init(&ctx);
    sha256_update(&ctx, (const BYTE *)input_passphrase, strlen(input_passphrase));
    sha256_final(&ctx, input_hash_bytes);
    bytes_to_hex_string(input_hash_bytes, input_hash_hex_upper, 32);

    printf("Ye whispered: '%s' (Hash: %s)\n", input_passphrase, input_hash_hex_upper);
    printf("The Captain's Log demands a hash of: %s\n", slots.pw_hash);

    // The critical comparison!
    if (strcmp(input_hash_hex_upper, slots.pw_hash) == 0)
    {
        printf("\nBy Neptune's beard! That be the right passphrase!\n");
        reveal_the_treasure(slots.flag, slots.flag_len);
    }
    else
    {
        printf("\nArrr, that ain't it! Ye be guessin' like a landlubber!\n");
        printf("No treasure for ye today! Walk the plank!\n");
    }

    printf("\nFair winds to ye, %s!\n", slots.name);
    return 0;
}
