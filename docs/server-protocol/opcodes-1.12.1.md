# Opcodes — WoW 1.12.1 (Build 5875)

> Parsed from MaNGOS `Protocol/Opcodes_1_12_1.h`. Total: 828 opcodes (0x000–0x33B).

## Naming Convention

| Prefix | Direction | Description |
|--------|-----------|-------------|
| `CMSG_` | Client → Server | Client message |
| `SMSG_` | Server → Client | Server message |
| `MSG_` | Bidirectional | Both directions |

## Packet Header Format

### Client → Server (6 bytes)
```
uint16  size;       // Payload size (includes 4-byte opcode), encrypted
uint32  opcode;     // Little-endian, encrypted
```

### Server → Client (4 bytes)
```
uint16  size;       // Payload size (includes 2-byte opcode), big-endian, encrypted
uint16  opcode;     // Little-endian, encrypted
```

Encryption uses HMAC-based stream cipher keyed on the 40-byte session key `K`.

---

## Authentication & Session

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 492 | 0x1EC | SMSG_AUTH_CHALLENGE | S→C |
| 493 | 0x1ED | CMSG_AUTH_SESSION | C→S |
| 494 | 0x1EE | SMSG_AUTH_RESPONSE | S→C |

## Character Management

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 54 | 0x36 | CMSG_CHAR_CREATE | C→S |
| 55 | 0x37 | CMSG_CHAR_ENUM | C→S |
| 56 | 0x38 | CMSG_CHAR_DELETE | C→S |
| 58 | 0x3A | SMSG_CHAR_CREATE | S→C |
| 59 | 0x3B | SMSG_CHAR_ENUM | S→C |
| 60 | 0x3C | SMSG_CHAR_DELETE | S→C |
| 61 | 0x3D | CMSG_PLAYER_LOGIN | C→S |
| 65 | 0x41 | SMSG_CHARACTER_LOGIN_FAILED | S→C |
| 711 | 0x2C7 | CMSG_CHAR_RENAME | C→S |
| 712 | 0x2C8 | SMSG_CHAR_RENAME | S→C |

## World Entry / Login

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 62 | 0x3E | SMSG_NEW_WORLD | S→C |
| 63 | 0x3F | SMSG_TRANSFER_PENDING | S→C |
| 64 | 0x40 | SMSG_TRANSFER_ABORTED | S→C |
| 66 | 0x42 | SMSG_LOGIN_SETTIMESPEED | S→C |
| 566 | 0x236 | SMSG_LOGIN_VERIFY_WORLD | S→C |

## Logout

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 74 | 0x4A | CMSG_PLAYER_LOGOUT | C→S |
| 75 | 0x4B | CMSG_LOGOUT_REQUEST | C→S |
| 76 | 0x4C | SMSG_LOGOUT_RESPONSE | S→C |
| 77 | 0x4D | SMSG_LOGOUT_COMPLETE | S→C |
| 78 | 0x4E | CMSG_LOGOUT_CANCEL | C→S |
| 79 | 0x4F | SMSG_LOGOUT_CANCEL_ACK | S→C |

## Object Updates

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 169 | 0xA9 | SMSG_UPDATE_OBJECT | S→C |
| 170 | 0xAA | SMSG_DESTROY_OBJECT | S→C |
| 502 | 0x1F6 | SMSG_COMPRESSED_UPDATE_OBJECT | S→C |

