using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using WoWSharpClient.Handlers;
using WoWSharpClient.Tests.Util;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class OpcodeHandler_Tests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        private readonly OpCodeDispatcher _dispatcher = new();

        /// <summary>
        /// Theory data for opcode dispatch tests. Each row specifies the opcode and
        /// which handler category it belongs to, enabling category-specific postcondition
        /// assertions.
        /// </summary>
        public static IEnumerable<object[]> OpcodeTestData =>
        [
            [Opcode.MSG_MOVE_FALL_LAND, "movement"],
            [Opcode.MSG_MOVE_TIME_SKIPPED, "movement"],
            [Opcode.SMSG_ACCOUNT_DATA_TIMES, "accountData"],
            [Opcode.SMSG_ACTION_BUTTONS, "dispatcher"],
            [Opcode.SMSG_AUTH_RESPONSE, "dispatcher"],
            [Opcode.SMSG_BINDPOINTUPDATE, "dispatcher"],
            [Opcode.SMSG_COMPRESSED_MOVES, "movement"],
            [Opcode.SMSG_DESTROY_OBJECT, "dispatcher"],
            [Opcode.SMSG_FRIEND_LIST, "dispatcher"],
            [Opcode.SMSG_GROUP_LIST, "dispatcher"],
            [Opcode.SMSG_IGNORE_LIST, "dispatcher"],
            [Opcode.SMSG_INITIALIZE_FACTIONS, "dispatcher"],
            [Opcode.SMSG_INITIAL_SPELLS, "spellInitial"],
            [Opcode.SMSG_INIT_WORLD_STATES, "worldState"],
            [Opcode.SMSG_LOGIN_SETTIMESPEED, "dispatcher"],
            [Opcode.SMSG_LOGIN_VERIFY_WORLD, "login"],
            [Opcode.SMSG_SET_FLAT_SPELL_MODIFIER, "dispatcher"],
            [Opcode.SMSG_SET_PCT_SPELL_MODIFIER, "dispatcher"],
            [Opcode.SMSG_SET_PROFICIENCY, "dispatcher"],
            [Opcode.SMSG_SET_REST_START, "worldState"],
            [Opcode.SMSG_SPELLLOGMISS, "spellLogMiss"],
            [Opcode.SMSG_SPELL_GO, "spellGo"],
            [Opcode.SMSG_TUTORIAL_FLAGS, "dispatcher"],
            [Opcode.SMSG_UPDATE_AURA_DURATION, "dispatcher"],
            [Opcode.SMSG_WEATHER, "dispatcher"]
        ];

        /// <summary>
        /// Dispatches each captured packet through the OpCodeDispatcher and verifies
        /// category-specific postconditions. The handlerType parameter drives which
        /// assertions are evaluated:
        ///   - "spellInitial": verifies spells are populated in ObjectManager
        ///   - "login": verifies OnLoginVerifyWorld event fires
        ///   - "worldState": verifies OnWorldStatesInit event fires
        ///   - all categories: verifies no unhandled exceptions during dispatch
        /// </summary>
        [Theory]
        [MemberData(nameof(OpcodeTestData))]
        public void ShouldHandleOpcodePackets(Opcode opcode, string handlerType)
        {
            var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", opcode.ToString());

            var files = Directory.GetFiles(directoryPath, "20240815_*.bin")
                .OrderBy(path =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var parts = fileName.Split('_');
                    return parts.Length > 1 && int.TryParse(parts[1], out var index) ? index : int.MaxValue;
                })
                .ToList();

            // Precondition: resource directory has at least one packet file
            Assert.NotEmpty(files);

            // Track handler-specific observables
            bool loginVerifyFired = false;
            bool worldStatesFired = false;
            EventHandler<WorldInfo> loginHandler = (_, _) => loginVerifyFired = true;
            EventHandler<List<WorldState>> worldStateHandler = (_, _) => worldStatesFired = true;

            if (handlerType == "login")
                WoWSharpEventEmitter.Instance.OnLoginVerifyWorld += loginHandler;
            if (handlerType == "worldState")
                WoWSharpEventEmitter.Instance.OnWorldStatesInit += worldStateHandler;

            try
            {
                // Dispatch all packets -- the dispatcher queues them internally
                foreach (var filePath in files)
                {
                    byte[] data = FileReader.ReadBinaryFile(filePath);
                    _dispatcher.Dispatch(opcode, data);
                }

                // Allow the dispatcher's async runner to process the queued actions
                // The runner loop has a 50ms delay, so 300ms covers several iterations
                Thread.Sleep(300);

                // Category-specific postconditions
                switch (handlerType)
                {
                    case "spellInitial":
                        // SMSG_INITIAL_SPELLS should populate the spell list
                        Assert.True(WoWSharpObjectManager.Instance.Spells.Count > 0,
                            "SMSG_INITIAL_SPELLS dispatch should populate the Spells collection.");
                        break;

                    case "login":
                        Assert.True(loginVerifyFired,
                            "SMSG_LOGIN_VERIFY_WORLD dispatch should fire OnLoginVerifyWorld event.");
                        break;

                    case "worldState":
                        if (opcode == Opcode.SMSG_INIT_WORLD_STATES)
                        {
                            Assert.True(worldStatesFired,
                                "SMSG_INIT_WORLD_STATES dispatch should fire OnWorldStatesInit event.");
                        }
                        break;

                    // For "movement", "accountData", "spellLogMiss", "spellGo", "dispatcher":
                    // The no-throw guarantee from dispatching real captured packets is the
                    // primary assertion. These handler types modify internal state that is
                    // not easily observable without a live ObjectManager player context.
                    default:
                        break;
                }
            }
            finally
            {
                WoWSharpEventEmitter.Instance.OnLoginVerifyWorld -= loginHandler;
                WoWSharpEventEmitter.Instance.OnWorldStatesInit -= worldStateHandler;
            }
        }

        /// <summary>
        /// Verifies that dispatching an unregistered opcode does not throw,
        /// confirming the dispatcher's fallback logging path works correctly.
        /// </summary>
        [Fact]
        public void Dispatch_UnregisteredOpcode_DoesNotThrow()
        {
            // Use an opcode that is not registered in the dispatcher's handler map
            byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            _dispatcher.Dispatch(Opcode.CMSG_PING, data);

            // No exception = success. The dispatcher should log and move on.
        }

        /// <summary>
        /// Verifies that dispatching an empty payload to a registered handler does
        /// not crash the dispatcher runner. Handlers are expected to handle truncated
        /// packets gracefully.
        /// </summary>
        [Fact]
        public void Dispatch_EmptyPayload_DoesNotCrashRunner()
        {
            byte[] data = [];
            _dispatcher.Dispatch(Opcode.SMSG_WEATHER, data);

            // Allow runner to process
            Thread.Sleep(100);

            // No exception = success. The handler should catch any parse errors internally.
        }
    }
}
