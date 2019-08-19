using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DG.Tweening;
using UnityModManagerNet;
using Harmony12;
using Kingmaker;
using Kingmaker.UI.MainMenuUI;
using static SkipIntro.Main;

// ReSharper disable InconsistentNaming
namespace SkipIntro
{
    internal static class Main
    {
        internal static readonly HarmonyInstance harmony = HarmonyInstance.Create("ca.gnivler.SkipIntro");

        private static bool OnToggle(UnityModManager.ModEntry mod_entry, bool value)
        {
            enabled = value;
            return true;
        }

        private static bool enabled;

        static void Load(UnityModManager.ModEntry mod_entry)
        {
            Log("Startup");
            mod_entry.OnToggle = OnToggle;
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            ManualPatches.Init();
        }

        internal static void Log(object input)
        {
            //FileLog.Log($"[SkipIntro] {input}");
        }
    }

    internal static class Patches
    {
        // gets rid of Kickstarter message and other logos
        [HarmonyPatch(typeof(SplashScreenController), "Start")]
        public class SplashScreenController_Start_Patch
        {
            public static void Prefix(List<SplashScreenController.ScreenUnit> ___m_Screens)
            {
                ___m_Screens.Clear();
            }
        }

        // makes the menu book appear in full right away, as at the end of its natural animation
        [HarmonyPatch(typeof(MainMenuBoard), "StartIntro")]
        private static class MainMenuBoard_StartIntro_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                codes[24].opcode = OpCodes.Ldc_I4_0;
                codes[45].opcode = OpCodes.Ldc_I4_0;

                return codes.AsEnumerable();
            }
        }

        // makes the book appear fully illuminated right away
        // thanks to https://www.nexusmods.com/pathfinderkingmaker/mods/67
        [HarmonyPatch(typeof(MainMenuAnimationsController), "Update")]
        private static class MainMenuAnimationsControllerUpdatePatch
        {
            private static void Postfix(MainMenuAnimationsController __instance)
            {
                __instance.FadeOutAnimator.Update(float.MaxValue);
            }
        }
    }

    internal static class ManualPatches
    {
        internal static void Init()
        {
            var startGameCoroutine = typeof(GameStarter).GetNestedTypes(AccessTools.all).First(x => x.Name.Contains("StartGameCoroutine"));
            var original = AccessTools.Method(startGameCoroutine, "MoveNext");
            var transpiler = AccessTools.Method(typeof(ManualPatches), nameof(StartGameCoroutine_Transpiler));
            try
            {
                harmony.Patch(original, null, null, new HarmonyMethod(transpiler));
            }
            catch (Exception ex)
            {
                Log(ex);
            }

            original = AccessTools.Method(typeof(MainMenuLogoAnimator), "Show");
            transpiler = AccessTools.Method(typeof(ManualPatches), nameof(Show_Transpiler));
            try
            {
                harmony.Patch(original, null, null, new HarmonyMethod(transpiler));
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        // removes 0.5 second wait on progress bar disappearing
        public static IEnumerable<CodeInstruction> StartGameCoroutine_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>();
            try
            {
                codes = instructions.ToList();
                var index = codes.FindIndex(x => x.opcode == OpCodes.Newobj);
                codes[index - 1].operand = 0f;
            }
            catch (Exception ex)
            {
                Log(ex);
            }

            return codes.AsEnumerable();
        }

        // removes the fade out of the logo after loading is complete
        public static IEnumerable<CodeInstruction> Show_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var index = codes.FindIndex(x => x.operand != null && x.operand.ToString().Contains("m_LogoHideDelay"));
            codes[index - 1].opcode = OpCodes.Nop;
            codes[index].opcode = OpCodes.Ldc_R4;
            codes[index].operand = 0f;

            index = codes.FindIndex(x => x.operand != null && x.operand.ToString().Contains("m_BackgroundHideDelay"));
            codes[index - 1].opcode = OpCodes.Nop;
            codes[index].opcode = OpCodes.Ldc_R4;
            codes[index].operand = 0f;

            return codes.AsEnumerable();
        }
    }
}
