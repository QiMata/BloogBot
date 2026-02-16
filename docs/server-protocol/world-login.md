# World Login Protocol — WoW 1.12.1 (Build 5875)

> Parsed from MaNGOS `CharacterHandler.cpp`, `WorldSocket.cpp`, `WorldSession.cpp`.

## Overview

After realm authentication (see `auth-protocol.md`), the client connects to the world server
on a TCP socket. The world server uses encrypted packet headers (HMAC-based cipher keyed on
the 40-byte session key `K` derived during SRP6 auth).

## Connection Sequence

```
1. Client connects to world server TCP socket
2. Server → Client: SMSG_AUTH_CHALLENGE (unencrypted)
3. Client → Server: CMSG_AUTH_SESSION (unencrypted)
4. Server verifies session key against realmd database
5. Server → Client: SMSG_AUTH_RESPONSE
   (encryption enabled from this point)
6. Client → Server: CMSG_CHAR_ENUM
7. Server → Client: SMSG_CHAR_ENUM
8. Client → Server: CMSG_PLAYER_LOGIN
9. Server loads character data (20 async DB queries)
10. Server → Client: Login packet burst (see below)
```

## Packet Header Encryption

### Server → Client Header (4 bytes)

```
uint16  size;     // Big-endian, encrypted
uint16  opcode;   // Little-endian, encrypted
```

Encryption (`AuthCrypt::EncryptSend`):
```
for t in 0..3:
    _send_i %= key.length   // key = 40-byte session key K
    x = (data[t] ^ key[_send_i]) + _send_j
    _send_i++
    data[t] = _send_j = x
```

### Client → Server Header (6 bytes)

```
uint16  size;     // Big-endian, encrypted
uint32  opcode;   // Little-endian, encrypted
```

Decryption (`AuthCrypt::DecryptRecv`):
```
for t in 0..5:
    _recv_i %= key.length
    x = (data[t] - _recv_j) ^ key[_recv_i]
    _recv_i++
    _recv_j = data[t]
    data[t] = x
```

**Note:** Packet payloads are NOT encrypted, only the headers.
Encryption is initialized after `CMSG_AUTH_SESSION` is processed.

## Key Packets

### SMSG_AUTH_CHALLENGE (0x1EC) — S→C

Sent immediately on connection (before encryption is active).

```
uint32  serverSeed;          // Random 32-bit value
```

### CMSG_AUTH_SESSION (0x1ED) — C→S

Client's authentication to the world server.

```
uint32  clientBuild;         // 5875 for 1.12.1
uint32  serverId;            // Realm ID
string  accountName;         // Null-terminated uppercase account name
uint32  clientSeed;          // Random 32-bit value
uint8   digest[20];          // SHA1 authentication hash
[AddonData]                  // Compressed addon info (zlib)
```

**Digest calculation:**
```
digest = SHA1(accountName, 0x00000000, clientSeed, serverSeed, sessionKey)
```

Where `sessionKey` is the 40-byte `K` from SRP6 auth, stored in the realmd database.

### SMSG_AUTH_RESPONSE (0x1EE) — S→C

```
uint8   result;              // Auth result code
```

| Code | Value | Description |
|------|-------|-------------|
| AUTH_OK | 12 | Success |
| AUTH_FAILED | 0 | Bad hash |
| AUTH_UNKNOWN_ACCOUNT | 21 | Account not found |
| AUTH_BANNED | 3 | Account banned |
| AUTH_UNAVAILABLE | 16 | Server locked |
| AUTH_VERSION_MISMATCH | 4 | Wrong build |

### CMSG_CHAR_ENUM (0x37) — C→S

Request character list. **Empty payload** (opcode only).

### SMSG_CHAR_ENUM (0x3B) — S→C

Character list response.

```
uint8   characterCount;      // Number of characters (max 10)

// Per character:
uint64  guid;                // Full 8-byte GUID
string  name;                // Null-terminated name
uint8   race;                // Race ID
uint8   class;               // Class ID
uint8   gender;              // 0=Male, 1=Female
uint8   skin;                // Appearance
uint8   face;                // Appearance
uint8   hairStyle;           // Appearance
uint8   hairColor;           // Appearance
uint8   facialHair;          // Appearance
uint8   level;               // Character level
uint32  zoneId;              // Current zone
uint32  mapId;               // Current map/continent
float   posX;                // X coordinate
float   posY;                // Y coordinate
float   posZ;                // Z coordinate
uint32  guildId;             // Guild ID (0 if none)
uint32  characterFlags;      // See flags below
uint8   firstLogin;          // 1 = never logged in
uint32  petDisplayId;        // Pet model (0 if none)
uint32  petLevel;            // Pet level (0 if none)
uint32  petFamily;           // Pet family (0 if none)

// Equipment (19 visible slots):
// Repeated 19 times:
uint32  displayInfoId;       // Item display ID (0 if empty)
uint8   inventoryType;       // Item slot type
```

