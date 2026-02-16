# Authentication Protocol — WoW 1.12.1 (Build 5875)

> Parsed from MaNGOS `realmd/AuthSocket.cpp`, `AuthCodes.h`, `AuthPackets.h`, `SRP6.cpp`, `SRP6.h`.

## Overview

The auth (realmd) protocol uses **TCP on port 3724** with a custom binary protocol (no encryption).
It implements **SRP6** (Secure Remote Password) for zero-knowledge password authentication.

The flow:
1. Client → Server: `CMD_AUTH_LOGON_CHALLENGE` (username + client info)
2. Server → Client: Challenge response (SRP6 public key B, salt, generator, modulus)
3. Client → Server: `CMD_AUTH_LOGON_PROOF` (SRP6 proof A, M1)
4. Server → Client: Proof response (M2 verification)
5. Client → Server: `CMD_REALM_LIST` (request realm list)
6. Server → Client: Realm list

## Auth Commands (eAuthCmd)

| Command | Value | Direction | Description |
|---------|-------|-----------|-------------|
| `CMD_AUTH_LOGON_CHALLENGE` | `0x00` | C→S | Initial login with username |
| `CMD_AUTH_LOGON_PROOF` | `0x01` | C→S | SRP6 proof (A, M1) |
| `CMD_AUTH_RECONNECT_CHALLENGE` | `0x02` | C→S | Reconnect attempt |
| `CMD_AUTH_RECONNECT_PROOF` | `0x03` | C→S | Reconnect proof |
| `CMD_REALM_LIST` | `0x10` | C→S | Request realm list |
| `CMD_XFER_INITIATE` | `0x30` | S→C | Patch transfer init |
| `CMD_XFER_DATA` | `0x31` | S→C | Patch data chunk |
| `CMD_XFER_ACCEPT` | `0x32` | C→S | Accept patch |
| `CMD_XFER_RESUME` | `0x33` | C→S | Resume patch |
| `CMD_XFER_CANCEL` | `0x34` | C→S | Cancel patch |

## Auth Result Codes (AuthResult)

| Code | Value | Description |
|------|-------|-------------|
| `WOW_SUCCESS` | `0x00` | Success |
| `WOW_FAIL_BANNED` | `0x03` | Account banned |
| `WOW_FAIL_UNKNOWN_ACCOUNT` | `0x04` | Unknown account |
| `WOW_FAIL_INCORRECT_PASSWORD` | `0x05` | Wrong password |
| `WOW_FAIL_ALREADY_ONLINE` | `0x06` | Already logged in |
| `WOW_FAIL_NO_TIME` | `0x07` | No game time |
| `WOW_FAIL_DB_BUSY` | `0x08` | DB busy |
| `WOW_FAIL_VERSION_INVALID` | `0x09` | Wrong client version |
| `WOW_FAIL_VERSION_UPDATE` | `0x0A` | Version update needed |
| `WOW_FAIL_SUSPENDED` | `0x0C` | Account suspended |
| `WOW_FAIL_PARENTCONTROL` | `0x0F` | Parental controls |
| `WOW_SUCCESS_SURVEY` | `0x0E` | Success + survey |
| `WOW_FAIL_LOCKED_ENFORCED` | `0x10` | Account locked |
| `WOW_FAIL_UNLOCKABLE_LOCK` | `0x12` | Locked (unlockable) |
| `WOW_FAIL_DISCONNECTED` | `0xFF` | Disconnected |

## SecurityFlags

| Flag | Value | Description |
|------|-------|-------------|
| `NONE` | `0x00` | No extra security |
| `PIN` | `0x01` | PIN input required |
| `UNK` | `0x02` | Unknown (unused in 1.12.1) |
| `AUTHENTICATOR` | `0x04` | Authenticator required (post-TBC) |

## Packet Structures

### CMD_AUTH_LOGON_CHALLENGE (C→S)

```
struct sAuthLogonChallengeHeader {        // 4 bytes
    uint8  cmd;                           // 0x00
    uint8  error;                         // 0x03 (always 3 for login)
    uint16 size;                          // remaining packet size
};

struct sAuthLogonChallengeBody {          // packed, variable size
    uint8  gamename[4];                   // "WoW\0" (reversed: 0x576F5700)
    uint8  version1;                      // 1
    uint8  version2;                      // 12
    uint8  version3;                      // 1
    uint16 build;                         // 5875
    uint8  platform[4];                   // "x86\0" (reversed: 0x783836xx)
    uint8  os[4];                         // "Win\0" (reversed)
    uint8  country[4];                    // "enUS" (reversed)
    uint32 timezone_bias;                 // minutes from UTC
    uint32 ip;                            // client IP address
    uint8  username_len;                  // length of username
    uint8  username[username_len];        // uppercase ASCII username
};
```

