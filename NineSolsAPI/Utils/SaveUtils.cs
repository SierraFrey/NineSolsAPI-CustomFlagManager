using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using JetBrains.Annotations;


namespace NineSolsAPI.Utils;

[PublicAPI]
public static class SaveUtils
{
    private static NineSolsAPICore Instance => NineSolsAPICore.Instance;
    internal static SaveSlotModMetaData[] saveModMetaDataList = new SaveSlotModMetaData[5];
    
    public static Dictionary<string, ModData> modDict = new()
    {
        {NineSolsAPICore.PluginGUID, new ModData(Instance)}
    };
    internal static Dictionary<string, Dictionary<string, GameFlagBase>> allCustomFlags = new()
    {
        {NineSolsAPICore.PluginGUID, new Dictionary<string, GameFlagBase>()}
    };

    private static bool active = false;

    public static ScriptableDataBool CreateScriptableDataBool(BaseUnityPlugin owner, string name, bool defaultValue = false)
    {
        ScriptableDataBool flag = new ScriptableDataBool();
        flag.field = new FlagFieldBool();
        flag.CurrentValue = defaultValue;
        return (ScriptableDataBool)CreateNewFlagBase(owner, name, flag);
    }
    public static GameFlagInt CreateGameFlagInt(BaseUnityPlugin owner, string name, int defaultValue = 0)
    {
        GameFlagInt flag = new GameFlagInt();
        flag.field = new FlagFieldInt();
        flag.CurrentValue = defaultValue;
        return (GameFlagInt)CreateNewFlagBase(owner, name, flag);
    }
    public static ScriptableDataFloat CreateScriptableDataFloat(BaseUnityPlugin owner, string name, float defaultValue = 0f)
    {
        ScriptableDataFloat flag = new ScriptableDataFloat();
        flag.field = new FlagFieldFloat();
        flag.CurrentValue = defaultValue;
        return (ScriptableDataFloat)CreateNewFlagBase(owner, name, flag);
    }
    public static GameFlagString CreateGameFlagString(BaseUnityPlugin owner, string name, string defaultValue = "")
    {
        GameFlagString flag = new GameFlagString();
        flag.field = new FlagFieldString();
        flag.CurrentValue = defaultValue;
        return (GameFlagString)CreateNewFlagBase(owner, name, flag);
    }
    
    public static void SetURL(BaseUnityPlugin owner, string url)
    {
        if (active)
        {
            Log.Error("SetURL must be called before a save is loaded/created.");
            return;
        }
        string GUID = owner.Info.Metadata.GUID;
        if (!modDict.ContainsKey(GUID))
        {
            modDict[GUID] = new ModData(owner);
        }
        modDict[GUID].url = url;
    }

    internal static void Start()
    {
        if (active)
        {
            return;
        }
        SetURL(Instance, "https://github.com/nine-sols-modding/NineSolsAPI");

        Log.Debug("Inserting flags...");

        for (int i = modDict.Count - 1; i >= 0; i--)
        {
            var guid = modDict.ElementAt(i).Key;
            if (!allCustomFlags.ContainsKey(guid))
            {
                modDict.Remove(guid);
                continue;
            }
            foreach (var flag in allCustomFlags[guid].Values)
            {
                SaveManager.Instance.allFlags.FlagDict.Add(flag.FinalSaveID, flag);
                SaveManager.Instance.allFlags.Flags.Add(flag);
            }
        }

        int count = 0;
        foreach (var meta in modDict.Values)
        {
            count += meta.flagCount;
        }

        active = true;
        Log.Debug($"{count} Inserted to flag dictionary.");
    }
    private static GameFlagBase CreateNewFlagBase(BaseUnityPlugin owner, string name, GameFlagBase flag)
    {
        // As far as I know, there isn't a limit to how long a name/id can be.
        string GUID = owner.Info.Metadata.GUID;
        flag.name = name;
        flag.SetSaveID($"{GUID}.{name}.");

        if (!allCustomFlags.ContainsKey(GUID))
        {
            allCustomFlags.Add(GUID, new Dictionary<string, GameFlagBase>());
        }
        else if (allCustomFlags[GUID].ContainsKey(flag.FinalSaveID))
        {
            // Mainly for plugins that are loaded with ScriptEngine. Prevents errors when reloading ingame.
            return allCustomFlags[GUID][flag.FinalSaveID];
        }

        
        if (active)
        {
            Log.Error("New flags must be created before a save is loaded/created.");
            return flag;
        }

        if (!modDict.ContainsKey(GUID))
        {
            modDict[GUID] = new ModData(owner);
        }

        

        try
        {
            allCustomFlags[GUID].Add(flag.FinalSaveID, flag);
        }
        catch (ArgumentException e)
        {
            Log.Error($"Flag creation failed: {e}");
        }
        
        modDict[GUID].flagCount++;
        return flag;
    }

    internal static string InsertModListBeforeBinarySerialization(string? meta)
    {
        if (meta == null || modDict.Count <= 1)
        {
            return meta!;
        }
        string output = meta;
        SaveSlotModMetaData modMetaData = new();
        modMetaData.modList = new(SaveUtils.modDict.Values);
        string modMetaDataString = JsonUtils.Serialize(modMetaData);
        modMetaDataString = modMetaDataString.Remove(0, 1);
        output = output.Remove(output.Length - 1, 1);
        output += ",";
        output += modMetaDataString;
        Log.Debug(output);
        return output;
    }

    internal static void ReadModListBeforeJSONDeserialization(string? meta, string? path)
    {
        if (path == null || meta == null)
        {
            return;
        }
        int slotNumber = (int)path[8] - 48;
        if (slotNumber >= 0 && slotNumber <= 4)
        {
            saveModMetaDataList[slotNumber] = JsonUtils.Deserialize<SaveSlotModMetaData>(meta)!;
        }
    }
}

[Serializable]
public class ModData
{
    public ModData()
    {
        name = "";
        guid = "";
        version = "";
        url = "";
    }
    public ModData(BaseUnityPlugin mod)
    {
        BepInPlugin meta = mod.Info.Metadata;
        name = meta.Name;
        guid = meta.GUID;
        version = meta.Version.ToString();
        url = "";
    }

    public string url;

    public string name;

    public string guid;

    public string version;

    public int flagCount;
}

[Serializable]
public class SaveSlotModMetaData
{
    public SaveSlotModMetaData()
    {
        modList = new List<ModData>();
    }
    public List<ModData> modList;
}