## Queries

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 80 | 0x50 | CMSG_NAME_QUERY | C→S |
| 81 | 0x51 | SMSG_NAME_QUERY_RESPONSE | S→C |
| 82 | 0x52 | CMSG_PET_NAME_QUERY | C→S |
| 83 | 0x53 | SMSG_PET_NAME_QUERY_RESPONSE | S→C |
| 84 | 0x54 | CMSG_GUILD_QUERY | C→S |
| 85 | 0x55 | SMSG_GUILD_QUERY_RESPONSE | S→C |
| 86 | 0x56 | CMSG_ITEM_QUERY_SINGLE | C→S |
| 88 | 0x58 | SMSG_ITEM_QUERY_SINGLE_RESPONSE | S→C |
| 90 | 0x5A | CMSG_PAGE_TEXT_QUERY | C→S |
| 91 | 0x5B | SMSG_PAGE_TEXT_QUERY_RESPONSE | S→C |
| 92 | 0x5C | CMSG_QUEST_QUERY | C→S |
| 93 | 0x5D | SMSG_QUEST_QUERY_RESPONSE | S→C |
| 94 | 0x5E | CMSG_GAMEOBJECT_QUERY | C→S |
| 95 | 0x5F | SMSG_GAMEOBJECT_QUERY_RESPONSE | S→C |
| 96 | 0x60 | CMSG_CREATURE_QUERY | C→S |
| 97 | 0x61 | SMSG_CREATURE_QUERY_RESPONSE | S→C |

## Movement (MSG_MOVE_*)

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 181 | 0xB5 | MSG_MOVE_START_FORWARD | Both |
| 182 | 0xB6 | MSG_MOVE_START_BACKWARD | Both |
| 183 | 0xB7 | MSG_MOVE_STOP | Both |
| 184 | 0xB8 | MSG_MOVE_START_STRAFE_LEFT | Both |
| 185 | 0xB9 | MSG_MOVE_START_STRAFE_RIGHT | Both |
| 186 | 0xBA | MSG_MOVE_STOP_STRAFE | Both |
| 187 | 0xBB | MSG_MOVE_JUMP | Both |
| 188 | 0xBC | MSG_MOVE_START_TURN_LEFT | Both |
| 189 | 0xBD | MSG_MOVE_START_TURN_RIGHT | Both |
| 190 | 0xBE | MSG_MOVE_STOP_TURN | Both |
| 191 | 0xBF | MSG_MOVE_START_PITCH_UP | Both |
| 192 | 0xC0 | MSG_MOVE_START_PITCH_DOWN | Both |
| 193 | 0xC1 | MSG_MOVE_STOP_PITCH | Both |
| 194 | 0xC2 | MSG_MOVE_SET_RUN_MODE | Both |
| 195 | 0xC3 | MSG_MOVE_SET_WALK_MODE | Both |
| 197 | 0xC5 | MSG_MOVE_TELEPORT | Both |
| 199 | 0xC7 | MSG_MOVE_TELEPORT_ACK | Both |
| 201 | 0xC9 | MSG_MOVE_FALL_LAND | Both |
| 202 | 0xCA | MSG_MOVE_START_SWIM | Both |
| 203 | 0xCB | MSG_MOVE_STOP_SWIM | Both |
| 218 | 0xDA | MSG_MOVE_SET_FACING | Both |
| 219 | 0xDB | MSG_MOVE_SET_PITCH | Both |
| 220 | 0xDC | MSG_MOVE_WORLDPORT_ACK | Both |
| 236 | 0xEC | MSG_MOVE_ROOT | Both |
| 237 | 0xED | MSG_MOVE_UNROOT | Both |
| 238 | 0xEE | MSG_MOVE_HEARTBEAT | Both |
| 241 | 0xF1 | MSG_MOVE_KNOCK_BACK | Both |
| 247 | 0xF7 | MSG_MOVE_HOVER | Both |
| 688 | 0x2B0 | MSG_MOVE_FEATHER_FALL | Both |
| 689 | 0x2B1 | MSG_MOVE_WATER_WALK | Both |