**Note:** `gamename`, `platform`, `os`, `country` are all reversed byte-order in the struct.

### CMD_AUTH_LOGON_CHALLENGE (S→C) — Success

```
uint8  cmd;                               // 0x00
uint8  unk;                               // 0x00
uint8  error;                             // WOW_SUCCESS (0x00)
uint8  B[32];                             // Server public ephemeral (big-endian)
uint8  g_len;                             // 1
uint8  g;                                 // 7 (generator)
uint8  N_len;                             // 32
uint8  N[32];                             // Safe prime (big-endian)
uint8  salt[32];                          // Account salt (from DB)
uint8  VersionChallenge[16];              // Random bytes for version hash
uint8  securityFlags;                     // SecurityFlags bitmask
// IF securityFlags & PIN (0x01):
//   uint32 pinGridSeed;
//   uint8  pinSalt[16];
```

### CMD_AUTH_LOGON_CHALLENGE (S→C) — Failure

```
uint8  cmd;                               // 0x00
uint8  unk;                               // 0x00
uint8  error;                             // AuthResult code (non-zero)
```

### CMD_AUTH_LOGON_PROOF (C→S)

For build >= 5428 (includes 1.12.1 build 5875):
```
struct sAuthLogonProof_C {                // packed
    uint8  cmd;                           // 0x01
    uint8  A[32];                         // Client public ephemeral
    uint8  M1[20];                        // Client proof (SHA1)
    uint8  crc_hash[20];                  // Client file CRC hash
    uint8  number_of_keys;                // 0
    uint8  securityFlags;                 // Must match server's flags
};
```

For builds < 5428 (pre-1.11.0):
```
struct sAuthLogonProof_C_Pre_1_11_0 {     // packed
    uint8  cmd;                           // 0x01
    uint8  A[32];                         // Client public ephemeral
    uint8  M1[20];                        // Client proof (SHA1)
    uint8  crc_hash[20];                  // Client file CRC hash
    uint8  number_of_keys;               // 0
};
```

### CMD_AUTH_LOGON_PROOF (S→C) — For build < 6299 (includes 1.12.1)

```
struct AUTH_LOGON_PROOF_S {               // packed
    uint8  cmd;                           // 0x01
    uint8  error;                         // AuthResult
    uint8  M2[20];                        // Server proof (SHA1)
    uint32 surveyId;                      // Survey ID (0 = no survey)
};
```

On failure:
```
uint8  cmd;                               // 0x01
uint8  error;                             // AuthResult code
uint8  unk1;                              // padding
uint8  unk2;                              // padding
```

## SRP6 Protocol Details

### Constants

```
N = 0x894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7  (256-bit safe prime)
g = 7                                     (generator)
```

### Registration (stored in DB)

```
x  = SHA1(salt, SHA1(USERNAME:PASSWORD))  // USERNAME and PASSWORD are uppercase
salt = random(32 bytes)
v  = g^x mod N                           // Verifier stored in DB
```

### Server Challenge (CalculateHostPublicEphemeral)

```
b  = random(19 bytes = 152 bits)          // Server secret ephemeral
B  = ((v * 3) + g^b mod N) mod N          // Server public ephemeral
```

Server sends: `B`, `g`, `N`, `salt`

### Client Proof

```
A  = g^a mod N                            // Client public ephemeral (a = random)
// Client MUST verify: A % N != 0 (server checks this)

u  = SHA1(A, B)                           // Scrambling parameter
S  = (B - 3 * g^x)^(a + u*x) mod N       // Client-side session key material
K  = HashSessionKey(S)                    // 40-byte session key

M1 = SHA1(SHA1(N) XOR SHA1(g), SHA1(USERNAME), salt, A, B, K)
```

### Server Verification (CalculateProof / Finalize)

```
u  = SHA1(A, B)
S  = (A * v^u)^b mod N                   // Server-side session key material
K  = HashSessionKey(S)                   // Must match client's K

// Verify M1
expected_M1 = SHA1(SHA1(N) XOR SHA1(g), SHA1(USERNAME), salt, A, B, K)
if M1 != expected_M1: reject

M2 = SHA1(A, M1, K)                      // Server proof sent back to client
```

