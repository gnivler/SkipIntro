using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using EasyHook;
using UnityModManagerNet;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI._ConsoleUI.Endless.GameOver;
using Kingmaker.UI.MainMenuUI;
using static SkipIntro.Main;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local 
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedType.Local 
// ReSharper disable InconsistentNaming

namespace SkipIntro
{
    internal static class Main
    {
        internal static readonly Harmony harmony = new Harmony("ca.gnivler.SkipIntro");


        private static bool OnToggle(UnityModManager.ModEntry mod_entry, bool value)
        {
            enabled = value;
            return true;
        }

        // ReSharper disable once NotAccessedField.Local
        private static bool enabled;

        static void Load(UnityModManager.ModEntry mod_entry)
        {
            Log("Startup");
            mod_entry.OnToggle = OnToggle;
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            ManualPatches.Init();
            try
            {
                //var hook = LocalHook.Create(
                //    LocalHook.GetProcAddress("UnityPlayer.dll", "BeginSplashScreen"),
                //    new EasyHook.BeginSplashDelegate(EasyHook.BeginSplashScreenHook),
                //    null);
                //
                //Log("HOOK" + hook);
                //hook.ThreadACL.SetInclusiveACL(new[] {0});
            }
            catch (Exception ex)
            {
                Log(ex);
            }
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
                => ___m_Screens.Clear();
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
                => __instance.FadeOutAnimator.Update(float.MaxValue);
        }
    }

    internal static class ManualPatches
    {
        internal static void Init()
        {
            var startGameCoroutine = typeof(GameStarter).GetNestedTypes(AccessTools.all).First(x => x.Name.Contains("StartGameCoroutine"));
            var original = AccessTools.Method(startGameCoroutine, "MoveNext");
            var transpiler = AccessTools.Method(typeof(ManualPatches), nameof(StartGameCoroutine_Transpiler));
            var postfix = AccessTools.Method(typeof(ManualPatches), nameof(StartGameCoroutine_Postfix));
            try
            {
                harmony.Patch(original, null, new HarmonyMethod(postfix), new HarmonyMethod(transpiler));
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
        private static IEnumerable<CodeInstruction> StartGameCoroutine_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>();
            try
            {
                codes = instructions.ToList();
                var index = codes.FindIndex(x =>
                    x.opcode == OpCodes.Ldfld &&
                    x.operand.ToString().Contains(@"<>8__1"));
                index++;
                codes[index].opcode = OpCodes.Ldc_I4_1;
                Log("========================== First");
                codes.GetRange(index - 5, 10).Do(x => Log($"{x.opcode,-20}{x.operand}")); //--> {(x.labels.Count > 0 ? x.labels[0].ToString() : ""),20}"));
                codes[index].operand = 0f;
                Log("========================== Last");
                codes.GetRange(index - 5, 10).Do(x => Log($"{x.opcode,-20}{x.operand}")); // --> {(x.labels.Count > 0 ? x.labels[0].ToString() : ""),20}"));
            }
            catch (Exception ex)
            {
                Log(ex);
            }

            return codes.AsEnumerable();
        }

        private static void StartGameCoroutine_Postfix()
        {
            try
            {
                var controls = new FileInfo(Path.Combine(Assembly.GetExecutingAssembly().Location + "controls.txt"));
                if (File.Exists(controls.FullName))
                {
                    var input = File.ReadLines(controls.FullName).First().ToLowerInvariant();
                    Game.Instance.ControllerMode = input == "mouse" ? Game.ControllerModeType.Mouse : Game.ControllerModeType.Gamepad;
                }
                else
                {
                    Game.Instance.ControllerMode = Game.ControllerModeType.Mouse;
                }
            }
            catch (Exception ex)
            {
                Game.Instance.ControllerMode = Game.ControllerModeType.Mouse;
                Log(ex);
            }
        }

        // removes the fade out of the logo after loading is complete
        private static IEnumerable<CodeInstruction> Show_Transpiler(IEnumerable<CodeInstruction> instructions)
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