## Movement Speed Changes

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 205 | 0xCD | MSG_MOVE_SET_RUN_SPEED | Both |
| 207 | 0xCF | MSG_MOVE_SET_RUN_BACK_SPEED | Both |
| 209 | 0xD1 | MSG_MOVE_SET_WALK_SPEED | Both |
| 211 | 0xD3 | MSG_MOVE_SET_SWIM_SPEED | Both |
| 213 | 0xD5 | MSG_MOVE_SET_SWIM_BACK_SPEED | Both |
| 216 | 0xD8 | MSG_MOVE_SET_TURN_RATE | Both |
| 226 | 0xE2 | SMSG_FORCE_RUN_SPEED_CHANGE | S→C |
| 227 | 0xE3 | CMSG_FORCE_RUN_SPEED_CHANGE_ACK | C→S |
| 228 | 0xE4 | SMSG_FORCE_RUN_BACK_SPEED_CHANGE | S→C |
| 229 | 0xE5 | CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK | C→S |
| 230 | 0xE6 | SMSG_FORCE_SWIM_SPEED_CHANGE | S→C |
| 231 | 0xE7 | CMSG_FORCE_SWIM_SPEED_CHANGE_ACK | C→S |
| 232 | 0xE8 | SMSG_FORCE_MOVE_ROOT | S→C |
| 233 | 0xE9 | CMSG_FORCE_MOVE_ROOT_ACK | C→S |
| 234 | 0xEA | SMSG_FORCE_MOVE_UNROOT | S→C |
| 235 | 0xEB | CMSG_FORCE_MOVE_UNROOT_ACK | C→S |
| 730 | 0x2DA | SMSG_FORCE_WALK_SPEED_CHANGE | S→C |
| 731 | 0x2DB | CMSG_FORCE_WALK_SPEED_CHANGE_ACK | C→S |
| 732 | 0x2DC | SMSG_FORCE_SWIM_BACK_SPEED_CHANGE | S→C |
| 733 | 0x2DD | CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK | C→S |
| 734 | 0x2DE | SMSG_FORCE_TURN_RATE_CHANGE | S→C |
| 735 | 0x2DF | CMSG_FORCE_TURN_RATE_CHANGE_ACK | C→S |

## Monster / Spline Movement

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 221 | 0xDD | SMSG_MONSTER_MOVE | S→C |
| 686 | 0x2AE | SMSG_MONSTER_MOVE_TRANSPORT | S→C |
| 766 | 0x2FE | SMSG_SPLINE_SET_RUN_SPEED | S→C |
| 767 | 0x2FF | SMSG_SPLINE_SET_RUN_BACK_SPEED | S→C |
| 768 | 0x300 | SMSG_SPLINE_SET_SWIM_SPEED | S→C |
| 769 | 0x301 | SMSG_SPLINE_SET_WALK_SPEED | S→C |
| 770 | 0x302 | SMSG_SPLINE_SET_SWIM_BACK_SPEED | S→C |
| 771 | 0x303 | SMSG_SPLINE_SET_TURN_RATE | S→C |
| 763 | 0x2FB | SMSG_COMPRESSED_MOVES | S→C |

## Combat / Attack

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 317 | 0x13D | CMSG_SET_SELECTION | C→S |
| 321 | 0x141 | CMSG_ATTACKSWING | C→S |
| 322 | 0x142 | CMSG_ATTACKSTOP | C→S |
| 323 | 0x143 | SMSG_ATTACKSTART | S→C |
| 324 | 0x144 | SMSG_ATTACKSTOP | S→C |
| 325 | 0x145 | SMSG_ATTACKSWING_NOTINRANGE | S→C |
| 326 | 0x146 | SMSG_ATTACKSWING_BADFACING | S→C |
| 330 | 0x14A | SMSG_ATTACKERSTATEUPDATE | S→C |
| 334 | 0x14E | SMSG_CANCEL_COMBAT | S→C |