### HashSessionKey (Session Key Derivation)

The session key `K` is derived from `S` via interleaved SHA1:

```
1. Convert S to byte array (32 bytes, little-endian)
2. Split into even bytes and odd bytes:
   even[i] = S_bytes[i*2]     for i = 0..15
   odd[i]  = S_bytes[i*2+1]   for i = 0..15
3. hash_even = SHA1(even[0..15])  // 20 bytes
4. hash_odd  = SHA1(odd[0..15])   // 20 bytes
5. Interleave:
   K[i*2]   = hash_even[i]   for i = 0..19
   K[i*2+1] = hash_odd[i]    for i = 0..19
Result: K is 40 bytes
```

### SHA1(N) XOR SHA1(g) Computation

```
hash_N = SHA1(N as big-endian byte array)   // 20 bytes
hash_g = SHA1(g as single byte: 0x07)       // 20 bytes
xor_result[i] = hash_N[i] ^ hash_g[i]      // 20 bytes
```

## Reconnect Protocol

Used when client still has a valid session key from a previous connection.

### CMD_AUTH_RECONNECT_CHALLENGE (C→S)

Same structure as `CMD_AUTH_LOGON_CHALLENGE` (header + body).

### CMD_AUTH_RECONNECT_CHALLENGE (S→C) — Success

```
uint8  cmd;                               // 0x02
uint8  error;                             // 0x00
uint8  reconnect_proof[16];              // Random bytes (server stores these)
uint8  VersionChallenge[16];             // Random bytes for version hash
```

### CMD_AUTH_RECONNECT_PROOF (C→S)

```
struct AUTH_RECONNECT_PROOF_C {           // packed
    uint8  cmd;                           // 0x03
    uint8  R1[16];                        // Client random proof data
    uint8  R2[20];                        // SHA1(username, R1, reconnect_proof, K)
    uint8  R3[20];                        // Unused (zeroed)
    uint8  number_of_keys;               // 0
};
```

Server verifies: `SHA1(username, R1, reconnect_proof, K) == R2`

### CMD_AUTH_RECONNECT_PROOF (S→C) — For build < 6299

```
uint8  cmd;                               // 0x03
uint8  error;                             // AuthResult (0x00 = success)
```

## Realm List

### CMD_REALM_LIST (C→S)

```
uint8  cmd;                               // 0x10
uint32 unk;                               // unused (typically 0)
```

### CMD_REALM_LIST (S→C) — For build < 6299 (1.12.1)

```
uint8  cmd;                               // 0x10
uint16 size;                              // remaining packet size
uint32 unused;                            // always 0
uint8  realm_count;                       // number of realms

// Repeated realm_count times:
struct RealmEntry {
    uint32 type;                          // 0=Normal, 1=PvP, 6=RP, 8=RPPvP
    uint8  flags;                         // 0x01=Invalid, 0x02=Offline, 0x04=ForceBlue, 0x20=ForceGreen, 0x40=ForceRed
    char   name[];                        // null-terminated string
    char   address[];                     // "ip:port" null-terminated string
    float  population;                    // population level (0.0-2.0+ range)
    uint8  characters;                    // character count for this account
    uint8  category;                      // realm category (time zone grouping)
    uint8  padding;                       // 0
};

uint16 trailer;                           // 0x0002 (always)
```

**Realm type values (1.12.1):**
| Type | Value | Description |
|------|-------|-------------|
| Normal | 0 | PvE realm |
| PvP | 1 | PvP realm |
| RP | 6 | RP realm |
| RPPvP | 8 | RP-PvP realm |

## Version Checking / Patching

After successful login proof, the server may optionally check the client version hash (`crc_hash` from the proof packet). If the client files don't match expected hashes, the server can initiate a patch transfer via `CMD_XFER_INITIATE`.

For 1.12.1 private servers, this check is typically disabled.

## Security Notes

- All numeric values in SRP6 are transmitted as **little-endian byte arrays** (32 bytes for 256-bit numbers)
- The username must be **uppercased** before hashing: `SHA1(salt, SHA1(UPPER(username) + ":" + UPPER(password)))`
- The server MUST check `A % N != 0` to prevent a trivial attack
- The server MUST check that `B % N != 0`
- Session key `K` is 40 bytes and used for world server packet encryption (HMAC-based)
- Build number determines packet format variations (pre/post 1.11.0 at build 5428, pre/post 2.x at build 6299)
