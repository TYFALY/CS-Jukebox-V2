using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CS_Jukebox
{
    static class Properties
    {
        //Paths
        public static readonly string ConfigPath = @"\csgo\cfg\gamestate_integration_jukebox.cfg";
        public static readonly string ConfigName = @"\gamestate_integration_jukebox.cfg";
        public static readonly string PropertiesFilePath = @"\properties.json";
        public static readonly string MusicKitsPath = @"\kits";

        public static string GameDir = null;
        public static int MasterVolume;
        public static MusicKit SelectedKit
        {
            get { return selectedKit; }
            set { SetKit(value); }
        }

        public static List<MusicKit> MusicKits = null;

        private static string startDir;

        private static MusicKit selectedKit = null;
        private static string SelectedKitName = null;

        //Calls all load methods
        public static void Load()
        {
            LoadProperties();
            LoadKits();
        }

        //Calls all save methods
        public static void Save()
        {
            SaveProperties();
            SaveKits();
        }

        //Converts settings to json file then saves it
        public static void SaveProperties()
        {
            string dir = startDir + PropertiesFilePath;

            PropertiesFile propFile = new PropertiesFile();
            propFile.GameDir = GameDir;
            propFile.SelectedKitName = SelectedKitName;
            propFile.MasterVolume = MasterVolume;
            Console.WriteLine(propFile.GameDir);

            string jsonFile = JsonConvert.SerializeObject(propFile);

            Console.WriteLine("Saving json properties: ");
            Console.WriteLine(jsonFile);
            File.WriteAllText(dir, jsonFile);
        }

        //Reads properties file then deserializes it
        public static void LoadProperties()
        {
            //startDir = Directory.GetCurrentDirectory();
            startDir = GetAppDirectory();
            Console.WriteLine("App Directory: " + startDir);
            string dir = startDir + PropertiesFilePath;
            PropertiesFile propFile;

            try
            {
                string jsonFile = File.ReadAllText(dir);
                propFile = JsonConvert.DeserializeObject<PropertiesFile>(jsonFile);
                GameDir = propFile.GameDir;
                SelectedKitName = propFile.SelectedKitName;
                MasterVolume = propFile.MasterVolume;
            }
            catch (FileNotFoundException e)
            {
                propFile = new PropertiesFile();
            }
        }

        private static string GetAppDirectory()
        {
            string[] execPath = Application.ExecutablePath.Split('\\');
            string appDir = "";

            for (int i = 0; i < execPath.Length - 1; i++)
            {
                appDir += execPath[i];
                if (i < execPath.Length - 2) appDir += "\\";
            }

            return appDir;
        }

        // Copies the config to the exact CS2 GSI directory.
        public static void CreateConfig()
        {
            if (string.IsNullOrWhiteSpace(Properties.GameDir)) return;

            string configFileName = Properties.ConfigName.TrimStart('\\', '/');
            string cfgDir = GameInstallLocator.GetConfigDirectory(Properties.GameDir);
            string configPath = Path.Combine(cfgDir, configFileName);
            string configSrc = Path.Combine(startDir, configFileName);

            try
            {
                if (!Directory.Exists(cfgDir))
                {
                    Console.WriteLine("CS2 configuration directory was not found: " + cfgDir);
                    return;
                }

                File.Copy(configSrc, configPath, true);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to copy config file: " + e.Message);
            }
        }

        public static void SaveKits()
        {
            string dir = startDir + MusicKitsPath;

            Directory.CreateDirectory(dir);

            foreach (MusicKit musicKit in MusicKits)
            {
                Console.WriteLine("Saving song: " + musicKit.Name);
                string kitDir = dir + @"\" + musicKit.Name + ".json";
                Console.WriteLine(kitDir);
                string jsonFile = JsonConvert.SerializeObject(musicKit);
                Console.WriteLine(jsonFile);
                File.WriteAllText(kitDir, jsonFile);
            }
        }

        public static void LoadKits()
        {
            string dir = startDir + MusicKitsPath;
            MusicKits = new List<MusicKit>();

            if (Directory.Exists(dir))
            {
                foreach (string filePath in Directory.GetFiles(dir))
                {
                    if (!filePath.EndsWith(".json")) continue;
                    string jsonFile = "";

                    try
                    {
                        jsonFile = File.ReadAllText(filePath);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception when trying to load music kits.");
                        Console.WriteLine(e.StackTrace);
                    }
                    finally
                    {
                        MusicKit musicKit = JsonConvert.DeserializeObject<MusicKit>(jsonFile);
                        MusicKits.Add(musicKit);
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(dir);
            }

            //Find a value for SelectedKit if applicable
            if (MusicKits.Count > 0)
            {
                foreach (MusicKit musicKit in MusicKits)
                {
                    if (musicKit.Name.Equals(SelectedKitName))
                    {
                        SelectedKit = musicKit;
                    }
                }

                if (SelectedKit == null)
                {
                    SelectedKit = MusicKits[0];
                }
            }
        }

        //Deletes the json file for a kit but not the kit itself
        public static void DeleteKitFile(string kitName)
        {
            string dir = startDir + MusicKitsPath;
            string kitDir = dir + @"\" + kitName + ".json";
            File.Delete(kitDir);
        }

        private static void SetKit(MusicKit newKit)
        {
            selectedKit = newKit;
            SelectedKitName = selectedKit.Name;
        }

        //Inner class for properties parameters
        private class PropertiesFile
        {
            public string GameDir;
            public string SelectedKitName;
            public int MasterVolume;
        }
    }
}