## Spellcasting

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 302 | 0x12E | CMSG_CAST_SPELL | C→S |
| 303 | 0x12F | CMSG_CANCEL_CAST | C→S |
| 304 | 0x130 | SMSG_CAST_RESULT | S→C |
| 305 | 0x131 | SMSG_SPELL_START | S→C |
| 306 | 0x132 | SMSG_SPELL_GO | S→C |
| 307 | 0x133 | SMSG_SPELL_FAILURE | S→C |
| 308 | 0x134 | SMSG_SPELL_COOLDOWN | S→C |
| 309 | 0x135 | SMSG_COOLDOWN_EVENT | S→C |
| 310 | 0x136 | CMSG_CANCEL_AURA | C→S |
| 313 | 0x139 | MSG_CHANNEL_START | Both |
| 314 | 0x13A | MSG_CHANNEL_UPDATE | Both |
| 315 | 0x13B | CMSG_CANCEL_CHANNELLING | C→S |
| 336 | 0x150 | SMSG_SPELLHEALLOG | S→C |
| 337 | 0x151 | SMSG_SPELLENERGIZELOG | S→C |
| 482 | 0x1EA | SMSG_SPELL_DELAYED | S→C |
| 496 | 0x1F0 | CMSG_PET_CAST_SPELL | C→S |
| 592 | 0x250 | SMSG_SPELLNONMELEEDAMAGELOG | S→C |
| 678 | 0x2A6 | SMSG_SPELL_FAILED_OTHER | S→C |

## Spell/Action Setup

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 296 | 0x128 | CMSG_SET_ACTION_BUTTON | C→S |
| 297 | 0x129 | SMSG_ACTION_BUTTONS | S→C |
| 298 | 0x12A | SMSG_INITIAL_SPELLS | S→C |
| 299 | 0x12B | SMSG_LEARNED_SPELL | S→C |
| 300 | 0x12C | SMSG_SUPERCEDED_SPELL | S→C |
| 515 | 0x203 | SMSG_REMOVED_SPELL | S→C |
| 593 | 0x251 | CMSG_LEARN_TALENT | C→S |

## Chat

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 149 | 0x95 | CMSG_MESSAGECHAT | C→S |
| 150 | 0x96 | SMSG_MESSAGECHAT | S→C |
| 151 | 0x97 | CMSG_JOIN_CHANNEL | C→S |
| 152 | 0x98 | CMSG_LEAVE_CHANNEL | C→S |
| 153 | 0x99 | SMSG_CHANNEL_NOTIFY | S→C |
| 537 | 0x219 | SMSG_CHAT_WRONG_FACTION | S→C |
| 682 | 0x2AA | SMSG_CHAT_PLAYER_NOT_FOUND | S→C |

## Social

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 98 | 0x62 | CMSG_WHO | C→S |
| 99 | 0x63 | SMSG_WHO | S→C |
| 102 | 0x66 | CMSG_FRIEND_LIST | C→S |
| 103 | 0x67 | SMSG_FRIEND_LIST | S→C |
| 104 | 0x68 | SMSG_FRIEND_STATUS | S→C |
| 105 | 0x69 | CMSG_ADD_FRIEND | C→S |
| 106 | 0x6A | CMSG_DEL_FRIEND | C→S |
| 107 | 0x6B | SMSG_IGNORE_LIST | S→C |
| 108 | 0x6C | CMSG_ADD_IGNORE | C→S |
| 109 | 0x6D | CMSG_DEL_IGNORE | C→S |

## Group / Party

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 110 | 0x6E | CMSG_GROUP_INVITE | C→S |
| 111 | 0x6F | SMSG_GROUP_INVITE | S→C |
| 114 | 0x72 | CMSG_GROUP_ACCEPT | C→S |
| 115 | 0x73 | CMSG_GROUP_DECLINE | C→S |
| 117 | 0x75 | CMSG_GROUP_UNINVITE | C→S |
| 120 | 0x78 | CMSG_GROUP_SET_LEADER | C→S |
| 122 | 0x7A | CMSG_LOOT_METHOD | C→S |
| 123 | 0x7B | CMSG_GROUP_DISBAND | C→S |
| 125 | 0x7D | SMSG_GROUP_LIST | S→C |
| 126 | 0x7E | SMSG_PARTY_MEMBER_STATS | S→C |
| 127 | 0x7F | SMSG_PARTY_COMMAND_RESULT | S→C |
| 654 | 0x28E | CMSG_GROUP_RAID_CONVERT | C→S |
| 655 | 0x28F | CMSG_GROUP_ASSISTANT_LEADER | C→S |