**Character Flags:**

| Flag | Description |
|------|-------------|
| 0x01 | Has helm hidden |
| 0x08 | Has cloak hidden |
| 0x02 | Is ghost |
| 0x04 | Needs rename |
| 0x20 | Is locked for transfer |

**Equipment slot order (19 slots):**
Head, Neck, Shoulder, Body (shirt), Chest, Waist, Legs, Feet, Wrist, Hands,
Finger1, Finger2, Trinket1, Trinket2, Back, MainHand, OffHand, Ranged, Tabard

### CMSG_PLAYER_LOGIN (0x3D) — C→S

```
uint64  characterGuid;       // GUID from SMSG_CHAR_ENUM
```

### Login Packet Burst

After `CMSG_PLAYER_LOGIN`, the server executes 20 database queries asynchronously:

| Query | Data |
|-------|------|
| LOADFROM | Main character data |
| LOADGROUP | Group membership |
| LOADBOUNDINSTANCES | Saved dungeon/raid instances |
| LOADAURAS | Active buffs/debuffs |
| LOADSPELLS | Known spells |
| LOADQUESTSTATUS | Quest progress |
| LOADHONORCP | Honor/PvP data |
| LOADREPUTATION | Faction standings |
| LOADINVENTORY | All items |
| LOADITEMLOOT | Pending item loot |
| LOADACTIONS | Action bar layout |
| LOADSOCIALLIST | Friends/ignore lists |
| LOADHOMEBIND | Hearthstone bind point |
| LOADSPELLCOOLDOWNS | Spell cooldowns |
| LOADGUILD | Guild data |
| LOADBGDATA | Battleground state |
| LOADACCOUNTDATA | Account-wide settings |
| LOADSKILLS | Skill levels |
| LOADMAILS | Mail headers |
| LOADMAILEDITEMS | Mail attachments |

Once all queries complete, the server sends a burst of packets:

```
SMSG_LOGIN_VERIFY_WORLD         // Position confirmation
SMSG_ACCOUNT_DATA_MD5           // Account data checksums
SMSG_LOGIN_SETTIMESPEED         // Game time + speed
SMSG_TUTORIAL_FLAGS             // Tutorial state
SMSG_INITIAL_SPELLS             // All known spells
SMSG_ACTION_BUTTONS             // Action bar config
SMSG_INITIALIZE_FACTIONS        // Reputation data
SMSG_SET_PROFICIENCY            // Weapon/armor proficiencies
SMSG_UPDATE_OBJECT              // Self create (UPDATETYPE_CREATE_OBJECT2)
// ... additional state packets ...
```

### SMSG_LOGIN_VERIFY_WORLD (0x236) — S→C

Confirms the player's position in the world.

```
uint32  mapId;               // Continent/instance map ID
float   posX;                // X coordinate
float   posY;                // Y coordinate
float   posZ;                // Z coordinate
float   orientation;         // Facing angle (radians)
```

### SMSG_LOGIN_SETTIMESPEED (0x42) — S→C

```
uint32  gameTime;            // Packed game time (see below)
float   gameSpeed;           // Game time speed (normally 0.01666667 = 1/60)
```

**Game time packing:**
```
bits 0-5:   minutes (0-59)
bits 6-10:  hours (0-23)
bits 11-16: weekday (0=Sun)
bits 17-21: monthday (1-31)
bits 22-25: month (0-11)
bits 26-30: year (since 2000)
```

## Map Transfer (Same Server)

When entering a dungeon, using a portal, or teleporting within the same server:

```
1. Server → Client: SMSG_TRANSFER_PENDING
   uint32  mapId;           // Destination map
   // IF transport:
   uint32  transportEntry;  // Transport template entry
   uint32  oldMapId;        // Source map

2. Client unloads current map, shows loading screen

3. Server → Client: SMSG_NEW_WORLD
   uint32  mapId;
   float   posX, posY, posZ, orientation;

4. Client loads new map data

5. Client → Server: MSG_MOVE_WORLDPORT_ACK
   (empty payload - just the opcode)

6. Server sends object create packets for new area
```

## Ping / Keepalive

```
CMSG_PING (0x1DC) — C→S:
    uint32  sequenceId;      // Incrementing counter
    uint32  latency;         // Client-measured round-trip time (ms)

SMSG_PONG (0x1DD) — S→C:
    uint32  sequenceId;      // Echo back the sequence ID
```

The server disconnects if no ping is received within the timeout period.
