using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NineSolsAPI.Utils;

namespace NineSolsAPI.Patches;

[HarmonyPatch]
public class Transpilers
{
    // To include the modlist in the save metadata, transpiler inserts a CIL instruction 
    // that calls InsertModListBeforeSerialization() to put the modlist into the metadata string json before it gets serialized to binary.
    [HarmonyPatch(typeof(GameFlagManager), nameof(GameFlagManager.FlagsToBinary))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FlagsToBinary_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        int insertionIndex = -1;
        for (int i = (code.Count - 1); i > 0; i--) // Looks for the instruction that puts the save metadata string json on the stack.
        {
            if (
                code[i - 1].opcode == OpCodes.Callvirt &&
                code[i].opcode == OpCodes.Call &&
                code[i + 1].opcode == OpCodes.Stfld
            )
            {
                insertionIndex = i + 1;
                break;
            }
        }

        if (insertionIndex == -1)
        {
            Log.Error("GameFlagManager FlagsToBinary transpiler failed!");
            return instructions;
        }

        code.Insert(insertionIndex, CodeInstruction.Call(() => SaveUtils.InsertModListBeforeBinarySerialization(default)));
        return code;
    }
/*
How I figured out how to get the modlist metadata:

I can't just read the result of LoadMetaJsonFromBinarySave() from the GameFlagManager class because yes, it does get me the modlist metadata,
however savefiles do not contain any data on what save slot it belongs to. I can find the metadata alongside its slot number in
LoadAllSaveSlotMetaForMainMenu() In the SaveManager class.

LoadAllSaveSlotMetaForMainMenu() iterates through every save slot and calls a method (ReadMetaInSave()) that returns a
SaveSlotMetaData object. The modlist data inside the string json metadata is lost when it gets turned into a SaveSlotMetaData
object before it gets returned so I need to transpile ReadMetaInSave() for the string json metadata.

The compiler performs some wacky stuff on async methods. The actual CIL instructions for ReadMetaInSave()
(which is an async method) that I need to transpile is in a private method called MoveNext() within a private struct called
<ReadMetaInSave>d__109. [HarmonyTargetMethod] allows me to transpile the method, but it needs to be in its own class as it
would otherwise affect the other transpilers. Unfortunately, there is one small issue with transpiling it.

For some unholy reason, when transpiled, even with ZERO changes, the method returns a blank SaveSlotMetaData object, which causes
The UI to think that there is no save data. After some experimenting, the problem is that a blank SaveSlotMetaData is
overwriting the proper return variable everytime, yet it seems impossible for the logic to lead up to the overwrite?

The "fix" I came up with was to replace the overwrite command with a pop to remove the blank SaveSlotMetaData from the stack.
Everything seems to work fine afterward, but I'm worried that what I changed is important for some edge case or something else I don't know about.
*/

    public class ReadMetaInSave_TargetMethod
    {
        private static Type accessStruct = AccessTools.Inner(typeof(SaveManager), "<ReadMetaInSave>d__109");
        private static MethodInfo accessMethod = AccessTools.Method(accessStruct, "MoveNext");
        private static FieldInfo accessField = accessStruct.GetField("saveDirectory");

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return accessMethod;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);

            int overwriteFixInsertionIndex = -1;
            for (int i = 1; i < code.Count; i++)
            {
                if (code[i].opcode == OpCodes.Newobj)
                {
                    overwriteFixInsertionIndex = i + 1;
                    break;
                }
            }
            int readMetaInsertionIndex = -1;
            for (int i = 1; i < code.Count; i++)
            {
                if (
                    code[i - 1].opcode == OpCodes.Call &&
                    code[i].opcode == OpCodes.Call
                )
                {
                    readMetaInsertionIndex = i + 1;
                    break;
                }
            }
            
            if (overwriteFixInsertionIndex == -1 || readMetaInsertionIndex == -1)
            {
                Log.Error("<ReadMetaInSave>d__109 MoveNext transpiler failed!");
                foreach (var instruction in instructions)
                {
                    Log.Error($"{instruction.opcode} {instruction.operand}");
                }
                return null!; // As stated above, patching with the instructions unchanged somehow breaks it. Returning a null seems to have Harmony cancel the patch and leave the instructions alone.
            }

            code[overwriteFixInsertionIndex].opcode = OpCodes.Pop; // The "fix."

            var readMetaInstructions = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, accessField),
                new CodeInstruction(CodeInstruction.Call(() => SaveUtils.ReadModListBeforeJSONDeserialization(default, default)))
            };
            code.InsertRange(readMetaInsertionIndex, readMetaInstructions);

            return code;
        }
    }

}

