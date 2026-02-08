using Newtonsoft.Json;
using System.Reflection;
using Serilog;

namespace WoWStateManager.Settings
{
    public class StateManagerSettings
    {
        private static StateManagerSettings _instance;
        public static StateManagerSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new StateManagerSettings();
                    _instance.LoadConfig();
                }
                return _instance;
            }
        }

        private void LoadConfig()
        {
            string currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string settingsFilePath = Path.Combine(currentFolder, "Settings\\StateManagerSettings.json");

            CharacterSettings = JsonConvert.DeserializeObject<List<CharacterSettings>>(File.ReadAllText(settingsFilePath)) ?? [];
        }

        public static void SaveConfig()
        {
            try
            {
                string currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string settingsFilePath = Path.Combine(currentFolder, "Settings\\StateManagerSettings.json");
                string json = JsonConvert.SerializeObject(_instance.CharacterSettings, Formatting.Indented);

                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving config");
            }
        }

        private StateManagerSettings() { }

        /// <summary>
        /// List of character settings defining which bots to run and how.
        /// </summary>
        public List<CharacterSettings> CharacterSettings { get; private set; } = [];
    }
}
