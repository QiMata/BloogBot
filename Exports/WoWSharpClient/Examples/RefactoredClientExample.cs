using System;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using WoWSharpClient.Client;

namespace WoWSharpClient.Examples
{
    /// <summary>
    /// Example demonstrating how to use the refactored client architecture.
    /// </summary>
    public sealed class RefactoredClientExample : IDisposable
    {
        private WoWClientOrchestrator? _orchestrator;
        private bool _disposed;

        /// <summary>
        /// Demonstrates the complete login flow using the orchestrator.
        /// </summary>
        public async Task RunCompleteExampleAsync()
        {
            try
            {
                // Create the orchestrator - this manages both auth and world clients
                _orchestrator = WoWClientFactory.CreateOrchestrator();

                // Subscribe to events
                _orchestrator.WorldConnected += OnWorldConnected;
                _orchestrator.WorldDisconnected += OnWorldDisconnected;

                // Step 1: Login to authentication server
                Console.WriteLine("[Example] Connecting to authentication server...");
                await _orchestrator.LoginAsync("127.0.0.1", "testuser", "testpass");
                
                Console.WriteLine("[Example] Authentication successful!");

                // Step 2: Get realm list
                Console.WriteLine("[Example] Fetching realm list...");
                var realms = await _orchestrator.GetRealmListAsync();
                
                if (realms.Count > 0)
                {
                    var realm = realms[0]; // Use first realm
                    Console.WriteLine($"[Example] Connecting to realm: {realm.RealmName}");

                    // Step 3: Connect to world server
                    await _orchestrator.ConnectToRealmAsync(realm);

                    // Step 4: Get character list
                    Console.WriteLine("[Example] Requesting character list...");
                    await _orchestrator.RefreshCharacterListAsync();

                    // Step 5: Send some world packets
                    await _orchestrator.SendPingAsync();
                    await _orchestrator.QueryTimeAsync();

                    // Step 6: Example character login (using dummy GUID)
                    // await _orchestrator.EnterWorldAsync(123456789);

                    // Keep the example running for a bit
                    await Task.Delay(5000);

                    Console.WriteLine("[Example] Disconnecting...");
                }
                else
                {
                    Console.WriteLine("[Example] No realms available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Example] Error: {ex.Message}");
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        /// <summary>
        /// Demonstrates using individual clients separately.
        /// </summary>
        public async Task RunSeparateClientsExampleAsync()
        {
            AuthClient? authClient = null;
            WorldClient? worldClient = null;

            try
            {
                // Step 1: Create and use auth client
                Console.WriteLine("[Example] Creating auth client...");
                authClient = WoWClientFactory.CreateAuthClient();

                await authClient.ConnectAsync("127.0.0.1");
                await authClient.LoginAsync("testuser", "testpass");

                var realms = await authClient.GetRealmListAsync();
                Console.WriteLine($"[Example] Found {realms.Count} realms");

                // Step 2: Create and use world client
                if (realms.Count > 0)
                {
                    Console.WriteLine("[Example] Creating world client...");
                    worldClient = WoWClientFactory.CreateWorldClient();

                    var sessionKey = authClient.SessionKey;
                    var username = authClient.Username;
                    var realm = realms[0];

                    await worldClient.ConnectAsync(username, "127.0.0.1", sessionKey, realm.AddressPort);

                    // Send some packets
                    await worldClient.SendCharEnumAsync();
                    await worldClient.SendPingAsync(1);
                    await worldClient.SendQueryTimeAsync();

                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Example] Error: {ex.Message}");
            }
            finally
            {
                if (worldClient != null)
                {
                    await worldClient.DisconnectAsync();
                    worldClient.Dispose();
                }

                if (authClient != null)
                {
                    await authClient.DisconnectAsync();
                    authClient.Dispose();
                }
            }
        }

        /// <summary>
        /// Demonstrates creating clients with custom configurations.
        /// </summary>
        public async Task RunCustomConfigurationExampleAsync()
        {
            WorldClient? worldClient = null;

            try
            {
                Console.WriteLine("[Example] Creating world client with reconnection...");
                
                // Create world client with automatic reconnection
                worldClient = WoWClientFactory.CreateWorldClientWithReconnection("127.0.0.1", 8085);

                // Simulate connection
                await worldClient.ConnectAsync("testuser", "127.0.0.1", new byte[40]); // dummy session key

                // Send some packets
                await worldClient.SendPingAsync(1);
                await worldClient.SendQueryTimeAsync();

                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Example] Error: {ex.Message}");
            }
            finally
            {
                if (worldClient != null)
                {
                    await worldClient.DisconnectAsync();
                    worldClient.Dispose();
                }
            }
        }

        /// <summary>
        /// Demonstrates chat functionality.
        /// </summary>
        public async Task RunChatExampleAsync()
        {
            try
            {
                _orchestrator = WoWClientFactory.CreateOrchestrator();

                // Login process (simplified)
                await _orchestrator.LoginAsync("127.0.0.1", "testuser", "testpass");
                var realms = await _orchestrator.GetRealmListAsync();
                
                if (realms.Count > 0)
                {
                    await _orchestrator.ConnectToRealmAsync(realms[0]);
                    
                    // Send various chat messages
                    await _orchestrator.SendChatMessageAsync(ChatMsg.CHAT_MSG_SAY, Language.Common, "", "Hello world!");
                    await _orchestrator.SendChatMessageAsync(ChatMsg.CHAT_MSG_YELL, Language.Common, "", "Testing yell!");
                    await _orchestrator.SendChatMessageAsync(ChatMsg.CHAT_MSG_WHISPER, Language.Common, "targetplayer", "Private message");

                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Example] Chat error: {ex.Message}");
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        private void OnWorldConnected()
        {
            Console.WriteLine("[Example] Connected to world server!");
        }

        private void OnWorldDisconnected(Exception? exception)
        {
            if (exception != null)
                Console.WriteLine($"[Example] Disconnected from world server due to error: {exception.Message}");
            else
                Console.WriteLine("[Example] Disconnected from world server gracefully");
        }

        private async Task DisconnectAsync()
        {
            if (_orchestrator != null)
            {
                await _orchestrator.DisconnectWorldAsync();
                await _orchestrator.DisconnectAuthAsync();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _orchestrator?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Program entry point for running the examples.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var example = new RefactoredClientExample();

            Console.WriteLine("=== Refactored WoW Client Examples ===\n");

            // Run different examples
            Console.WriteLine("1. Complete Orchestrator Example:");
            await example.RunCompleteExampleAsync();
            
            Console.WriteLine("\n2. Separate Clients Example:");
            await example.RunSeparateClientsExampleAsync();
            
            Console.WriteLine("\n3. Custom Configuration Example:");
            await example.RunCustomConfigurationExampleAsync();
            
            Console.WriteLine("\n4. Chat Example:");
            await example.RunChatExampleAsync();

            Console.WriteLine("\n=== Examples Complete ===");
        }
    }
}