## Looting

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 349 | 0x15D | CMSG_LOOT | C→S |
| 350 | 0x15E | CMSG_LOOT_MONEY | C→S |
| 351 | 0x15F | CMSG_LOOT_RELEASE | C→S |
| 352 | 0x160 | SMSG_LOOT_RESPONSE | S→C |
| 353 | 0x161 | SMSG_LOOT_RELEASE_RESPONSE | S→C |
| 354 | 0x162 | SMSG_LOOT_REMOVED | S→C |
| 355 | 0x163 | SMSG_LOOT_MONEY_NOTIFY | S→C |
| 358 | 0x166 | SMSG_ITEM_PUSH_RESULT | S→C |

## Item / Inventory

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 171 | 0xAB | CMSG_USE_ITEM | C→S |
| 172 | 0xAC | CMSG_OPEN_ITEM | C→S |
| 266 | 0x10A | CMSG_AUTOEQUIP_ITEM | C→S |
| 267 | 0x10B | CMSG_AUTOSTORE_BAG_ITEM | C→S |
| 268 | 0x10C | CMSG_SWAP_ITEM | C→S |
| 269 | 0x10D | CMSG_SWAP_INV_ITEM | C→S |
| 270 | 0x10E | CMSG_SPLIT_ITEM | C→S |
| 273 | 0x111 | CMSG_DESTROYITEM | C→S |
| 274 | 0x112 | SMSG_INVENTORY_CHANGE_FAILURE | S→C |

## Vendor / Shopping

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 414 | 0x19E | CMSG_LIST_INVENTORY | C→S |
| 415 | 0x19F | SMSG_LIST_INVENTORY | S→C |
| 416 | 0x1A0 | CMSG_SELL_ITEM | C→S |
| 418 | 0x1A2 | CMSG_BUY_ITEM | C→S |
| 420 | 0x1A4 | SMSG_BUY_ITEM | S→C |
| 421 | 0x1A5 | SMSG_BUY_FAILED | S→C |

## Quest System

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 386 | 0x182 | CMSG_QUESTGIVER_STATUS_QUERY | C→S |
| 387 | 0x183 | SMSG_QUESTGIVER_STATUS | S→C |
| 388 | 0x184 | CMSG_QUESTGIVER_HELLO | C→S |
| 389 | 0x185 | SMSG_QUESTGIVER_QUEST_LIST | S→C |
| 390 | 0x186 | CMSG_QUESTGIVER_QUERY_QUEST | C→S |
| 392 | 0x188 | SMSG_QUESTGIVER_QUEST_DETAILS | S→C |
| 393 | 0x189 | CMSG_QUESTGIVER_ACCEPT_QUEST | C→S |
| 394 | 0x18A | CMSG_QUESTGIVER_COMPLETE_QUEST | C→S |
| 395 | 0x18B | SMSG_QUESTGIVER_REQUEST_ITEMS | S→C |
| 397 | 0x18D | SMSG_QUESTGIVER_OFFER_REWARD | S→C |
| 398 | 0x18E | CMSG_QUESTGIVER_CHOOSE_REWARD | C→S |
| 401 | 0x191 | SMSG_QUESTGIVER_QUEST_COMPLETE | S→C |
| 406 | 0x196 | SMSG_QUESTUPDATE_FAILED | S→C |
| 408 | 0x198 | SMSG_QUESTUPDATE_COMPLETE | S→C |
| 409 | 0x199 | SMSG_QUESTUPDATE_ADD_KILL | S→C |

## NPC Interaction / Gossip

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 379 | 0x17B | CMSG_GOSSIP_HELLO | C→S |
| 380 | 0x17C | CMSG_GOSSIP_SELECT_OPTION | C→S |
| 381 | 0x17D | SMSG_GOSSIP_MESSAGE | S→C |
| 382 | 0x17E | SMSG_GOSSIP_COMPLETE | S→C |
| 383 | 0x17F | CMSG_NPC_TEXT_QUERY | C→S |
| 384 | 0x180 | SMSG_NPC_TEXT_UPDATE | S→C |

