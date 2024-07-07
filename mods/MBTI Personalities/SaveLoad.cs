using HarmonyLib;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using System.Text;
using System.Reflection;
using static MBTIPersonalities.MBTIPersonalities;
using SimpleJSON;
using System.Linq;
using System.Reflection.Emit;
using static policies;

namespace MBTIPersonalities
{
    //// Save MBTI data
    //[HarmonyPatch]
    //public class DataSaver_saveData
    //{
    //    public static MethodBase TargetMethod()
    //    {
    //        // refer to C# reflection documentation:
    //        return typeof(DataSaver).GetMethod("saveData").MakeGenericMethod(typeof(SaveManager.SavedData));
    //    }
    //    public static void Postfix(string dataFileName, bool fullPath = false)
    //    {
    //        if(dataFileName == "global_data" || dataFileName.StartsWith("export_"))
    //        {
    //            return;
    //        }

    //        List<SavedMBTI.MBTISaveRecord> list = new();
    //        foreach (data_girls.girls girls in data_girls.girl)
    //        {
    //            SavedMBTI.MBTISaveRecord girlData = new()
    //            {
    //                id = girls.id,
    //                firstName = girls.firstName,
    //                lastName = girls.lastName,
    //                mbti = MBTIPersonalities.GetGirlMBTI(girls).ToString()
    //            };
    //            list.Add(girlData);
    //        }

    //        SavedMBTI Data = new()
    //        {
    //            Saved__GirlMBTI = list
    //        };

    //        SaveMBTIData(Data, dataFileName, fullPath);
    //    }

    //    private static void SaveMBTIData(SavedMBTI dataToSave, string dataFileName, bool fullPath)
    //    {
    //        string dataPath = Application.persistentDataPath;
    //        string fullFileName;
    //        if (fullPath)
    //        {
    //            fullFileName = dataFileName;
    //        }
    //        else
    //        {
    //            fullFileName = Path.Combine(dataPath, "data");
    //            fullFileName = Path.Combine(fullFileName, dataFileName + ".json");
    //        }
    //        fullFileName = fullFileName.Replace(".json", "_mbti.json");
    //        byte[] bytes = Encoding.UTF8.GetBytes(dataToSave.ToString());
    //        if (!Directory.Exists(Path.GetDirectoryName(fullFileName)))
    //        {
    //            Directory.CreateDirectory(Path.GetDirectoryName(fullFileName));
    //        }
    //        try
    //        {
    //            File.WriteAllBytes(fullFileName, bytes);
    //        }
    //        catch (Exception ex)
    //        {
    //            Debug.LogWarning("Failed To Write MBTI Data to: " + fullFileName.Replace("/", "\\"));
    //            Debug.LogWarning("Error: " + ex.Message);
    //        }
    //    }
    //}

    //// Load MBTI data from autosave or quickload
    //[HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] {typeof(bool)})]
    //public class SaveManager_LoadData_bool
    //{
    //    public static void Postfix(SaveManager __instance, bool autoSave)
    //    {
    //        string dataFileName;
    //        if (autoSave)
    //        {
    //            dataFileName = SaveManager.GetLatestAutosavePath();
    //        }
    //        else
    //        {
    //            var GetSaveFileName = __instance.GetType().GetMethod("GetSaveFileName", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
    //            dataFileName = (string)GetSaveFileName.Invoke(__instance,new object[] {autoSave});
    //        }
    //        LoadedMBTI.LoadMBTIData(dataFileName);
    //    }
    //}

    //// Load MBTI data from save game manager
    //[HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(string) })]
    //public class SaveManager_LoadData_string
    //{
    //    public static void Postfix(string path)
    //    {
    //        LoadedMBTI.LoadMBTIData(path);
    //    }
    //}

    // Load MBTI data
    [HarmonyPatch(typeof(data_girls), "LoadFunction")]
    public class data_girls_LoadFunction
    {
        public static void Postfix()
        {
            ResetMBTI();

            foreach (data_girls.girls girl in data_girls.girl)
            {
                foreach (string variable in girl.Variables)
                {
                    if(Enum.TryParse(variable, true, out MBTI mBTI))
                    {
                        SetGirlMBTI(girl, mBTI);
                        break;
                    }
                }
            }
        }
    }


    // Load MBTI from unique idol textures
    [HarmonyPatch(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) })]
    public class data_girls_textures_LoadAssetsData
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            object textureAssetOperand = null;
            object jsonNodeOperand = null;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable && localVariable.LocalIndex == 8)
                {
                    textureAssetOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable2 && localVariable2.LocalIndex == 13)
                {
                    index = i;
                    jsonNodeOperand = instructionList[i].operand;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_S, jsonNodeOperand));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_S, textureAssetOperand));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), "Infix")));
            }

            return instructionList.AsEnumerable();
        }

        public static void Infix(JSONNode jsonnode, data_girls_textures._textureAsset textureAsset)
        {
            if (jsonnode[MBTINodeID] == null)
                return;

            string mbtiString = jsonnode[MBTINodeID];

            MBTI girlMBTI = Enum.TryParse(mbtiString, true, out MBTI tryMBTI) ? tryMBTI : MBTI.None;

            MBTITextureData mBTITextureData = new()
            {
                ModName = textureAsset.ModName,
                body_id = textureAsset.body_id,
                mbti = girlMBTI
            };

            MBTITextureReferenceList.Add(mBTITextureData);
        }
    }

    //public class SavedMBTI
    //{
    //    public int id = 0;
    //    public List<MBTISaveRecord> Saved__GirlMBTI;

    //    public override string ToString()
    //    {
    //        string output = "[\n";
    //        int count = 0;
    //        foreach(MBTISaveRecord record in Saved__GirlMBTI)
    //        {
    //            output += JsonUtility.ToJson(record, true);
    //            count++;
    //            if(count < Saved__GirlMBTI.Count)
    //            {
    //                output += ",\n";
    //            }
    //        }
    //        output += "\n]";
    //        return output;
    //    }

    //    [Serializable]
    //    public class MBTISaveRecord
    //    {
    //        public int id;
    //        public string firstName;
    //        public string lastName;
    //        public string mbti;
    //    }
    //}

    //public class LoadedMBTI
    //{
    //    public static void LoadMBTIData(string path)
    //    {
    //        path = path.Replace(".json", "_mbti.json");
    //        if (!File.Exists(path))
    //        {
    //            return;
    //        }

    //        int id;
    //        string mbtiString;

    //        foreach (object obj in mainScript.ProcessInboundData(File.ReadAllText(path)).AsArray)
    //        {
    //            JSONNode jsonnode = (JSONNode)obj;

    //            if (!Int32.TryParse(jsonnode["id"], out int idVal))
    //            {
    //                Debug.LogWarning($"Int32.TryParse could not parse '{jsonnode["id"]}' to an int.");
    //                continue;
    //            }
    //            id = idVal;
    //            mbtiString = jsonnode["mbti"];
    //            data_girls.girls girl = data_girls.GetGirlByID(id);
    //            if (!Enum.TryParse(mbtiString, true, out MBTI tryMBTI))
    //            {
    //                Debug.LogWarning($"Enum.TryParse could not parse '{mbtiString}' to an MBTI value.");
    //                tryMBTI = GenerateMBTI(girl);
    //            }
    //            SetGirlMBTI(girl, tryMBTI);

    //        }
    //    }
    //}
}
