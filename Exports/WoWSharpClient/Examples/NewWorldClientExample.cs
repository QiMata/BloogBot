// Example usage of the updated NewWorldClient

using WoWSharpClient.Client;
using WoWSharpClient.Networking.Implementation;
using GameData.Core.Enums;

class Program
{
    static async Task Main(string[] args)
    {
        // Example session key (in reality this would come from auth server)
        byte[] sessionKeyFromAuthServer = new byte[40]; // Placeholder session key

        // Create the client using the factory
        var worldClient = WoWClientFactory.CreateWorldClient();

        // Subscribe to authentication events
        worldClient.OnAuthenticationSuccessful += () =>
        {
            Console.WriteLine("World server authentication successful! Character list will be requested automatically.");
        };

        worldClient.OnAuthenticationFailed += (errorCode) =>
        {
            Console.WriteLine($"World server authentication failed with error code: {errorCode:X2}");
        };

        // Subscribe to character discovery events
        worldClient.OnCharacterFound += (guid, name, race, characterClass, gender) =>
        {
            Console.WriteLine($"Found character: {name} (GUID: {guid}, Race: {race}, Class: {characterClass})");
        };

        // Subscribe to general connection events
        worldClient.Connected += () =>
        {
            Console.WriteLine("Connected to world server. Waiting for authentication challenge...");
        };

        worldClient.Disconnected += (exception) =>
        {
            if (exception != null)
                Console.WriteLine($"Disconnected from world server due to error: {exception.Message}");
            else
                Console.WriteLine("Disconnected from world server gracefully");
        };

        try
        {
            // Connect to world server (this will trigger the auth flow automatically)
            await worldClient.ConnectAsync("PlayerName", "127.0.0.1", sessionKeyFromAuthServer, 8085);
            
            // Wait for authentication to complete
            while (!worldClient.IsAuthenticated && worldClient.IsConnected)
            {
                await Task.Delay(100);
            }
            
            if (worldClient.IsAuthenticated)
            {
                Console.WriteLine("Ready to interact with world server!");
                
                // Now you can send other packets like:
                // await worldClient.SendPlayerLoginAsync(characterGuid);
                // await worldClient.SendPingAsync(1);
                // await worldClient.SendQueryTimeAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to world server: {ex.Message}");
        }
        finally
        {
            await worldClient.DisconnectAsync();
            worldClient.Dispose();
        }
    }
}