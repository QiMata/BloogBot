# Opcode Dispatch Table

Static opcode registration recovered from WoW.exe 1.12.1 for `NetClient::ProcessMessage` (`0x537AA0`).

- Source enum set: `Exports/GameData.Core/Enums/Opcode.cs` (906 unique opcode values, `0x000`-`0x423`).
- Static registration sites: calls to `0x537A60` / `0x5AB650` recovered from the client binary.
- Dispatch proof: `0x537ACF` loads `handler = [this + opcode*4 + 0x74]`; `0x537ADC` loads `context = [this + opcode*4 + 0xD64]` before `call eax`.
- Plan mismatch: `docs/WOW_EXE_PACKET_PARITY_PLAN.md` says 828 opcodes, but the current repo enum defines 906 unique values; this table covers the full repo-defined set.

| Opcode | Name | Dir | Handler VA | Context | Call Site | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `0x000` | `MSG_NULL_ACTION` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x001` | `CMSG_BOOTME` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x002` | `CMSG_DBLOOKUP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x003` | `SMSG_DBLOOKUP` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x004` | `CMSG_QUERY_OBJECT_POSITION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x005` | `SMSG_QUERY_OBJECT_POSITION` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x006` | `CMSG_QUERY_OBJECT_ROTATION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x007` | `SMSG_QUERY_OBJECT_ROTATION` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x008` | `CMSG_WORLD_TELEPORT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x009` | `CMSG_TELEPORT_TO_UNIT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x00A` | `CMSG_ZONE_MAP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x00B` | `SMSG_ZONE_MAP` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x00C` | `CMSG_DEBUG_CHANGECELLZONE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x00D` | `CMSG_EMBLAZON_TABARD_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x00E` | `CMSG_UNEMBLAZON_TABARD_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x00F` | `CMSG_RECHARGE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x010` | `CMSG_LEARN_SPELL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x011` | `CMSG_CREATEMONSTER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x012` | `CMSG_DESTROYMONSTER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x013` | `CMSG_CREATEITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x014` | `CMSG_CREATEGAMEOBJECT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x015` | `SMSG_CHECK_FOR_BOTS` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x016` | `CMSG_MAKEMONSTERATTACKGUID` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x017` | `CMSG_BOT_DETECTED2` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x018` | `CMSG_FORCEACTION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x019` | `CMSG_FORCEACTIONONOTHER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x01A` | `CMSG_FORCEACTIONSHOW` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x01B` | `SMSG_FORCEACTIONSHOW` | `S->C` | `0x5e38c0` | `0` | `0x005E3638` | Static registration observed in WoW.exe. |
| `0x01C` | `CMSG_PETGODMODE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x01D` | `SMSG_PETGODMODE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x01E` | `SMSG_DEBUGINFOSPELLMISS_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x01F` | `CMSG_WEATHER_SPEED_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x020` | `CMSG_UNDRESSPLAYER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x021` | `CMSG_BEASTMASTER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x022` | `CMSG_GODMODE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x023` | `SMSG_GODMODE` | `S->C` | `0x5e38c0` | `0` | `0x005E3649` | Static registration observed in WoW.exe. |
| `0x024` | `CMSG_CHEAT_SETMONEY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x025` | `CMSG_LEVEL_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x026` | `CMSG_PET_LEVEL_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x027` | `CMSG_SET_WORLDSTATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x028` | `CMSG_COOLDOWN_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x029` | `CMSG_USE_SKILL_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x02A` | `CMSG_FLAG_QUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x02B` | `CMSG_FLAG_QUEST_FINISH` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x02C` | `CMSG_CLEAR_QUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x02D` | `CMSG_SEND_EVENT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x02E` | `CMSG_DEBUG_AISTATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x02F` | `SMSG_DEBUG_AISTATE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x030` | `CMSG_DISABLE_PVP_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x031` | `CMSG_ADVANCE_SPAWN_TIME` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x032` | `CMSG_PVP_PORT_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x033` | `CMSG_AUTH_SRP6_BEGIN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x034` | `CMSG_AUTH_SRP6_PROOF` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x035` | `CMSG_AUTH_SRP6_RECODE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x036` | `CMSG_CHAR_CREATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x037` | `CMSG_CHAR_ENUM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x038` | `CMSG_CHAR_DELETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x039` | `SMSG_AUTH_SRP6_RESPONSE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x03A` | `SMSG_CHAR_CREATE` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3B06` | Static registration observed in WoW.exe. |
| `0x03B` | `SMSG_CHAR_ENUM` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3AF7` | Static registration observed in WoW.exe. |
| `0x03C` | `SMSG_CHAR_DELETE` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3B51` | Static registration observed in WoW.exe. |
| `0x03D` | `CMSG_PLAYER_LOGIN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x03E` | `SMSG_NEW_WORLD` | `S->C` | `0x401b00` | `0` | `0x00401756` | SMSG_NEW_WORLD world-entry handler. |
| `0x03F` | `SMSG_TRANSFER_PENDING` | `S->C` | `0x401900` | `0` | `0x00401767` | Static registration observed in WoW.exe. |
| `0x040` | `SMSG_TRANSFER_ABORTED` | `S->C` | `0x4019a0` | `0` | `0x00401778` | Static registration observed in WoW.exe. |
| `0x041` | `SMSG_CHARACTER_LOGIN_FAILED` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3B15` | Static registration observed in WoW.exe. |
| `0x042` | `SMSG_LOGIN_SETTIMESPEED` | `S->C` | `0x6c5e80` | `0` | `0x006C5D8D` | Static registration observed in WoW.exe. |
| `0x043` | `SMSG_GAMETIME_UPDATE` | `S->C` | `0x6c6010` | `0` | `0x006C5D9E` | Static registration observed in WoW.exe. |
| `0x044` | `CMSG_GAMETIME_SET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x045` | `SMSG_GAMETIME_SET` | `S->C` | `0x6c6120` | `0` | `0x006C5DC0` | Static registration observed in WoW.exe. |
| `0x046` | `CMSG_GAMESPEED_SET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x047` | `SMSG_GAMESPEED_SET` | `S->C` | `0x6c5de0` | `0` | `0x006C5D7C` | Static registration observed in WoW.exe. |
| `0x048` | `CMSG_SERVERTIME` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x049` | `SMSG_SERVERTIME` | `S->C` | `0x6c6080` | `0` | `0x006C5DAF` | Static registration observed in WoW.exe. |
| `0x04A` | `CMSG_PLAYER_LOGOUT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x04B` | `CMSG_LOGOUT_REQUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x04C` | `SMSG_LOGOUT_RESPONSE` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3B42` | Static registration observed in WoW.exe. |
| `0x04D` | `SMSG_LOGOUT_COMPLETE` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3B24` | Static registration observed in WoW.exe. |
| `0x04E` | `CMSG_LOGOUT_CANCEL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x04F` | `SMSG_LOGOUT_CANCEL_ACK` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3B33` | Static registration observed in WoW.exe. |
| `0x050` | `CMSG_NAME_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x051` | `SMSG_NAME_QUERY_RESPONSE` | `S->C` | `0x5551a0` | `0` | `0x00555062` | Static registration observed in WoW.exe. |
| `0x052` | `CMSG_PET_NAME_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x053` | `SMSG_PET_NAME_QUERY_RESPONSE` | `S->C` | `0x555480` | `0` | `0x005550A6` | Static registration observed in WoW.exe. |
| `0x054` | `CMSG_GUILD_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x055` | `SMSG_GUILD_QUERY_RESPONSE` | `S->C` | `0x555290` | `0` | `0x00555073` | Static registration observed in WoW.exe. |
| `0x056` | `CMSG_ITEM_QUERY_SINGLE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x057` | `CMSG_ITEM_QUERY_MULTIPLE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x058` | `SMSG_ITEM_QUERY_SINGLE_RESPONSE` | `S->C` | `0x555140` | `0` | `0x0055502F` | Static registration observed in WoW.exe. |
| `0x059` | `SMSG_ITEM_QUERY_MULTIPLE_RESPONSE` | `S->C` | `0x555160` | `0` | `0x00555040` | Static registration observed in WoW.exe. |
| `0x05A` | `CMSG_PAGE_TEXT_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x05B` | `SMSG_PAGE_TEXT_QUERY_RESPONSE` | `S->C` | `0x555460` | `0` | `0x00555095` | Static registration observed in WoW.exe. |
| `0x05C` | `CMSG_QUEST_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x05D` | `SMSG_QUEST_QUERY_RESPONSE` | `S->C` | `0x555300` | `0` | `0x00555084` | Static registration observed in WoW.exe. |
| `0x05E` | `CMSG_GAMEOBJECT_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x05F` | `SMSG_GAMEOBJECT_QUERY_RESPONSE` | `S->C` | `0x555100` | `0` | `0x0055500D` | Static registration observed in WoW.exe. |
| `0x060` | `CMSG_CREATURE_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x061` | `SMSG_CREATURE_QUERY_RESPONSE` | `S->C` | `0x5550e0` | `0` | `0x00554FFC` | Static registration observed in WoW.exe. |
| `0x062` | `CMSG_WHO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x063` | `SMSG_WHO` | `S->C` | `0x5adf60` | `0x5600c281` | `0x005ADC98` | Static registration observed in WoW.exe. |
| `0x064` | `CMSG_WHOIS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x065` | `SMSG_WHOIS` | `S->C` | `0x5adda0` | `?` | `0x005ADCA8` | Static registration observed in WoW.exe. |
| `0x066` | `CMSG_FRIEND_LIST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x067` | `SMSG_FRIEND_LIST` | `S->C` | `0x5add80` | `?` | `0x005ADCC8` | Static registration observed in WoW.exe. |
| `0x068` | `SMSG_FRIEND_STATUS` | `S->C` | `0x5add30` | `?` | `0x005ADCD8` | Static registration observed in WoW.exe. |
| `0x069` | `CMSG_ADD_FRIEND` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x06A` | `CMSG_DEL_FRIEND` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x06B` | `SMSG_IGNORE_LIST` | `S->C` | `0x5ae2b0` | `?` | `0x005ADCE8` | Static registration observed in WoW.exe. |
| `0x06C` | `CMSG_ADD_IGNORE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x06D` | `CMSG_DEL_IGNORE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x06E` | `CMSG_GROUP_INVITE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x06F` | `SMSG_GROUP_INVITE` | `S->C` | `0x5e6730` | `0` | `0x005E315F` | Static registration observed in WoW.exe. |
| `0x070` | `CMSG_GROUP_CANCEL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x071` | `SMSG_GROUP_CANCEL` | `S->C` | `0x5e6770` | `0` | `0x005E3170` | Static registration observed in WoW.exe. |
| `0x072` | `CMSG_GROUP_ACCEPT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x073` | `CMSG_GROUP_DECLINE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x074` | `SMSG_GROUP_DECLINE` | `S->C` | `0x5e67a0` | `0` | `0x005E3181` | Static registration observed in WoW.exe. |
| `0x075` | `CMSG_GROUP_UNINVITE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x076` | `CMSG_GROUP_UNINVITE_GUID` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x077` | `SMSG_GROUP_UNINVITE` | `S->C` | `0x5e6850` | `0` | `0x005E3192` | Static registration observed in WoW.exe. |
| `0x078` | `CMSG_GROUP_SET_LEADER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x079` | `SMSG_GROUP_SET_LEADER` | `S->C` | `0x5e67d0` | `0` | `0x005E31A3` | Static registration observed in WoW.exe. |
| `0x07A` | `CMSG_LOOT_METHOD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x07B` | `CMSG_GROUP_DISBAND` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x07C` | `SMSG_GROUP_DESTROYED` | `S->C` | `0x5e6880` | `0` | `0x005E31B4` | Static registration observed in WoW.exe. |
| `0x07D` | `SMSG_GROUP_LIST` | `S->C` | `0x5e6a40` | `0` | `0x005E31D6` | Static registration observed in WoW.exe. |
| `0x07E` | `SMSG_PARTY_MEMBER_STATS` | `S->C` | `0x5e5110` | `0` | `0x005E34C2` | Static registration observed in WoW.exe. |
| `0x07F` | `SMSG_PARTY_COMMAND_RESULT` | `S->C` | `0x5e68b0` | `0` | `0x005E31C5` | Static registration observed in WoW.exe. |
| `0x080` | `UMSG_UPDATE_GROUP_MEMBERS` | `Client` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x081` | `CMSG_GUILD_CREATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x082` | `CMSG_GUILD_INVITE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x083` | `SMSG_GUILD_INVITE` | `S->C` | `0x5e6f20` | `0` | `0x005E334C` | Static registration observed in WoW.exe. |
| `0x084` | `CMSG_GUILD_ACCEPT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x085` | `CMSG_GUILD_DECLINE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x086` | `SMSG_GUILD_DECLINE` | `S->C` | `0x5e6f80` | `0` | `0x005E335D` | Static registration observed in WoW.exe. |
| `0x087` | `CMSG_GUILD_INFO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x088` | `SMSG_GUILD_INFO` | `S->C` | `0x5e6fb0` | `0` | `0x005E336E` | Static registration observed in WoW.exe. |
| `0x089` | `CMSG_GUILD_ROSTER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x08A` | `SMSG_GUILD_ROSTER` | `S->C` | `0x4d0ad0` | `?` | `0x004D0A0E` | Static registration observed in WoW.exe. |
| `0x08B` | `CMSG_GUILD_PROMOTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x08C` | `CMSG_GUILD_DEMOTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x08D` | `CMSG_GUILD_LEAVE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x08E` | `CMSG_GUILD_REMOVE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x08F` | `CMSG_GUILD_DISBAND` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x090` | `CMSG_GUILD_LEADER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x091` | `CMSG_GUILD_MOTD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x092` | `SMSG_GUILD_EVENT` | `S->C` | `0x5e7180` | `0` | `0x005E337F` | Static registration observed in WoW.exe. |
| `0x093` | `SMSG_GUILD_COMMAND_RESULT` | `S->C` | `0x5e7520` | `0` | `0x005E3390` | Static registration observed in WoW.exe. |
| `0x094` | `UMSG_UPDATE_GUILD` | `Client` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x095` | `CMSG_MESSAGECHAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x096` | `SMSG_MESSAGECHAT` | `S->C` | `0x49d560` | `?` | `0x0049863A` | Static registration observed in WoW.exe. |
| `0x097` | `CMSG_JOIN_CHANNEL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x098` | `CMSG_LEAVE_CHANNEL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x099` | `SMSG_CHANNEL_NOTIFY` | `S->C` | `0x49bf80` | `?` | `0x0049861A` | Static registration observed in WoW.exe. |
| `0x09A` | `CMSG_CHANNEL_LIST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x09B` | `SMSG_CHANNEL_LIST` | `S->C` | `0x49c690` | `?` | `0x0049862A` | Static registration observed in WoW.exe. |
| `0x09C` | `CMSG_CHANNEL_PASSWORD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x09D` | `CMSG_CHANNEL_SET_OWNER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x09E` | `CMSG_CHANNEL_OWNER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x09F` | `CMSG_CHANNEL_MODERATOR` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A0` | `CMSG_CHANNEL_UNMODERATOR` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A1` | `CMSG_CHANNEL_MUTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A2` | `CMSG_CHANNEL_UNMUTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A3` | `CMSG_CHANNEL_INVITE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A4` | `CMSG_CHANNEL_KICK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A5` | `CMSG_CHANNEL_BAN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A6` | `CMSG_CHANNEL_UNBAN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A7` | `CMSG_CHANNEL_ANNOUNCEMENTS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A8` | `CMSG_CHANNEL_MODERATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0A9` | `SMSG_UPDATE_OBJECT` | `S->C` | `0x4651a0` | `0` | `0x00465157` | SMSG_UPDATE_OBJECT dispatcher. |
| `0x0AA` | `SMSG_DESTROY_OBJECT` | `S->C` | `0x4674a0` | `0` | `0x00465191` | Static registration observed in WoW.exe. |
| `0x0AB` | `CMSG_USE_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0AC` | `CMSG_OPEN_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0AD` | `CMSG_READ_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0AE` | `SMSG_READ_ITEM_OK` | `S->C` | `0x5e7d90` | `0` | `0x005E32E6` | Static registration observed in WoW.exe. |
| `0x0AF` | `SMSG_READ_ITEM_FAILED` | `S->C` | `0x5e7d90` | `0` | `0x005E32F7` | Static registration observed in WoW.exe. |
| `0x0B0` | `SMSG_ITEM_COOLDOWN` | `S->C` | `0x6e95d0` | `?` | `0x006E71BF` | Static registration observed in WoW.exe. |
| `0x0B1` | `CMSG_GAMEOBJ_USE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0B2` | `CMSG_GAMEOBJ_CHAIR_USE_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0B3` | `SMSG_GAMEOBJECT_CUSTOM_ANIM` | `S->C` | `0x5f8930` | `0` | `0x005F883D` | Static registration observed in WoW.exe. |
| `0x0B4` | `CMSG_AREATRIGGER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0B5` | `MSG_MOVE_START_FORWARD` | `Bi` | `0x603bb0` | `?` | `0x006033D0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0B6` | `MSG_MOVE_START_BACKWARD` | `Bi` | `0x603bb0` | `?` | `0x006033E0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0B7` | `MSG_MOVE_STOP` | `Bi` | `0x603bb0` | `?` | `0x006033F0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0B8` | `MSG_MOVE_START_STRAFE_LEFT` | `Bi` | `0x603bb0` | `?` | `0x00603400` | Wrapper -> `0x601580` movement dispatch. |
| `0x0B9` | `MSG_MOVE_START_STRAFE_RIGHT` | `Bi` | `0x603bb0` | `?` | `0x00603410` | Wrapper -> `0x601580` movement dispatch. |
| `0x0BA` | `MSG_MOVE_STOP_STRAFE` | `Bi` | `0x603bb0` | `?` | `0x00603420` | Wrapper -> `0x601580` movement dispatch. |
| `0x0BB` | `MSG_MOVE_JUMP` | `Bi` | `0x603bb0` | `?` | `0x00603430` | Wrapper -> `0x601580` movement dispatch. |
| `0x0BC` | `MSG_MOVE_START_TURN_LEFT` | `Bi` | `0x603bb0` | `?` | `0x00603440` | Wrapper -> `0x601580` movement dispatch. |
| `0x0BD` | `MSG_MOVE_START_TURN_RIGHT` | `Bi` | `0x603bb0` | `?` | `0x00603450` | Wrapper -> `0x601580` movement dispatch. |
| `0x0BE` | `MSG_MOVE_STOP_TURN` | `Bi` | `0x603bb0` | `?` | `0x00603460` | Wrapper -> `0x601580` movement dispatch. |
| `0x0BF` | `MSG_MOVE_START_PITCH_UP` | `Bi` | `0x603bb0` | `?` | `0x00603580` | Wrapper -> `0x601580` movement dispatch. |
| `0x0C0` | `MSG_MOVE_START_PITCH_DOWN` | `Bi` | `0x603bb0` | `?` | `0x00603590` | Wrapper -> `0x601580` movement dispatch. |
| `0x0C1` | `MSG_MOVE_STOP_PITCH` | `Bi` | `0x603bb0` | `?` | `0x006035A0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0C2` | `MSG_MOVE_SET_RUN_MODE` | `Bi` | `0x603bb0` | `?` | `0x00603470` | Wrapper -> `0x601580` movement dispatch. |
| `0x0C3` | `MSG_MOVE_SET_WALK_MODE` | `Bi` | `0x603bb0` | `?` | `0x00603480` | Wrapper -> `0x601580` movement dispatch. |
| `0x0C4` | `MSG_MOVE_TOGGLE_LOGGING` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0C5` | `MSG_MOVE_TELEPORT` | `Bi` | `0x603bb0` | `?` | `0x00603490` | Wrapper -> `0x601580` movement dispatch. |
| `0x0C6` | `MSG_MOVE_TELEPORT_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0C7` | `MSG_MOVE_TELEPORT_ACK` | `Bi` | `0x603f90` | `?` | `0x006035D0` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0C8` | `MSG_MOVE_TOGGLE_FALL_LOGGING` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0C9` | `MSG_MOVE_FALL_LAND` | `Bi` | `0x603bb0` | `?` | `0x006035C0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0CA` | `MSG_MOVE_START_SWIM` | `Bi` | `0x603bb0` | `?` | `0x00603560` | Wrapper -> `0x601580` movement dispatch. |
| `0x0CB` | `MSG_MOVE_STOP_SWIM` | `Bi` | `0x603bb0` | `?` | `0x00603570` | Wrapper -> `0x601580` movement dispatch. |
| `0x0CC` | `MSG_MOVE_SET_RUN_SPEED_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0CD` | `MSG_MOVE_SET_RUN_SPEED` | `Bi` | `0x603ae0` | `?` | `0x006034E0` | Static registration observed in WoW.exe. |
| `0x0CE` | `MSG_MOVE_SET_RUN_BACK_SPEED_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0CF` | `MSG_MOVE_SET_RUN_BACK_SPEED` | `Bi` | `0x603ae0` | `?` | `0x006034F0` | Static registration observed in WoW.exe. |
| `0x0D0` | `MSG_MOVE_SET_WALK_SPEED_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0D1` | `MSG_MOVE_SET_WALK_SPEED` | `Bi` | `0x603ae0` | `?` | `0x00603500` | Static registration observed in WoW.exe. |
| `0x0D2` | `MSG_MOVE_SET_SWIM_SPEED_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0D3` | `MSG_MOVE_SET_SWIM_SPEED` | `Bi` | `0x603ae0` | `?` | `0x00603510` | Static registration observed in WoW.exe. |
| `0x0D4` | `MSG_MOVE_SET_SWIM_BACK_SPEED_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0D5` | `MSG_MOVE_SET_SWIM_BACK_SPEED` | `Bi` | `0x603ae0` | `?` | `0x00603520` | Static registration observed in WoW.exe. |
| `0x0D6` | `MSG_MOVE_SET_ALL_SPEED_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0D7` | `MSG_MOVE_SET_TURN_RATE_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0D8` | `MSG_MOVE_SET_TURN_RATE` | `Bi` | `0x603ae0` | `?` | `0x00603530` | Static registration observed in WoW.exe. |
| `0x0D9` | `MSG_MOVE_TOGGLE_COLLISION_CHEAT` | `Bi` | `0x603bb0` | `?` | `0x006034C0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0DA` | `MSG_MOVE_SET_FACING` | `Bi` | `0x603bb0` | `?` | `0x006034A0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0DB` | `MSG_MOVE_SET_PITCH` | `Bi` | `0x603bb0` | `?` | `0x006034B0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0DC` | `MSG_MOVE_WORLDPORT_ACK` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0DD` | `SMSG_MONSTER_MOVE` | `S->C` | `0x603f00` | `?` | `0x006035F0` | Static registration observed in WoW.exe. |
| `0x0DE` | `SMSG_MOVE_WATER_WALK` | `S->C` | `0x603f90` | `?` | `0x00603690` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0DF` | `SMSG_MOVE_LAND_WALK` | `S->C` | `0x603f90` | `?` | `0x006036A0` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0E0` | `MSG_MOVE_SET_RAW_POSITION_ACK` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0E1` | `CMSG_MOVE_SET_RAW_POSITION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0E2` | `SMSG_FORCE_RUN_SPEED_CHANGE` | `S->C` | `0x603f90` | `?` | `0x00603610` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0E3` | `CMSG_FORCE_RUN_SPEED_CHANGE_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0E4` | `SMSG_FORCE_RUN_BACK_SPEED_CHANGE` | `S->C` | `0x603f90` | `?` | `0x00603620` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0E5` | `CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0E6` | `SMSG_FORCE_SWIM_SPEED_CHANGE` | `S->C` | `0x603f90` | `?` | `0x00603630` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0E7` | `CMSG_FORCE_SWIM_SPEED_CHANGE_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0E8` | `SMSG_FORCE_MOVE_ROOT` | `S->C` | `0x603f90` | `?` | `0x00603670` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0E9` | `CMSG_FORCE_MOVE_ROOT_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0EA` | `SMSG_FORCE_MOVE_UNROOT` | `S->C` | `0x603f90` | `?` | `0x00603680` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0EB` | `CMSG_FORCE_MOVE_UNROOT_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0EC` | `MSG_MOVE_ROOT` | `Bi` | `0x603bb0` | `?` | `0x00603540` | Wrapper -> `0x601580` movement dispatch. |
| `0x0ED` | `MSG_MOVE_UNROOT` | `Bi` | `0x603bb0` | `?` | `0x00603550` | Wrapper -> `0x601580` movement dispatch. |
| `0x0EE` | `MSG_MOVE_HEARTBEAT` | `Bi` | `0x603bb0` | `?` | `0x006035B0` | Wrapper -> `0x601580` movement dispatch. |
| `0x0EF` | `SMSG_MOVE_KNOCK_BACK` | `S->C` | `0x603f90` | `?` | `0x006036F0` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0F0` | `CMSG_MOVE_KNOCK_BACK_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0F1` | `MSG_MOVE_KNOCK_BACK` | `Bi` | `0x603bb0` | `?` | `0x00603720` | Wrapper -> `0x601580` movement dispatch. |
| `0x0F2` | `SMSG_MOVE_FEATHER_FALL` | `S->C` | `0x603f90` | `?` | `0x006036B0` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0F3` | `SMSG_MOVE_NORMAL_FALL` | `S->C` | `0x603f90` | `?` | `0x006036C0` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0F4` | `SMSG_MOVE_SET_HOVER` | `S->C` | `0x603f90` | `?` | `0x006036D0` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0F5` | `SMSG_MOVE_UNSET_HOVER` | `S->C` | `0x603f90` | `?` | `0x006036E0` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x0F6` | `CMSG_MOVE_HOVER_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0F7` | `MSG_MOVE_HOVER` | `Bi` | `0x603bb0` | `?` | `0x00603730` | Wrapper -> `0x601580` movement dispatch. |
| `0x0F8` | `CMSG_TRIGGER_CINEMATIC_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0F9` | `CMSG_OPENING_CINEMATIC` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0FA` | `SMSG_TRIGGER_CINEMATIC` | `S->C` | `0x5e38c0` | `0` | `0x005E35C1` | Static registration observed in WoW.exe. |
| `0x0FB` | `CMSG_NEXT_CINEMATIC_CAMERA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0FC` | `CMSG_COMPLETE_CINEMATIC` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0FD` | `SMSG_TUTORIAL_FLAGS` | `S->C` | `0x4b5700` | `0` | `0x004B536F` | Static registration observed in WoW.exe. |
| `0x0FE` | `CMSG_TUTORIAL_FLAG` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x0FF` | `CMSG_TUTORIAL_CLEAR` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x100` | `CMSG_TUTORIAL_RESET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x101` | `CMSG_STANDSTATECHANGE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x102` | `CMSG_EMOTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x103` | `SMSG_EMOTE` | `S->C` | `0x5e66b0` | `0` | `0x005E345C` | Static registration observed in WoW.exe. |
| `0x104` | `CMSG_TEXT_EMOTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x105` | `SMSG_TEXT_EMOTE` | `S->C` | `0x49dbe0` | `?` | `0x0049864A` | Static registration observed in WoW.exe. |
| `0x106` | `CMSG_AUTOEQUIP_GROUND_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x107` | `CMSG_AUTOSTORE_GROUND_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x108` | `CMSG_AUTOSTORE_LOOT_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x109` | `CMSG_STORE_LOOT_IN_SLOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x10A` | `CMSG_AUTOEQUIP_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x10B` | `CMSG_AUTOSTORE_BAG_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x10C` | `CMSG_SWAP_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x10D` | `CMSG_SWAP_INV_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x10E` | `CMSG_SPLIT_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x10F` | `CMSG_AUTOEQUIP_ITEM_SLOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x110` | `OBSOLETE_DROP_ITEM` | `?` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x111` | `CMSG_DESTROYITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x112` | `SMSG_INVENTORY_CHANGE_FAILURE` | `S->C` | `0x5e38c0` | `0` | `0x005E303E` | Static registration observed in WoW.exe. |
| `0x113` | `SMSG_OPEN_CONTAINER` | `S->C` | `0x5e38c0` | `0` | `0x005E304F` | Static registration observed in WoW.exe. |
| `0x114` | `CMSG_INSPECT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x115` | `SMSG_INSPECT` | `S->C` | `0x5e7d70` | `0` | `0x005E32D5` | Static registration observed in WoW.exe. |
| `0x116` | `CMSG_INITIATE_TRADE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x117` | `CMSG_BEGIN_TRADE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x118` | `CMSG_BUSY_TRADE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x119` | `CMSG_IGNORE_TRADE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x11A` | `CMSG_ACCEPT_TRADE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x11B` | `CMSG_UNACCEPT_TRADE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x11C` | `CMSG_CANCEL_TRADE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x11D` | `CMSG_SET_TRADE_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x11E` | `CMSG_CLEAR_TRADE_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x11F` | `CMSG_SET_TRADE_GOLD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x120` | `SMSG_TRADE_STATUS` | `S->C` | `0x5d47c0` | `?` | `0x005D47A6` | Static registration observed in WoW.exe. |
| `0x121` | `SMSG_TRADE_STATUS_EXTENDED` | `S->C` | `0x5d4990` | `?` | `0x005D47B6` | Static registration observed in WoW.exe. |
| `0x122` | `SMSG_INITIALIZE_FACTIONS` | `S->C` | `0x4d5640` | `0` | `0x004D529C` | Static registration observed in WoW.exe. |
| `0x123` | `SMSG_SET_FACTION_VISIBLE` | `S->C` | `0x4d5710` | `0` | `0x004D52BE` | Static registration observed in WoW.exe. |
| `0x124` | `SMSG_SET_FACTION_STANDING` | `S->C` | `0x4d5760` | `0` | `0x004D52CF` | Static registration observed in WoW.exe. |
| `0x125` | `CMSG_SET_FACTION_ATWAR` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x126` | `CMSG_SET_FACTION_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x127` | `SMSG_SET_PROFICIENCY` | `S->C` | `0x5e7b70` | `0` | `0x005E32A2` | Static registration observed in WoW.exe. |
| `0x128` | `CMSG_SET_ACTION_BUTTON` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x129` | `SMSG_ACTION_BUTTONS` | `S->C` | `0x5e6680` | `0` | `0x005E314E` | Static registration observed in WoW.exe. |
| `0x12A` | `SMSG_INITIAL_SPELLS` | `S->C` | `0x5e6510` | `0` | `0x005E313D` | Static registration observed in WoW.exe. |
| `0x12B` | `SMSG_LEARNED_SPELL` | `S->C` | `0x5e61c0` | `0` | `0x005E311B` | Static registration observed in WoW.exe. |
| `0x12C` | `SMSG_SUPERCEDED_SPELL` | `S->C` | `0x5e6330` | `0` | `0x005E312C` | Static registration observed in WoW.exe. |
| `0x12D` | `CMSG_NEW_SPELL_SLOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x12E` | `CMSG_CAST_SPELL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x12F` | `CMSG_CANCEL_CAST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x130` | `SMSG_CAST_FAILED` | `S->C` | `0x6e7330` | `?` | `0x006E715F` | Static registration observed in WoW.exe. |
| `0x131` | `SMSG_SPELL_START` | `S->C` | `0x6e7640` | `?` | `0x006E716F` | Static registration observed in WoW.exe. |
| `0x132` | `SMSG_SPELL_GO` | `S->C` | `0x6e7640` | `?` | `0x006E717F` | Static registration observed in WoW.exe. |
| `0x133` | `SMSG_SPELL_FAILURE` | `S->C` | `0x6e8d80` | `?` | `0x006E718F` | Static registration observed in WoW.exe. |
| `0x134` | `SMSG_SPELL_COOLDOWN` | `S->C` | `0x6e9460` | `?` | `0x006E71AF` | Static registration observed in WoW.exe. |
| `0x135` | `SMSG_COOLDOWN_EVENT` | `S->C` | `0x6e9670` | `?` | `0x006E71CF` | Static registration observed in WoW.exe. |
| `0x136` | `CMSG_CANCEL_AURA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x137` | `SMSG_UPDATE_AURA_DURATION` | `S->C` | `0x5e38c0` | `0` | `0x005E3429` | Static registration observed in WoW.exe. |
| `0x138` | `SMSG_PET_CAST_FAILED` | `S->C` | `0x6e8eb0` | `?` | `0x006E719F` | Static registration observed in WoW.exe. |
| `0x139` | `MSG_CHANNEL_START` | `Bi` | `0x6e7550` | `?` | `0x006E721F` | Static registration observed in WoW.exe. |
| `0x13A` | `MSG_CHANNEL_UPDATE` | `Bi` | `0x6e75f0` | `?` | `0x006E722F` | Static registration observed in WoW.exe. |
| `0x13B` | `CMSG_CANCEL_CHANNELLING` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x13C` | `SMSG_AI_REACTION` | `S->C` | `0x604060` | `?` | `0x00603710` | Static registration observed in WoW.exe. |
| `0x13D` | `CMSG_SET_SELECTION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x13E` | `CMSG_SET_TARGET_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x13F` | `CMSG_UNUSED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x140` | `CMSG_UNUSED2` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x141` | `CMSG_ATTACKSWING` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x142` | `CMSG_ATTACKSTOP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x143` | `SMSG_ATTACKSTART` | `S->C` | `0x6255b0` | `0` | `0x0062552C` | Static registration observed in WoW.exe. |
| `0x144` | `SMSG_ATTACKSTOP` | `S->C` | `0x6255b0` | `0` | `0x0062553D` | Static registration observed in WoW.exe. |
| `0x145` | `SMSG_ATTACKSWING_NOTINRANGE` | `S->C` | `0x6255b0` | `0` | `0x0062555F` | Static registration observed in WoW.exe. |
| `0x146` | `SMSG_ATTACKSWING_BADFACING` | `S->C` | `0x6255b0` | `0` | `0x00625570` | Static registration observed in WoW.exe. |
| `0x147` | `SMSG_ATTACKSWING_NOTSTANDING` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x148` | `SMSG_ATTACKSWING_DEADTARGET` | `S->C` | `0x6255b0` | `0` | `0x00625581` | Static registration observed in WoW.exe. |
| `0x149` | `SMSG_ATTACKSWING_CANT_ATTACK` | `S->C` | `0x6255b0` | `0` | `0x00625592` | Static registration observed in WoW.exe. |
| `0x14A` | `SMSG_ATTACKERSTATEUPDATE` | `S->C` | `0x6255b0` | `0` | `0x0062554E` | Static registration observed in WoW.exe. |
| `0x14B` | `SMSG_VICTIMSTATEUPDATE_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x14C` | `SMSG_DAMAGE_DONE_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x14D` | `SMSG_DAMAGE_TAKEN_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x14E` | `SMSG_CANCEL_COMBAT` | `S->C` | `0x5e7dd0` | `0` | `0x005E3308` | Static registration observed in WoW.exe. |
| `0x14F` | `SMSG_PLAYER_COMBAT_XP_GAIN_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x150` | `SMSG_SPELLHEALLOG` | `S->C` | `0x5e89c0` | `0` | `0x005E379D` | Static registration observed in WoW.exe. |
| `0x151` | `SMSG_SPELLENERGIZELOG` | `S->C` | `0x5e8a90` | `0` | `0x005E37AE` | Static registration observed in WoW.exe. |
| `0x152` | `CMSG_SHEATHE_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x153` | `CMSG_SAVE_PLAYER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x154` | `CMSG_SETDEATHBINDPOINT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x155` | `SMSG_BINDPOINTUPDATE` | `S->C` | `0x5e38c0` | `0` | `0x005E343A` | Static registration observed in WoW.exe. |
| `0x156` | `CMSG_GETDEATHBINDZONE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x157` | `SMSG_BINDZONEREPLY` | `S->C` | `0x5e38c0` | `0` | `0x005E344B` | Static registration observed in WoW.exe. |
| `0x158` | `SMSG_PLAYERBOUND` | `S->C` | `0x5e38c0` | `0` | `0x005E346D` | Static registration observed in WoW.exe. |
| `0x159` | `SMSG_CLIENT_CONTROL_UPDATE` | `S->C` | `0x603ea0` | `?` | `0x006038D0` | Static registration observed in WoW.exe. |
| `0x15A` | `CMSG_REPOP_REQUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x15B` | `SMSG_RESURRECT_REQUEST` | `S->C` | `0x5e7bc0` | `0` | `0x005E32B3` | Static registration observed in WoW.exe. |
| `0x15C` | `CMSG_RESURRECT_RESPONSE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x15D` | `CMSG_LOOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x15E` | `CMSG_LOOT_MONEY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x15F` | `CMSG_LOOT_RELEASE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x160` | `SMSG_LOOT_RESPONSE` | `S->C` | `0x5e6010` | `0` | `0x005E30B5` | Static registration observed in WoW.exe. |
| `0x161` | `SMSG_LOOT_RELEASE_RESPONSE` | `S->C` | `0x5e6010` | `0` | `0x005E30C6` | Static registration observed in WoW.exe. |
| `0x162` | `SMSG_LOOT_REMOVED` | `S->C` | `0x5e6010` | `0` | `0x005E30D7` | Static registration observed in WoW.exe. |
| `0x163` | `SMSG_LOOT_MONEY_NOTIFY` | `S->C` | `0x5e6010` | `0` | `0x005E30E8` | Static registration observed in WoW.exe. |
| `0x164` | `SMSG_LOOT_ITEM_NOTIFY` | `S->C` | `0x5e6010` | `0` | `0x005E30F9` | Static registration observed in WoW.exe. |
| `0x165` | `SMSG_LOOT_CLEAR_MONEY` | `S->C` | `0x5e6010` | `0` | `0x005E310A` | Static registration observed in WoW.exe. |
| `0x166` | `SMSG_ITEM_PUSH_RESULT` | `S->C` | `0x5e38c0` | `0` | `0x005E3060` | Static registration observed in WoW.exe. |
| `0x167` | `SMSG_DUEL_REQUESTED` | `S->C` | `0x4d49d0` | `0` | `0x004D471C` | Static registration observed in WoW.exe. |
| `0x168` | `SMSG_DUEL_OUTOFBOUNDS` | `S->C` | `0x4d4aa0` | `0` | `0x004D472D` | Static registration observed in WoW.exe. |
| `0x169` | `SMSG_DUEL_INBOUNDS` | `S->C` | `0x4d4ac0` | `0` | `0x004D473E` | Static registration observed in WoW.exe. |
| `0x16A` | `SMSG_DUEL_COMPLETE` | `S->C` | `0x4d4b20` | `0` | `0x004D4760` | Static registration observed in WoW.exe. |
| `0x16B` | `SMSG_DUEL_WINNER` | `S->C` | `0x4d4ba0` | `0` | `0x004D4771` | Static registration observed in WoW.exe. |
| `0x16C` | `CMSG_DUEL_ACCEPTED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x16D` | `CMSG_DUEL_CANCELLED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x16E` | `SMSG_MOUNTRESULT` | `S->C` | `0x5e38c0` | `0` | `0x005E301C` | Static registration observed in WoW.exe. |
| `0x16F` | `SMSG_DISMOUNTRESULT` | `S->C` | `0x5e38c0` | `0` | `0x005E302D` | Static registration observed in WoW.exe. |
| `0x170` | `SMSG_PUREMOUNT_CANCELLED_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x171` | `CMSG_MOUNTSPECIAL_ANIM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x172` | `SMSG_MOUNTSPECIAL_ANIM` | `S->C` | `0x603ff0` | `?` | `0x00603700` | Static registration observed in WoW.exe. |
| `0x173` | `SMSG_PET_TAME_FAILURE` | `S->C` | `0x6e97e0` | `?` | `0x006E71FF` | Static registration observed in WoW.exe. |
| `0x174` | `CMSG_PET_SET_ACTION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x175` | `CMSG_PET_ACTION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x176` | `CMSG_PET_ABANDON` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x177` | `CMSG_PET_RENAME` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x178` | `SMSG_PET_NAME_INVALID` | `S->C` | `0x5e38c0` | `0` | `0x005E34A0` | Static registration observed in WoW.exe. |
| `0x179` | `SMSG_PET_SPELLS` | `S->C` | `0x4bd990` | `?` | `0x004BC6B6` | Static registration observed in WoW.exe. |
| `0x17A` | `SMSG_PET_MODE` | `S->C` | `0x4bdb10` | `?` | `0x004BC6C6` | Static registration observed in WoW.exe. |
| `0x17B` | `CMSG_GOSSIP_HELLO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x17C` | `CMSG_GOSSIP_SELECT_OPTION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x17D` | `SMSG_GOSSIP_MESSAGE` | `S->C` | `0x4e26e0` | `0` | `0x004E1EFC` | Static registration observed in WoW.exe. |
| `0x17E` | `SMSG_GOSSIP_COMPLETE` | `S->C` | `0x4e2800` | `0` | `0x004E1F0D` | Static registration observed in WoW.exe. |
| `0x17F` | `CMSG_NPC_TEXT_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x180` | `SMSG_NPC_TEXT_UPDATE` | `S->C` | `0x555180` | `0` | `0x00555051` | Static registration observed in WoW.exe. |
| `0x181` | `SMSG_NPC_WONT_TALK` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x182` | `CMSG_QUESTGIVER_STATUS_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x183` | `SMSG_QUESTGIVER_STATUS` | `S->C` | `0x5e59b0` | `0` | `0x005E325E` | Static registration observed in WoW.exe. |
| `0x184` | `CMSG_QUESTGIVER_HELLO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x185` | `SMSG_QUESTGIVER_QUEST_LIST` | `S->C` | `0x5e59b0` | `0` | `0x005E31E7` | Static registration observed in WoW.exe. |
| `0x186` | `CMSG_QUESTGIVER_QUERY_QUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x187` | `CMSG_QUESTGIVER_QUEST_AUTOLAUNCH` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x188` | `SMSG_QUESTGIVER_QUEST_DETAILS` | `S->C` | `0x5e59b0` | `0` | `0x005E3209` | Static registration observed in WoW.exe. |
| `0x189` | `CMSG_QUESTGIVER_ACCEPT_QUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x18A` | `CMSG_QUESTGIVER_COMPLETE_QUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x18B` | `SMSG_QUESTGIVER_REQUEST_ITEMS` | `S->C` | `0x5e59b0` | `0` | `0x005E321A` | Static registration observed in WoW.exe. |
| `0x18C` | `CMSG_QUESTGIVER_REQUEST_REWARD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x18D` | `SMSG_QUESTGIVER_OFFER_REWARD` | `S->C` | `0x5e59b0` | `0` | `0x005E322B` | Static registration observed in WoW.exe. |
| `0x18E` | `CMSG_QUESTGIVER_CHOOSE_REWARD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x18F` | `SMSG_QUESTGIVER_QUEST_INVALID` | `S->C` | `0x5e59b0` | `0` | `0x005E31F8` | Static registration observed in WoW.exe. |
| `0x190` | `CMSG_QUESTGIVER_CANCEL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x191` | `SMSG_QUESTGIVER_QUEST_COMPLETE` | `S->C` | `0x5e59b0` | `0` | `0x005E323C` | Static registration observed in WoW.exe. |
| `0x192` | `SMSG_QUESTGIVER_QUEST_FAILED` | `S->C` | `0x5e59b0` | `0` | `0x005E324D` | Static registration observed in WoW.exe. |
| `0x193` | `CMSG_QUESTLOG_SWAP_QUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x194` | `CMSG_QUESTLOG_REMOVE_QUEST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x195` | `SMSG_QUESTLOG_FULL` | `S->C` | `0x5e59b0` | `0` | `0x005E326F` | Static registration observed in WoW.exe. |
| `0x196` | `SMSG_QUESTUPDATE_FAILED` | `S->C` | `0x5e5ad0` | `0` | `0x005E34E4` | Static registration observed in WoW.exe. |
| `0x197` | `SMSG_QUESTUPDATE_FAILEDTIMER` | `S->C` | `0x5e5ad0` | `0` | `0x005E34F5` | Static registration observed in WoW.exe. |
| `0x198` | `SMSG_QUESTUPDATE_COMPLETE` | `S->C` | `0x5e5ad0` | `0` | `0x005E3506` | Static registration observed in WoW.exe. |
| `0x199` | `SMSG_QUESTUPDATE_ADD_KILL` | `S->C` | `0x5e5ad0` | `0` | `0x005E3517` | Static registration observed in WoW.exe. |
| `0x19A` | `SMSG_QUESTUPDATE_ADD_ITEM` | `S->C` | `0x5e5ad0` | `0` | `0x005E3528` | Static registration observed in WoW.exe. |
| `0x19B` | `CMSG_QUEST_CONFIRM_ACCEPT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x19C` | `SMSG_QUEST_CONFIRM_ACCEPT` | `S->C` | `0x5e5eb0` | `0` | `0x005E3539` | Static registration observed in WoW.exe. |
| `0x19D` | `CMSG_PUSHQUESTTOPARTY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x19E` | `CMSG_LIST_INVENTORY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x19F` | `SMSG_LIST_INVENTORY` | `S->C` | `0x5e5910` | `0` | `0x005E3071` | Static registration observed in WoW.exe. |
| `0x1A0` | `CMSG_SELL_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1A1` | `SMSG_SELL_ITEM` | `S->C` | `0x5e5910` | `0` | `0x005E30A4` | Static registration observed in WoW.exe. |
| `0x1A2` | `CMSG_BUY_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1A3` | `CMSG_BUY_ITEM_IN_SLOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1A4` | `SMSG_BUY_ITEM` | `S->C` | `0x5e5910` | `0` | `0x005E3093` | Static registration observed in WoW.exe. |
| `0x1A5` | `SMSG_BUY_FAILED` | `S->C` | `0x5e5910` | `0` | `0x005E3082` | Static registration observed in WoW.exe. |
| `0x1A6` | `CMSG_TAXICLEARALLNODES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1A7` | `CMSG_TAXIENABLEALLNODES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1A8` | `CMSG_TAXISHOWNODES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1A9` | `SMSG_SHOWTAXINODES` | `S->C` | `0x5e38c0` | `0` | `0x005E332A` | Static registration observed in WoW.exe. |
| `0x1AA` | `CMSG_TAXINODE_STATUS_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1AB` | `SMSG_TAXINODE_STATUS` | `S->C` | `0x5e38c0` | `0` | `0x005E3319` | Static registration observed in WoW.exe. |
| `0x1AC` | `CMSG_TAXIQUERYAVAILABLENODES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1AD` | `CMSG_ACTIVATETAXI` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1AE` | `SMSG_ACTIVATETAXIREPLY` | `S->C` | `0x5e38c0` | `0` | `0x005E333B` | Static registration observed in WoW.exe. |
| `0x1AF` | `SMSG_NEW_TAXI_PATH` | `S->C` | `0x5e38c0` | `0` | `0x005E348F` | Static registration observed in WoW.exe. |
| `0x1B0` | `CMSG_TRAINER_LIST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1B1` | `SMSG_TRAINER_LIST` | `S->C` | `0x5e5f10` | `0` | `0x005E3280` | Static registration observed in WoW.exe. |
| `0x1B2` | `CMSG_TRAINER_BUY_SPELL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1B3` | `SMSG_TRAINER_BUY_SUCCEEDED` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1B4` | `SMSG_TRAINER_BUY_FAILED` | `S->C` | `0x5e5f10` | `0` | `0x005E3291` | Static registration observed in WoW.exe. |
| `0x1B5` | `CMSG_BINDER_ACTIVATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1B6` | `SMSG_PLAYERBINDERROR` | `S->C` | `0x5e38c0` | `0` | `0x005E347E` | Static registration observed in WoW.exe. |
| `0x1B7` | `CMSG_BANKER_ACTIVATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1B8` | `SMSG_SHOW_BANK` | `S->C` | `0x5e38c0` | `0` | `0x005E354A` | Static registration observed in WoW.exe. |
| `0x1B9` | `CMSG_BUY_BANK_SLOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1BA` | `SMSG_BUY_BANK_SLOT_RESULT` | `S->C` | `0x5e38c0` | `0` | `0x005E355B` | Static registration observed in WoW.exe. |
| `0x1BB` | `CMSG_PETITION_SHOWLIST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1BC` | `SMSG_PETITION_SHOWLIST` | `S->C` | `0x5e5050` | `0` | `0x005E33C3` | Static registration observed in WoW.exe. |
| `0x1BD` | `CMSG_PETITION_BUY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1BE` | `CMSG_PETITION_SHOW_SIGNATURES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1BF` | `SMSG_PETITION_SHOW_SIGNATURES` | `S->C` | `0x5e5050` | `0` | `0x005E33D4` | Static registration observed in WoW.exe. |
| `0x1C0` | `CMSG_PETITION_SIGN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1C1` | `SMSG_PETITION_SIGN_RESULTS` | `S->C` | `0x5e5050` | `0` | `0x005E33E5` | Static registration observed in WoW.exe. |
| `0x1C2` | `MSG_PETITION_DECLINE` | `Bi` | `0x5e5050` | `0` | `0x005E33F6` | Static registration observed in WoW.exe. |
| `0x1C3` | `CMSG_OFFER_PETITION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1C4` | `CMSG_TURN_IN_PETITION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1C5` | `SMSG_TURN_IN_PETITION_RESULTS` | `S->C` | `0x5e5050` | `0` | `0x005E3407` | Static registration observed in WoW.exe. |
| `0x1C6` | `CMSG_PETITION_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1C7` | `SMSG_PETITION_QUERY_RESPONSE` | `S->C` | `0x555500` | `0` | `0x005550B7` | Static registration observed in WoW.exe. |
| `0x1C8` | `SMSG_FISH_NOT_HOOKED` | `S->C` | `0x5e38c0` | `0` | `0x005E3605` | Static registration observed in WoW.exe. |
| `0x1C9` | `SMSG_FISH_ESCAPED` | `S->C` | `0x5e38c0` | `0` | `0x005E3616` | Static registration observed in WoW.exe. |
| `0x1CA` | `CMSG_BUG` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1CB` | `SMSG_NOTIFICATION` | `S->C` | `0x401800` | `0` | `0x00401734` | Static registration observed in WoW.exe. |
| `0x1CC` | `CMSG_PLAYED_TIME` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1CD` | `SMSG_PLAYED_TIME` | `S->C` | `0x401850` | `0` | `0x00401745` | Static registration observed in WoW.exe. |
| `0x1CE` | `CMSG_QUERY_TIME` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1CF` | `SMSG_QUERY_TIME_RESPONSE` | `S->C` | `0x4de400` | `?` | `0x004DE3EF` | Static registration observed in WoW.exe. |
| `0x1D0` | `SMSG_LOG_XPGAIN` | `S->C` | `0x637e30` | `0` | `0x00637E1C` | Static registration observed in WoW.exe. |
| `0x1D1` | `SMSG_AURACASTLOG` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1D2` | `CMSG_RECLAIM_CORPSE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1D3` | `CMSG_WRAP_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1D4` | `SMSG_LEVELUP_INFO` | `S->C` | `0x5e38c0` | `0` | `0x005E356C` | Static registration observed in WoW.exe. |
| `0x1D5` | `MSG_MINIMAP_PING` | `Bi` | `0x5e38c0` | `0` | `0x005E357D` | Static registration observed in WoW.exe. |
| `0x1D6` | `SMSG_RESISTLOG` | `S->C` | `0x630170` | `0` | `0x0063015C` | Static registration observed in WoW.exe. |
| `0x1D7` | `SMSG_ENCHANTMENTLOG` | `S->C` | `0x628ea0` | `0` | `0x00626D81` | Static registration observed in WoW.exe. |
| `0x1D8` | `CMSG_SET_SKILL_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1D9` | `SMSG_START_MIRROR_TIMER` | `S->C` | `0x5e7990` | `0` | `0x005E358E` | Static registration observed in WoW.exe. |
| `0x1DA` | `SMSG_PAUSE_MIRROR_TIMER` | `S->C` | `0x5e7990` | `0` | `0x005E359F` | Static registration observed in WoW.exe. |
| `0x1DB` | `SMSG_STOP_MIRROR_TIMER` | `S->C` | `0x5e7990` | `0` | `0x005E35B0` | Static registration observed in WoW.exe. |
| `0x1DC` | `CMSG_PING` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1DD` | `SMSG_PONG` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1DE` | `SMSG_CLEAR_COOLDOWN` | `S->C` | `0x6e9670` | `?` | `0x006E71DF` | Static registration observed in WoW.exe. |
| `0x1DF` | `SMSG_GAMEOBJECT_PAGETEXT` | `S->C` | `0x5f88c0` | `0` | `0x005F882C` | Static registration observed in WoW.exe. |
| `0x1E0` | `CMSG_SETSHEATHED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1E1` | `SMSG_COOLDOWN_CHEAT` | `S->C` | `0x6e9730` | `?` | `0x006E71EF` | Static registration observed in WoW.exe. |
| `0x1E2` | `SMSG_SPELL_DELAYED` | `S->C` | `0x6e74f0` | `?` | `0x006E720F` | Static registration observed in WoW.exe. |
| `0x1E3` | `CMSG_PLAYER_MACRO_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1E4` | `SMSG_PLAYER_MACRO_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1E5` | `CMSG_GHOST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1E6` | `CMSG_GM_INVIS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1E7` | `SMSG_INVALID_PROMOTION_CODE` | `S->C` | `0x48f690` | `?` | `0x0048F54F` | Static registration observed in WoW.exe. |
| `0x1E8` | `MSG_GM_BIND_OTHER` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1E9` | `MSG_GM_SUMMON` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1EA` | `SMSG_ITEM_TIME_UPDATE` | `S->C` | `0x5e4f30` | `0` | `0x005E35D2` | Static registration observed in WoW.exe. |
| `0x1EB` | `SMSG_ITEM_ENCHANT_TIME_UPDATE` | `S->C` | `0x5e4f30` | `0` | `0x005E35E3` | Static registration observed in WoW.exe. |
| `0x1EC` | `SMSG_AUTH_CHALLENGE` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3AC4` | Static registration observed in WoW.exe. |
| `0x1ED` | `CMSG_AUTH_SESSION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1EE` | `SMSG_AUTH_RESPONSE` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3AD6` | Static registration observed in WoW.exe. |
| `0x1EF` | `MSG_GM_SHOWLABEL` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1F0` | `CMSG_PET_CAST_SPELL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1F1` | `MSG_SAVE_GUILD_EMBLEM` | `Bi` | `0x5e70f0` | `0` | `0x005E33A1` | Static registration observed in WoW.exe. |
| `0x1F2` | `MSG_TABARDVENDOR_ACTIVATE` | `Bi` | `0x5e70c0` | `0` | `0x005E33B2` | Static registration observed in WoW.exe. |
| `0x1F3` | `SMSG_PLAY_SPELL_VISUAL` | `S->C` | `0x6e98d0` | `?` | `0x006E723F` | Static registration observed in WoW.exe. |
| `0x1F4` | `CMSG_ZONEUPDATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1F5` | `SMSG_PARTYKILLLOG` | `S->C` | `0x628890` | `0` | `0x00626D92` | Static registration observed in WoW.exe. |
| `0x1F6` | `SMSG_COMPRESSED_UPDATE_OBJECT` | `S->C` | `0x4672f0` | `0` | `0x00465174` | Static registration observed in WoW.exe. |
| `0x1F7` | `SMSG_PLAY_SPELL_IMPACT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1F8` | `SMSG_EXPLORATION_EXPERIENCE` | `S->C` | `0x5e38c0` | `0` | `0x005E34B1` | Static registration observed in WoW.exe. |
| `0x1F9` | `CMSG_GM_SET_SECURITY_GROUP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1FA` | `CMSG_GM_NUKE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1FB` | `MSG_RANDOM_ROLL` | `Bi` | `0x5e38c0` | `0` | `0x005E35F4` | Static registration observed in WoW.exe. |
| `0x1FC` | `SMSG_ENVIRONMENTALDAMAGELOG` | `S->C` | `0x6255b0` | `0` | `0x006255A3` | Static registration observed in WoW.exe. |
| `0x1FD` | `CMSG_RWHOIS_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x1FE` | `SMSG_RWHOIS` | `S->C` | `0x5adde0` | `?` | `0x005ADCB8` | Static registration observed in WoW.exe. |
| `0x1FF` | `MSG_LOOKING_FOR_GROUP` | `Bi` | `0x4e8dc0` | `?` | `0x004E7E30` | Static registration observed in WoW.exe. |
| `0x200` | `CMSG_SET_LOOKING_FOR_GROUP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x201` | `CMSG_UNLEARN_SPELL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x202` | `CMSG_UNLEARN_SKILL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x203` | `SMSG_REMOVED_SPELL` | `S->C` | `0x5e38c0` | `0` | `0x005E3627` | Static registration observed in WoW.exe. |
| `0x204` | `CMSG_DECHARGE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x205` | `CMSG_GMTICKET_CREATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x206` | `SMSG_GMTICKET_CREATE` | `S->C` | `0x5e38c0` | `0` | `0x005E366B` | Static registration observed in WoW.exe. |
| `0x207` | `CMSG_GMTICKET_UPDATETEXT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x208` | `SMSG_GMTICKET_UPDATETEXT` | `S->C` | `0x5e38c0` | `0` | `0x005E367C` | Static registration observed in WoW.exe. |
| `0x209` | `SMSG_ACCOUNT_DATA_TIMES` | `S->C` | `0x5af8e0` | `0` | `0x005AF863` | Static registration observed in WoW.exe. |
| `0x20A` | `CMSG_REQUEST_ACCOUNT_DATA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x20B` | `CMSG_UPDATE_ACCOUNT_DATA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x20C` | `SMSG_UPDATE_ACCOUNT_DATA` | `S->C` | `0x5afc60` | `0` | `0x005AF874` | Static registration observed in WoW.exe. |
| `0x20D` | `SMSG_CLEAR_FAR_SIGHT_IMMEDIATE` | `S->C` | `0x5e38c0` | `0` | `0x005E365A` | Static registration observed in WoW.exe. |
| `0x20E` | `SMSG_POWERGAINLOG_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x20F` | `CMSG_GM_TEACH` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x210` | `CMSG_GM_CREATE_ITEM_TARGET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x211` | `CMSG_GMTICKET_GETTICKET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x212` | `SMSG_GMTICKET_GETTICKET` | `S->C` | `0x5e38c0` | `0` | `0x005E368D` | Static registration observed in WoW.exe. |
| `0x213` | `CMSG_UNLEARN_TALENTS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x214` | `SMSG_GAMEOBJECT_SPAWN_ANIM_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x215` | `SMSG_GAMEOBJECT_DESPAWN_ANIM` | `S->C` | `0x5f8990` | `0` | `0x005F884E` | Static registration observed in WoW.exe. |
| `0x216` | `MSG_CORPSE_QUERY` | `Bi` | `0x48f690` | `?` | `0x0048F49F` | Static registration observed in WoW.exe. |
| `0x217` | `CMSG_GMTICKET_DELETETICKET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x218` | `SMSG_GMTICKET_DELETETICKET` | `S->C` | `0x5e38c0` | `0` | `0x005E369E` | Static registration observed in WoW.exe. |
| `0x219` | `SMSG_CHAT_WRONG_FACTION` | `S->C` | `0x5e38c0` | `0` | `0x005E36AF` | Static registration observed in WoW.exe. |
| `0x21A` | `CMSG_GMTICKET_SYSTEMSTATUS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x21B` | `SMSG_GMTICKET_SYSTEMSTATUS` | `S->C` | `0x5e38c0` | `0` | `0x005E36E2` | Static registration observed in WoW.exe. |
| `0x21C` | `CMSG_SPIRIT_HEALER_ACTIVATE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x21D` | `CMSG_SET_STAT_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x21E` | `SMSG_SET_REST_START` | `S->C` | `0x5e38c0` | `0` | `0x005E36F3` | Static registration observed in WoW.exe. |
| `0x21F` | `CMSG_SKILL_BUY_STEP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x220` | `CMSG_SKILL_BUY_RANK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x221` | `CMSG_XP_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x222` | `SMSG_SPIRIT_HEALER_CONFIRM` | `S->C` | `0x5e38c0` | `0` | `0x005E3704` | Static registration observed in WoW.exe. |
| `0x223` | `CMSG_CHARACTER_POINT_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x224` | `SMSG_GOSSIP_POI` | `S->C` | `0x4e2840` | `0` | `0x004E1F1E` | Static registration observed in WoW.exe. |
| `0x225` | `CMSG_CHAT_IGNORED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x226` | `CMSG_GM_VISION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x227` | `CMSG_SERVER_COMMAND` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x228` | `CMSG_GM_SILENCE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x229` | `CMSG_GM_REVEALTO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x22A` | `CMSG_GM_RESURRECT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x22B` | `CMSG_GM_SUMMONMOB` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x22C` | `CMSG_GM_MOVECORPSE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x22D` | `CMSG_GM_FREEZE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x22E` | `CMSG_GM_UBERINVIS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x22F` | `CMSG_GM_REQUEST_PLAYER_INFO` | `C->S` | `0x48f690` | `?` | `0x0048F4CF` | Static registration observed in WoW.exe. |
| `0x230` | `SMSG_GM_PLAYER_INFO` | `S->C` | `0x48f690` | `?` | `0x0048F4BF` | Static registration observed in WoW.exe. |
| `0x231` | `CMSG_GUILD_RANK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x232` | `CMSG_GUILD_ADD_RANK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x233` | `CMSG_GUILD_DEL_RANK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x234` | `CMSG_GUILD_SET_PUBLIC_NOTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x235` | `CMSG_GUILD_SET_OFFICER_NOTE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x236` | `SMSG_LOGIN_VERIFY_WORLD` | `S->C` | `0x401de0` | `0` | `0x00401789` | SMSG_LOGIN_VERIFY_WORLD world-entry verifier. |
| `0x237` | `CMSG_CLEAR_EXPLORATION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x238` | `CMSG_SEND_MAIL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x239` | `SMSG_SEND_MAIL_RESULT` | `S->C` | `0x4ad050` | `0` | `0x004ACAD0` | Static registration observed in WoW.exe. |
| `0x23A` | `CMSG_GET_MAIL_LIST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x23B` | `SMSG_MAIL_LIST_RESULT` | `S->C` | `0x4ad1b0` | `0` | `0x004ACAE1` | Static registration observed in WoW.exe. |
| `0x23C` | `CMSG_BATTLEFIELD_LIST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x23D` | `SMSG_BATTLEFIELD_LIST` | `S->C` | `0x4aa6c0` | `?` | `0x004A9C4F` | Static registration observed in WoW.exe. |
| `0x23E` | `CMSG_BATTLEFIELD_JOIN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x23F` | `SMSG_BATTLEFIELD_WIN_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x240` | `SMSG_BATTLEFIELD_LOSE_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x241` | `CMSG_TAXICLEARNODE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x242` | `CMSG_TAXIENABLENODE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x243` | `CMSG_ITEM_TEXT_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x244` | `SMSG_ITEM_TEXT_QUERY_RESPONSE` | `S->C` | `0x5555e0` | `0` | `0x005550C8` | Static registration observed in WoW.exe. |
| `0x245` | `CMSG_MAIL_TAKE_MONEY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x246` | `CMSG_MAIL_TAKE_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x247` | `CMSG_MAIL_MARK_AS_READ` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x248` | `CMSG_MAIL_RETURN_TO_SENDER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x249` | `CMSG_MAIL_DELETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x24A` | `CMSG_MAIL_CREATE_TEXT_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x24B` | `SMSG_SPELLLOGMISS` | `S->C` | `0x5e7e00` | `0` | `0x005E3759` | Static registration observed in WoW.exe. |
| `0x24C` | `SMSG_SPELLLOGEXECUTE` | `S->C` | `0x5e7f90` | `0` | `0x005E3748` | Static registration observed in WoW.exe. |
| `0x24D` | `SMSG_DEBUGAURAPROC` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x24E` | `SMSG_PERIODICAURALOG` | `S->C` | `0x626dd0` | `0` | `0x00626D70` | Static registration observed in WoW.exe. |
| `0x24F` | `SMSG_SPELLDAMAGESHIELD` | `S->C` | `0x5e84e0` | `0` | `0x005E376A` | Static registration observed in WoW.exe. |
| `0x250` | `SMSG_SPELLNONMELEEDAMAGELOG` | `S->C` | `0x5e85e0` | `0` | `0x005E378C` | Static registration observed in WoW.exe. |
| `0x251` | `CMSG_LEARN_TALENT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x252` | `SMSG_RESURRECT_FAILED` | `S->C` | `0x5e8f10` | `0` | `0x005E37E1` | Static registration observed in WoW.exe. |
| `0x253` | `CMSG_TOGGLE_PVP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x254` | `SMSG_ZONE_UNDER_ATTACK` | `S->C` | `0x49dcc0` | `?` | `0x0049865A` | Static registration observed in WoW.exe. |
| `0x255` | `MSG_AUCTION_HELLO` | `Bi` | `0x4cc420` | `?` | `0x004CC0FF` | Static registration observed in WoW.exe. |
| `0x256` | `CMSG_AUCTION_SELL_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x257` | `CMSG_AUCTION_REMOVE_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x258` | `CMSG_AUCTION_LIST_ITEMS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x259` | `CMSG_AUCTION_LIST_OWNER_ITEMS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x25A` | `CMSG_AUCTION_PLACE_BID` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x25B` | `SMSG_AUCTION_COMMAND_RESULT` | `S->C` | `0x4cc460` | `?` | `0x004CC10F` | Static registration observed in WoW.exe. |
| `0x25C` | `SMSG_AUCTION_LIST_RESULT` | `S->C` | `0x4cc7f0` | `?` | `0x004CC11F` | Static registration observed in WoW.exe. |
| `0x25D` | `SMSG_AUCTION_OWNER_LIST_RESULT` | `S->C` | `0x4cca30` | `?` | `0x004CC12F` | Static registration observed in WoW.exe. |
| `0x25E` | `SMSG_AUCTION_BIDDER_NOTIFICATION` | `S->C` | `0x4cced0` | `?` | `0x004CC14F` | Static registration observed in WoW.exe. |
| `0x25F` | `SMSG_AUCTION_OWNER_NOTIFICATION` | `S->C` | `0x4cd1f0` | `?` | `0x004CC15F` | Static registration observed in WoW.exe. |
| `0x260` | `SMSG_PROCRESIST` | `S->C` | `0x6289a0` | `0` | `0x00626DA3` | Static registration observed in WoW.exe. |
| `0x261` | `SMSG_STANDSTATE_CHANGE_FAILURE_OBSOLETE` | `S->C` | `0x603e00` | `?` | `0x006038A0` | Static registration observed in WoW.exe. |
| `0x262` | `SMSG_DISPEL_FAILED` | `S->C` | `0x628c20` | `0` | `0x00626DB4` | Static registration observed in WoW.exe. |
| `0x263` | `SMSG_SPELLORDAMAGE_IMMUNE` | `S->C` | `0x5e8ca0` | `0` | `0x005E37F2` | Static registration observed in WoW.exe. |
| `0x264` | `CMSG_AUCTION_LIST_BIDDER_ITEMS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x265` | `SMSG_AUCTION_BIDDER_LIST_RESULT` | `S->C` | `0x4ccc80` | `?` | `0x004CC13F` | Static registration observed in WoW.exe. |
| `0x266` | `SMSG_SET_FLAT_SPELL_MODIFIER` | `S->C` | `0x6e9950` | `?` | `0x006E724F` | Static registration observed in WoW.exe. |
| `0x267` | `SMSG_SET_PCT_SPELL_MODIFIER` | `S->C` | `0x6e9950` | `?` | `0x006E725F` | Static registration observed in WoW.exe. |
| `0x268` | `CMSG_SET_AMMO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x269` | `SMSG_CORPSE_RECLAIM_DELAY` | `S->C` | `0x48f690` | `?` | `0x0048F4AF` | Static registration observed in WoW.exe. |
| `0x26A` | `CMSG_SET_ACTIVE_MOVER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x26B` | `CMSG_PET_CANCEL_AURA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x26C` | `CMSG_PLAYER_AI_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x26D` | `CMSG_CANCEL_AUTO_REPEAT_SPELL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x26E` | `MSG_GM_ACCOUNT_ONLINE` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x26F` | `MSG_LIST_STABLED_PETS` | `Bi` | `0x4cab10` | `0` | `0x004CAA8C` | Static registration observed in WoW.exe. |
| `0x270` | `CMSG_STABLE_PET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x271` | `CMSG_UNSTABLE_PET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x272` | `CMSG_BUY_STABLE_SLOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x273` | `SMSG_STABLE_RESULT` | `S->C` | `0x4cacb0` | `0` | `0x004CAA9D` | Static registration observed in WoW.exe. |
| `0x274` | `CMSG_STABLE_REVIVE_PET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x275` | `CMSG_STABLE_SWAP_PET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x276` | `MSG_QUEST_PUSH_RESULT` | `Bi` | `0x5e38c0` | `0` | `0x005E3803` | Static registration observed in WoW.exe. |
| `0x277` | `SMSG_PLAY_MUSIC` | `S->C` | `0x48f690` | `?` | `0x0048F4DF` | Static registration observed in WoW.exe. |
| `0x278` | `SMSG_PLAY_OBJECT_SOUND` | `S->C` | `0x48f690` | `?` | `0x0048F4FF` | Static registration observed in WoW.exe. |
| `0x279` | `CMSG_REQUEST_PET_INFO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x27A` | `CMSG_FAR_SIGHT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x27B` | `SMSG_SPELLDISPELLOG` | `S->C` | `0x5e8b60` | `0` | `0x005E37BF` | Static registration observed in WoW.exe. |
| `0x27C` | `SMSG_DAMAGE_CALC_LOG` | `S->C` | `0x5e8d30` | `0` | `0x005E3814` | Static registration observed in WoW.exe. |
| `0x27D` | `CMSG_ENABLE_DAMAGE_LOG` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x27E` | `CMSG_GROUP_CHANGE_SUB_GROUP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x27F` | `CMSG_REQUEST_PARTY_MEMBER_STATS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x280` | `CMSG_GROUP_SWAP_SUB_GROUP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x281` | `CMSG_RESET_FACTION_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x282` | `CMSG_AUTOSTORE_BANK_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x283` | `CMSG_AUTOBANK_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x284` | `MSG_QUERY_NEXT_MAIL_TIME` | `Bi` | `0x4ad5f0` | `0` | `0x004ACAF2` | Static registration observed in WoW.exe. |
| `0x285` | `SMSG_RECEIVED_MAIL` | `S->C` | `0x4ad620` | `0` | `0x004ACB03` | Static registration observed in WoW.exe. |
| `0x286` | `SMSG_RAID_GROUP_ONLY` | `S->C` | `0x5e38c0` | `0` | `0x005E3825` | Static registration observed in WoW.exe. |
| `0x287` | `CMSG_SET_DURABILITY_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x288` | `CMSG_SET_PVP_RANK_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x289` | `CMSG_ADD_PVP_MEDAL_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x28A` | `CMSG_DEL_PVP_MEDAL_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x28B` | `CMSG_SET_PVP_TITLE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x28C` | `SMSG_PVP_CREDIT` | `S->C` | `0x48f690` | `?` | `0x0048F48F` | Static registration observed in WoW.exe. |
| `0x28D` | `SMSG_AUCTION_REMOVED_NOTIFICATION` | `S->C` | `0x4cd480` | `?` | `0x004CC16F` | Static registration observed in WoW.exe. |
| `0x28E` | `CMSG_GROUP_RAID_CONVERT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x28F` | `CMSG_GROUP_ASSISTANT_LEADER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x290` | `CMSG_BUYBACK_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x291` | `SMSG_SERVER_MESSAGE` | `S->C` | `0x49df80` | `?` | `0x0049867A` | Static registration observed in WoW.exe. |
| `0x292` | `CMSG_MEETINGSTONE_JOIN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x293` | `CMSG_MEETINGSTONE_LEAVE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x294` | `CMSG_MEETINGSTONE_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x295` | `SMSG_MEETINGSTONE_SETQUEUE` | `S->C` | `0x4ca230` | `0` | `0x004C9F07` | Static registration observed in WoW.exe. |
| `0x296` | `CMSG_MEETINGSTONE_INFO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x297` | `SMSG_MEETINGSTONE_COMPLETE` | `S->C` | `0x4ca3c0` | `0` | `0x004C9F18` | Static registration observed in WoW.exe. |
| `0x298` | `SMSG_MEETINGSTONE_IN_PROGRESS` | `S->C` | `0x4ca3c0` | `0` | `0x004C9F29` | Static registration observed in WoW.exe. |
| `0x299` | `SMSG_MEETINGSTONE_MEMBER_ADDED` | `S->C` | `0x4ca3c0` | `0` | `0x004C9F3A` | Static registration observed in WoW.exe. |
| `0x29A` | `CMSG_GMTICKETSYSTEM_TOGGLE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x29B` | `CMSG_CANCEL_GROWTH_AURA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x29C` | `SMSG_CANCEL_AUTO_REPEAT` | `S->C` | `0x6e99d0` | `?` | `0x006E726F` | Static registration observed in WoW.exe. |
| `0x29D` | `SMSG_STANDSTATE_UPDATE` | `S->C` | `0x603e50` | `?` | `0x006038B0` | Static registration observed in WoW.exe. |
| `0x29E` | `SMSG_LOOT_ALL_PASSED` | `S->C` | `0x5e6010` | `0` | `0x005E3847` | Static registration observed in WoW.exe. |
| `0x29F` | `SMSG_LOOT_ROLL_WON` | `S->C` | `0x5e6010` | `0` | `0x005E3858` | Static registration observed in WoW.exe. |
| `0x2A0` | `CMSG_LOOT_ROLL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2A1` | `SMSG_LOOT_START_ROLL` | `S->C` | `0x5e6010` | `0` | `0x005E3836` | Static registration observed in WoW.exe. |
| `0x2A2` | `SMSG_LOOT_ROLL` | `S->C` | `0x5e6010` | `0` | `0x005E3869` | Static registration observed in WoW.exe. |
| `0x2A3` | `CMSG_LOOT_MASTER_GIVE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2A4` | `SMSG_LOOT_MASTER_LIST` | `S->C` | `0x5e6010` | `0` | `0x005E387A` | Static registration observed in WoW.exe. |
| `0x2A5` | `SMSG_SET_FORCED_REACTIONS` | `S->C` | `0x4d59a0` | `0` | `0x004D52E0` | Static registration observed in WoW.exe. |
| `0x2A6` | `SMSG_SPELL_FAILED_OTHER` | `S->C` | `0x6e8e40` | `?` | `0x006E727F` | Static registration observed in WoW.exe. |
| `0x2A7` | `SMSG_GAMEOBJECT_RESET_STATE` | `S->C` | `0x6e9790` | `?` | `0x006E728F` | Static registration observed in WoW.exe. |
| `0x2A8` | `CMSG_REPAIR_ITEM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2A9` | `SMSG_CHAT_PLAYER_NOT_FOUND` | `S->C` | `0x5e38c0` | `0` | `0x005E36C0` | Static registration observed in WoW.exe. |
| `0x2AA` | `MSG_TALENT_WIPE_CONFIRM` | `Bi` | `0x5e38c0` | `0` | `0x005E3715` | Static registration observed in WoW.exe. |
| `0x2AB` | `SMSG_SUMMON_REQUEST` | `S->C` | `0x5e6140` | `0` | `0x005E388B` | Static registration observed in WoW.exe. |
| `0x2AC` | `CMSG_SUMMON_RESPONSE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2AD` | `MSG_MOVE_TOGGLE_GRAVITY_CHEAT` | `Bi` | `0x603bb0` | `?` | `0x006034D0` | Wrapper -> `0x601580` movement dispatch. |
| `0x2AE` | `SMSG_MONSTER_MOVE_TRANSPORT` | `S->C` | `0x603f00` | `?` | `0x00603600` | Static registration observed in WoW.exe. |
| `0x2AF` | `SMSG_PET_BROKEN` | `S->C` | `0x4bdc00` | `?` | `0x004BC6E6` | Static registration observed in WoW.exe. |
| `0x2B0` | `MSG_MOVE_FEATHER_FALL` | `Bi` | `0x603bb0` | `?` | `0x00603740` | Wrapper -> `0x601580` movement dispatch. |
| `0x2B1` | `MSG_MOVE_WATER_WALK` | `Bi` | `0x603bb0` | `?` | `0x00603750` | Wrapper -> `0x601580` movement dispatch. |
| `0x2B2` | `CMSG_SERVER_BROADCAST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2B3` | `CMSG_SELF_RES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2B4` | `SMSG_FEIGN_DEATH_RESISTED` | `S->C` | `0x6e9800` | `?` | `0x006E729F` | Static registration observed in WoW.exe. |
| `0x2B5` | `CMSG_RUN_SCRIPT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2B6` | `SMSG_SCRIPT_MESSAGE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2B7` | `SMSG_DUEL_COUNTDOWN` | `S->C` | `0x4d4ae0` | `0` | `0x004D474F` | Static registration observed in WoW.exe. |
| `0x2B8` | `SMSG_AREA_TRIGGER_MESSAGE` | `S->C` | `0x48f690` | `?` | `0x0048F50F` | Static registration observed in WoW.exe. |
| `0x2B9` | `CMSG_TOGGLE_HELM` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2BA` | `CMSG_TOGGLE_CLOAK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2BB` | `SMSG_MEETINGSTONE_JOINFAILED` | `S->C` | `0x4ca3c0` | `0` | `0x004C9EF6` | Static registration observed in WoW.exe. |
| `0x2BC` | `SMSG_PLAYER_SKINNED` | `S->C` | `0x5e7d40` | `0` | `0x005E32C4` | Static registration observed in WoW.exe. |
| `0x2BD` | `SMSG_DURABILITY_DAMAGE_DEATH` | `S->C` | `0x628e60` | `0` | `0x00626DC5` | Static registration observed in WoW.exe. |
| `0x2BE` | `CMSG_SET_EXPLORATION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2BF` | `CMSG_SET_ACTIONBAR_TOGGLES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2C0` | `UMSG_DELETE_GUILD_CHARTER` | `Client` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2C1` | `MSG_PETITION_RENAME` | `Bi` | `0x5e5050` | `0` | `0x005E3418` | Static registration observed in WoW.exe. |
| `0x2C2` | `SMSG_INIT_WORLD_STATES` | `S->C` | `0x48f690` | `?` | `0x0048F51F` | Static registration observed in WoW.exe. |
| `0x2C3` | `SMSG_UPDATE_WORLD_STATE` | `S->C` | `0x48f690` | `?` | `0x0048F52F` | Static registration observed in WoW.exe. |
| `0x2C4` | `CMSG_ITEM_NAME_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2C5` | `SMSG_ITEM_NAME_QUERY_RESPONSE` | `S->C` | `0x555120` | `0` | `0x0055501E` | Static registration observed in WoW.exe. |
| `0x2C6` | `SMSG_PET_ACTION_FEEDBACK` | `S->C` | `0x4bdb70` | `?` | `0x004BC6D6` | Static registration observed in WoW.exe. |
| `0x2C7` | `CMSG_CHAR_RENAME` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2C8` | `SMSG_CHAR_RENAME` | `S->C` | `0x46b440` | `?` | `0x0046AA4E` | Static registration observed in WoW.exe. |
| `0x2C9` | `CMSG_MOVE_SPLINE_DONE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2CA` | `CMSG_MOVE_FALL_RESET` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2CB` | `SMSG_INSTANCE_SAVE_CREATED` | `S->C` | `0x4e7e60` | `0` | `0x004E7E52` | Static registration observed in WoW.exe. |
| `0x2CC` | `SMSG_RAID_INSTANCE_INFO` | `S->C` | `0x49e070` | `?` | `0x0049868A` | Static registration observed in WoW.exe. |
| `0x2CD` | `CMSG_REQUEST_RAID_INFO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2CE` | `CMSG_MOVE_TIME_SKIPPED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2CF` | `CMSG_MOVE_FEATHER_FALL_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2D0` | `CMSG_MOVE_WATER_WALK_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2D1` | `CMSG_MOVE_NOT_ACTIVE_MOVER` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2D2` | `SMSG_PLAY_SOUND` | `S->C` | `0x48f690` | `?` | `0x0048F4EF` | Static registration observed in WoW.exe. |
| `0x2D3` | `CMSG_BATTLEFIELD_STATUS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2D4` | `SMSG_BATTLEFIELD_STATUS` | `S->C` | `0x4aa850` | `?` | `0x004A9C5F` | Static registration observed in WoW.exe. |
| `0x2D5` | `CMSG_BATTLEFIELD_PORT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2D6` | `MSG_INSPECT_HONOR_STATS` | `Bi` | `0x4c6e40` | `0` | `0x004C6D1C` | Static registration observed in WoW.exe. |
| `0x2D7` | `CMSG_BATTLEMASTER_HELLO` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2D8` | `CMSG_MOVE_START_SWIM_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2D9` | `CMSG_MOVE_STOP_SWIM_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2DA` | `SMSG_FORCE_WALK_SPEED_CHANGE` | `S->C` | `0x603f90` | `?` | `0x00603640` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x2DB` | `CMSG_FORCE_WALK_SPEED_CHANGE_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2DC` | `SMSG_FORCE_SWIM_BACK_SPEED_CHANGE` | `S->C` | `0x603f90` | `?` | `0x00603650` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x2DD` | `CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2DE` | `SMSG_FORCE_TURN_RATE_CHANGE` | `S->C` | `0x603f90` | `?` | `0x00603660` | Wrapper -> `0x602780` force-speed/root/flag dispatch. |
| `0x2DF` | `CMSG_FORCE_TURN_RATE_CHANGE_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2E0` | `MSG_PVP_LOG_DATA` | `Bi` | `0x4aab30` | `?` | `0x004A9C6F` | Static registration observed in WoW.exe. |
| `0x2E1` | `CMSG_LEAVE_BATTLEFIELD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2E2` | `CMSG_AREA_SPIRIT_HEALER_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2E3` | `CMSG_AREA_SPIRIT_HEALER_QUEUE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2E4` | `SMSG_AREA_SPIRIT_HEALER_TIME` | `S->C` | `0x48f690` | `?` | `0x0048F53F` | Static registration observed in WoW.exe. |
| `0x2E5` | `CMSG_GM_UNTEACH` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2E6` | `SMSG_WARDEN_DATA` | `S->C` | `0x6ca5c0` | `0` | `0x006CA347` | Static registration observed in WoW.exe. |
| `0x2E7` | `CMSG_WARDEN_DATA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2E8` | `SMSG_GROUP_JOINED_BATTLEGROUND` | `S->C` | `0x4aacc0` | `?` | `0x004A9C7F` | Static registration observed in WoW.exe. |
| `0x2E9` | `MSG_BATTLEGROUND_PLAYER_POSITIONS` | `Bi` | `0x4aad40` | `?` | `0x004A9C8F` | Static registration observed in WoW.exe. |
| `0x2EA` | `CMSG_PET_STOP_ATTACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2EB` | `SMSG_BINDER_CONFIRM` | `S->C` | `0x5e38c0` | `0` | `0x005E3737` | Static registration observed in WoW.exe. |
| `0x2EC` | `SMSG_BATTLEGROUND_PLAYER_JOINED` | `S->C` | `0x4aae10` | `?` | `0x004A9C9F` | Static registration observed in WoW.exe. |
| `0x2ED` | `SMSG_BATTLEGROUND_PLAYER_LEFT` | `S->C` | `0x4aae10` | `?` | `0x004A9CAF` | Static registration observed in WoW.exe. |
| `0x2EE` | `CMSG_BATTLEMASTER_JOIN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2EF` | `SMSG_ADDON_INFO` | `S->C` | `0x5b3ea0` | `esi` | `0x005B3AE8` | Static registration observed in WoW.exe. |
| `0x2F0` | `CMSG_PET_UNLEARN` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2F1` | `SMSG_PET_UNLEARN_CONFIRM` | `S->C` | `0x5e38c0` | `0` | `0x005E3726` | Static registration observed in WoW.exe. |
| `0x2F2` | `SMSG_PARTY_MEMBER_STATS_FULL` | `S->C` | `0x5e5110` | `0` | `0x005E34D3` | Static registration observed in WoW.exe. |
| `0x2F3` | `CMSG_PET_SPELL_AUTOCAST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2F4` | `SMSG_WEATHER` | `S->C` | `0x48f690` | `?` | `0x0048F55F` | Static registration observed in WoW.exe. |
| `0x2F5` | `SMSG_PLAY_TIME_WARNING` | `S->C` | `0x5e7830` | `0` | `0x005E389C` | Static registration observed in WoW.exe. |
| `0x2F6` | `SMSG_MINIGAME_SETUP` | `S->C` | `0x4c4c70` | `0` | `0x004C4BEC` | Static registration observed in WoW.exe. |
| `0x2F7` | `SMSG_MINIGAME_STATE` | `S->C` | `0x4c4cf0` | `0` | `0x004C4BFD` | Static registration observed in WoW.exe. |
| `0x2F8` | `CMSG_MINIGAME_MOVE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2F9` | `SMSG_MINIGAME_MOVE_FAILED` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2FA` | `SMSG_RAID_INSTANCE_MESSAGE` | `S->C` | `0x49e1c0` | `?` | `0x0049869A` | Static registration observed in WoW.exe. |
| `0x2FB` | `SMSG_COMPRESSED_MOVES` | `S->C` | `0x603ce0` | `?` | `0x006038C0` | Static registration observed in WoW.exe. |
| `0x2FC` | `CMSG_GUILD_INFO_TEXT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x2FD` | `SMSG_CHAT_RESTRICTED` | `S->C` | `0x5e38c0` | `0` | `0x005E36D1` | Static registration observed in WoW.exe. |
| `0x2FE` | `SMSG_SPLINE_SET_RUN_SPEED` | `S->C` | `0x603c10` | `?` | `0x00603840` | Static registration observed in WoW.exe. |
| `0x2FF` | `SMSG_SPLINE_SET_RUN_BACK_SPEED` | `S->C` | `0x603c10` | `?` | `0x00603850` | Static registration observed in WoW.exe. |
| `0x300` | `SMSG_SPLINE_SET_SWIM_SPEED` | `S->C` | `0x603c10` | `?` | `0x00603860` | Static registration observed in WoW.exe. |
| `0x301` | `SMSG_SPLINE_SET_WALK_SPEED` | `S->C` | `0x603c10` | `?` | `0x00603870` | Static registration observed in WoW.exe. |
| `0x302` | `SMSG_SPLINE_SET_SWIM_BACK_SPEED` | `S->C` | `0x603c10` | `?` | `0x00603880` | Static registration observed in WoW.exe. |
| `0x303` | `SMSG_SPLINE_SET_TURN_RATE` | `S->C` | `0x603c10` | `?` | `0x00603890` | Static registration observed in WoW.exe. |
| `0x304` | `SMSG_SPLINE_MOVE_UNROOT` | `S->C` | `0x603c80` | `?` | `0x00603790` | Static registration observed in WoW.exe. |
| `0x305` | `SMSG_SPLINE_MOVE_FEATHER_FALL` | `S->C` | `0x603c80` | `?` | `0x006037A0` | Static registration observed in WoW.exe. |
| `0x306` | `SMSG_SPLINE_MOVE_NORMAL_FALL` | `S->C` | `0x603c80` | `?` | `0x006037B0` | Static registration observed in WoW.exe. |
| `0x307` | `SMSG_SPLINE_MOVE_SET_HOVER` | `S->C` | `0x603c80` | `?` | `0x006037C0` | Static registration observed in WoW.exe. |
| `0x308` | `SMSG_SPLINE_MOVE_UNSET_HOVER` | `S->C` | `0x603c80` | `?` | `0x006037D0` | Static registration observed in WoW.exe. |
| `0x309` | `SMSG_SPLINE_MOVE_WATER_WALK` | `S->C` | `0x603c80` | `?` | `0x006037E0` | Static registration observed in WoW.exe. |
| `0x30A` | `SMSG_SPLINE_MOVE_LAND_WALK` | `S->C` | `0x603c80` | `?` | `0x006037F0` | Static registration observed in WoW.exe. |
| `0x30B` | `SMSG_SPLINE_MOVE_START_SWIM` | `S->C` | `0x603c80` | `?` | `0x00603800` | Static registration observed in WoW.exe. |
| `0x30C` | `SMSG_SPLINE_MOVE_STOP_SWIM` | `S->C` | `0x603c80` | `?` | `0x00603810` | Static registration observed in WoW.exe. |
| `0x30D` | `SMSG_SPLINE_MOVE_SET_RUN_MODE` | `S->C` | `0x603c80` | `?` | `0x00603820` | Static registration observed in WoW.exe. |
| `0x30E` | `SMSG_SPLINE_MOVE_SET_WALK_MODE` | `S->C` | `0x603c80` | `?` | `0x00603830` | Static registration observed in WoW.exe. |
| `0x30F` | `CMSG_GM_NUKE_ACCOUNT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x310` | `MSG_GM_DESTROY_CORPSE` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x311` | `CMSG_GM_DESTROY_ONLINE_CORPSE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x312` | `CMSG_ACTIVATETAXIEXPRESS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x313` | `SMSG_SET_FACTION_ATWAR` | `S->C` | `0x4d56b0` | `0` | `0x004D52AD` | Static registration observed in WoW.exe. |
| `0x314` | `SMSG_GAMETIMEBIAS_SET` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x315` | `CMSG_DEBUG_ACTIONS_START` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x316` | `CMSG_DEBUG_ACTIONS_STOP` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x317` | `CMSG_SET_FACTION_INACTIVE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x318` | `CMSG_SET_WATCHED_FACTION` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x319` | `MSG_MOVE_TIME_SKIPPED` | `Bi` | `0x603b40` | `?` | `0x006035E0` | Static registration observed in WoW.exe. |
| `0x31A` | `SMSG_SPLINE_MOVE_ROOT` | `S->C` | `0x603c80` | `?` | `0x00603780` | Static registration observed in WoW.exe. |
| `0x31B` | `CMSG_SET_EXPLORATION_ALL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x31C` | `SMSG_INVALIDATE_PLAYER` | `S->C` | `0x555600` | `0` | `0x005550D9` | Static registration observed in WoW.exe. |
| `0x31D` | `CMSG_RESET_INSTANCES` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x31E` | `SMSG_INSTANCE_RESET` | `S->C` | `0x49e470` | `?` | `0x004986AA` | Static registration observed in WoW.exe. |
| `0x31F` | `SMSG_INSTANCE_RESET_FAILED` | `S->C` | `0x49e540` | `?` | `0x004986BA` | Static registration observed in WoW.exe. |
| `0x320` | `SMSG_UPDATE_LAST_INSTANCE` | `S->C` | `0x49e670` | `?` | `0x004986CA` | Static registration observed in WoW.exe. |
| `0x321` | `MSG_RAID_TARGET_UPDATE` | `Bi` | `0x4ba220` | `?` | `0x004BA0C0` | Static registration observed in WoW.exe. |
| `0x322` | `MSG_RAID_READY_CHECK` | `Bi` | `0x4ba360` | `?` | `0x004BA0D0` | Static registration observed in WoW.exe. |
| `0x323` | `CMSG_LUA_USAGE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x324` | `SMSG_PET_ACTION_SOUND` | `S->C` | `0x6040c0` | `?` | `0x00603760` | Static registration observed in WoW.exe. |
| `0x325` | `SMSG_PET_DISMISS_SOUND` | `S->C` | `0x604140` | `?` | `0x00603770` | Static registration observed in WoW.exe. |
| `0x326` | `SMSG_GHOSTEE_GONE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x327` | `CMSG_GM_UPDATE_TICKET_STATUS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x328` | `SMSG_GM_TICKET_STATUS_UPDATE` | `S->C` | `0x5e78f0` | `0` | `0x005E38AD` | Static registration observed in WoW.exe. |
| `0x32A` | `CMSG_GMSURVEY_SUBMIT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x32B` | `SMSG_UPDATE_INSTANCE_OWNERSHIP` | `S->C` | `0x49e6c0` | `?` | `0x004986DA` | Static registration observed in WoW.exe. |
| `0x32C` | `CMSG_IGNORE_KNOCKBACK_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x32D` | `SMSG_CHAT_PLAYER_AMBIGUOUS` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x32E` | `MSG_DELAY_GHOST_TELEPORT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x32F` | `SMSG_SPELLINSTAKILLLOG` | `S->C` | `0x5e85a0` | `0` | `0x005E377B` | Static registration observed in WoW.exe. |
| `0x330` | `SMSG_SPELL_UPDATE_CHAIN_TARGETS` | `S->C` | `0x6e9820` | `?` | `0x006E72AF` | Static registration observed in WoW.exe. |
| `0x331` | `CMSG_CHAT_FILTERED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x332` | `SMSG_EXPECTED_SPAM_RECORDS` | `S->C` | `0x49e6e0` | `?` | `0x004986EA` | Static registration observed in WoW.exe. |
| `0x333` | `SMSG_SPELLSTEALLOG` | `S->C` | `0x5e8c00` | `0` | `0x005E37D0` | Static registration observed in WoW.exe. |
| `0x334` | `CMSG_LOTTERY_QUERY_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x335` | `SMSG_LOTTERY_QUERY_RESULT_OBSOLETE` | `S->C` | `0x4c3d50` | `0` | `0x004C3D2C` | Static registration observed in WoW.exe. |
| `0x336` | `CMSG_BUY_LOTTERY_TICKET_OBSOLETE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x337` | `SMSG_LOTTERY_RESULT_OBSOLETE` | `S->C` | `0x4c3e60` | `0` | `0x004C3D3D` | Static registration observed in WoW.exe. |
| `0x338` | `SMSG_CHARACTER_PROFILE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x339` | `SMSG_CHARACTER_PROFILE_REALM_CONNECTED` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x33A` | `SMSG_DEFENSE_MESSAGE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x33C` | `MSG_GM_RESETINSTANCELIMIT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x33D` | `SMSG_MOTD` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x33E` | `SMSG_MOVE_SET_FLIGHT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x33F` | `SMSG_MOVE_UNSET_FLIGHT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x340` | `CMSG_MOVE_FLIGHT_ACK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x341` | `MSG_MOVE_START_SWIM_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x342` | `MSG_MOVE_STOP_SWIM_CHEAT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x375` | `CMSG_CANCEL_MOUNT_AURA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x379` | `CMSG_CANCEL_TEMP_ENCHANTMENT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x387` | `CMSG_MAELSTROM_INVALIDATE_CACHE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x389` | `CMSG_SET_TAXI_BENCHMARK_MODE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x38D` | `CMSG_MOVE_CHNG_TRANSPORT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x38E` | `MSG_PARTY_ASSIGNMENT` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x38F` | `SMSG_OFFER_PETITION_ERROR` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x396` | `SMSG_RESET_FAILED_NOTIFY` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x397` | `SMSG_REAL_GROUP_UPDATE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3A3` | `SMSG_INIT_EXTRA_AURA_INFO` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3A4` | `SMSG_SET_EXTRA_AURA_INFO` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3A5` | `SMSG_SET_EXTRA_AURA_INFO_NEED_UPDATE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3AA` | `SMSG_SPELL_CHANCE_PROC_LOG` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3AB` | `CMSG_MOVE_SET_RUN_SPEED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3AC` | `SMSG_DISMOUNT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3AE` | `MSG_RAID_READY_CHECK_CONFIRM` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3BE` | `SMSG_CLEAR_TARGET` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3BF` | `CMSG_BOT_DETECTED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3C4` | `SMSG_KICK_REASON` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3C5` | `MSG_RAID_READY_CHECK_FINISHED` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3CF` | `CMSG_TARGET_CAST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3D0` | `CMSG_TARGET_SCRIPT_CAST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3D1` | `CMSG_CHANNEL_DISPLAY_LIST` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3D3` | `CMSG_GET_CHANNEL_MEMBER_COUNT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3D4` | `SMSG_CHANNEL_MEMBER_COUNT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3D7` | `CMSG_DEBUG_LIST_TARGETS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3D8` | `SMSG_DEBUG_LIST_TARGETS` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3DC` | `CMSG_PARTY_SILENCE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3DD` | `CMSG_PARTY_UNSILENCE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3DE` | `MSG_NOTIFY_PARTY_SQUELCH` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3DF` | `SMSG_COMSAT_RECONNECT_TRY` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3E0` | `SMSG_COMSAT_DISCONNECT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3E1` | `SMSG_COMSAT_CONNECT_FAIL` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3EE` | `CMSG_SET_CHANNEL_WATCH` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3EF` | `SMSG_USERLIST_ADD` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3F0` | `SMSG_USERLIST_REMOVE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3F1` | `SMSG_USERLIST_UPDATE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3F2` | `CMSG_CLEAR_CHANNEL_WATCH` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3F4` | `SMSG_GOGOGO_OBSOLETE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3F5` | `SMSG_ECHO_PARTY_SQUELCH` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3F7` | `CMSG_SPELLCLICK` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3F8` | `SMSG_LOOT_LIST` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3FC` | `MSG_GUILD_PERMISSIONS` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3FE` | `MSG_GUILD_EVENT_LOG_QUERY` | `Bi` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x3FF` | `CMSG_MAELSTROM_RENAME_GUILD` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x400` | `CMSG_GET_MIRRORIMAGE_DATA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x401` | `SMSG_MIRRORIMAGE_DATA` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x402` | `SMSG_FORCE_DISPLAY_UPDATE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x403` | `SMSG_SPELL_CHANCE_RESIST_PUSHBACK` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x404` | `CMSG_IGNORE_DIMINISHING_RETURNS_CHEAT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x405` | `SMSG_IGNORE_DIMINISHING_RETURNS_CHEAT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x406` | `CMSG_KEEP_ALIVE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x407` | `SMSG_RAID_READY_CHECK_ERROR` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x408` | `CMSG_OPT_OUT_OF_LOOT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x40B` | `CMSG_SET_GRANTABLE_LEVELS` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x40C` | `CMSG_GRANT_LEVEL` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x40F` | `CMSG_DECLINE_CHANNEL_INVITE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x410` | `CMSG_GROUPACTION_THROTTLED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x411` | `SMSG_OVERRIDE_LIGHT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x412` | `SMSG_TOTEM_CREATED` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x413` | `CMSG_TOTEM_DESTROYED` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x414` | `CMSG_EXPIRE_RAID_INSTANCE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x415` | `CMSG_NO_SPELL_VARIANCE` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x416` | `CMSG_QUESTGIVER_STATUS_MULTIPLE_QUERY` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x417` | `SMSG_QUESTGIVER_STATUS_MULTIPLE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x41A` | `CMSG_QUERY_SERVER_BUCK_DATA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x41B` | `CMSG_CLEAR_SERVER_BUCK_DATA` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x41C` | `SMSG_SERVER_BUCK_DATA` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x41D` | `SMSG_SEND_UNLEARN_SPELLS` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x41E` | `SMSG_PROPOSE_LEVEL_GRANT` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x41F` | `CMSG_ACCEPT_LEVEL_GRANT` | `C->S` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x420` | `SMSG_REFER_A_FRIEND_FAILURE` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
| `0x423` | `SMSG_SUMMON_CANCEL` | `S->C` | `N/A` | `N/A` | `N/A` | No static registration observed in WoW.exe. |