## Trainer

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 432 | 0x1B0 | CMSG_TRAINER_LIST | C→S |
| 433 | 0x1B1 | SMSG_TRAINER_LIST | S→C |
| 434 | 0x1B2 | CMSG_TRAINER_BUY_SPELL | C→S |

## Taxi

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 425 | 0x1A9 | SMSG_SHOWTAXINODES | S→C |
| 426 | 0x1AA | CMSG_TAXINODE_STATUS_QUERY | C→S |
| 427 | 0x1AB | SMSG_TAXINODE_STATUS | S→C |
| 429 | 0x1AD | CMSG_ACTIVATETAXI | C→S |
| 430 | 0x1AE | SMSG_ACTIVATETAXIREPLY | S→C |
| 786 | 0x312 | CMSG_ACTIVATETAXIEXPRESS | C→S |

## Mail

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 568 | 0x238 | CMSG_SEND_MAIL | C→S |
| 569 | 0x239 | SMSG_SEND_MAIL_RESULT | S→C |
| 570 | 0x23A | CMSG_GET_MAIL_LIST | C→S |
| 571 | 0x23B | SMSG_MAIL_LIST_RESULT | S→C |
| 645 | 0x285 | SMSG_RECEIVED_MAIL | S→C |

## Auction House

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 597 | 0x255 | MSG_AUCTION_HELLO | Both |
| 598 | 0x256 | CMSG_AUCTION_SELL_ITEM | C→S |
| 599 | 0x257 | CMSG_AUCTION_REMOVE_ITEM | C→S |
| 600 | 0x258 | CMSG_AUCTION_LIST_ITEMS | C→S |
| 602 | 0x25A | CMSG_AUCTION_PLACE_BID | C→S |
| 603 | 0x25B | SMSG_AUCTION_COMMAND_RESULT | S→C |
| 604 | 0x25C | SMSG_AUCTION_LIST_RESULT | S→C |

## Guild

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 129 | 0x81 | CMSG_GUILD_CREATE | C→S |
| 130 | 0x82 | CMSG_GUILD_INVITE | C→S |
| 137 | 0x89 | CMSG_GUILD_ROSTER | C→S |
| 138 | 0x8A | SMSG_GUILD_ROSTER | S→C |
| 141 | 0x8D | CMSG_GUILD_LEAVE | C→S |
| 146 | 0x92 | SMSG_GUILD_EVENT | S→C |
| 147 | 0x93 | SMSG_GUILD_COMMAND_RESULT | S→C |

## Emotes

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 257 | 0x101 | CMSG_STANDSTATECHANGE | C→S |
| 258 | 0x102 | CMSG_EMOTE | C→S |
| 259 | 0x103 | SMSG_EMOTE | S→C |
| 260 | 0x104 | CMSG_TEXT_EMOTE | C→S |
| 261 | 0x105 | SMSG_TEXT_EMOTE | S→C |

## Death / Corpse

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 346 | 0x15A | CMSG_REPOP_REQUEST | C→S |
| 347 | 0x15B | SMSG_RESURRECT_REQUEST | S→C |
| 348 | 0x15C | CMSG_RESURRECT_RESPONSE | C→S |
| 466 | 0x1D2 | CMSG_RECLAIM_CORPSE | C→S |
| 534 | 0x20E | MSG_CORPSE_QUERY | Both |
| 617 | 0x269 | SMSG_CORPSE_RECLAIM_DELAY | S→C |

## Duel

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 359 | 0x167 | SMSG_DUEL_REQUESTED | S→C |
| 362 | 0x16A | SMSG_DUEL_COMPLETE | S→C |
| 363 | 0x16B | SMSG_DUEL_WINNER | S→C |
| 364 | 0x16C | CMSG_DUEL_ACCEPTED | C→S |
| 365 | 0x16D | CMSG_DUEL_CANCELLED | C→S |

## Pet

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 372 | 0x174 | CMSG_PET_SET_ACTION | C→S |
| 373 | 0x175 | CMSG_PET_ACTION | C→S |
| 374 | 0x176 | CMSG_PET_ABANDON | C→S |
| 377 | 0x179 | SMSG_PET_SPELLS | S→C |

## Battleground / PvP

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 572 | 0x23C | CMSG_BATTLEFIELD_LIST | C→S |
| 573 | 0x23D | SMSG_BATTLEFIELD_LIST | S→C |
| 723 | 0x2D3 | CMSG_BATTLEFIELD_STATUS | C→S |
| 724 | 0x2D4 | SMSG_BATTLEFIELD_STATUS | S→C |
| 725 | 0x2D5 | CMSG_BATTLEFIELD_PORT | C→S |
| 750 | 0x2EE | CMSG_BATTLEMASTER_JOIN | C→S |

## Warden (Anti-Cheat)

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 742 | 0x2E6 | SMSG_WARDEN_DATA | S→C |
| 743 | 0x2E7 | CMSG_WARDEN_DATA | C→S |

## Ping / Keepalive

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 476 | 0x1DC | CMSG_PING | C→S |
| 477 | 0x1DD | SMSG_PONG | S→C |

## Account Data

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 521 | 0x209 | SMSG_ACCOUNT_DATA_MD5 | S→C |
| 522 | 0x20A | CMSG_REQUEST_ACCOUNT_DATA | C→S |
| 523 | 0x20B | CMSG_UPDATE_ACCOUNT_DATA | C→S |

## Time / Timer

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 460 | 0x1CC | CMSG_PLAYED_TIME | C→S |
| 461 | 0x1CD | SMSG_PLAYED_TIME | S→C |
| 462 | 0x1CE | CMSG_QUERY_TIME | C→S |
| 463 | 0x1CF | SMSG_QUERY_TIME_RESPONSE | S→C |
| 473 | 0x1D9 | SMSG_START_MIRROR_TIMER | S→C |
| 474 | 0x1DA | SMSG_PAUSE_MIRROR_TIMER | S→C |
| 475 | 0x1DB | SMSG_STOP_MIRROR_TIMER | S→C |

## World State / Environment

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 500 | 0x1F4 | CMSG_ZONEUPDATE | C→S |
| 504 | 0x1F8 | SMSG_EXPLORATION_EXPERIENCE | S→C |
| 706 | 0x2C2 | SMSG_INIT_WORLD_STATES | S→C |
| 707 | 0x2C3 | SMSG_UPDATE_WORLD_STATE | S→C |

## Cinematic / Tutorial

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 250 | 0xFA | SMSG_TRIGGER_CINEMATIC | S→C |
| 252 | 0xFC | CMSG_COMPLETE_CINEMATIC | C→S |
| 253 | 0xFD | SMSG_TUTORIAL_FLAGS | S→C |
| 254 | 0xFE | CMSG_TUTORIAL_FLAG | C→S |

## Misc

| Opcode | Hex | Name | Direction |
|--------|-----|------|-----------|
| 0 | 0x00 | MSG_NULL_ACTION | — |
| 176 | 0xB0 | SMSG_ITEM_COOLDOWN | S→C |
| 345 | 0x159 | SMSG_CLIENT_CONTROL_UPDATE | S→C |
| 459 | 0x1CB | SMSG_NOTIFICATION | S→C |
| 464 | 0x1D0 | SMSG_LOG_XPGAIN | S→C |
| 468 | 0x1D4 | SMSG_LEVELUP_INFO | S→C |
| 507 | 0x1FB | MSG_RANDOM_ROLL | Both |
| 508 | 0x1FC | SMSG_ENVIRONMENTALDAMAGELOG | S→C |
| 657 | 0x291 | SMSG_SERVER_MESSAGE | S→C |
| 751 | 0x2EF | SMSG_ADDON_INFO | S→C |
