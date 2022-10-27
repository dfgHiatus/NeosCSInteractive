﻿using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using BaseX;
using FrooxEngine.UIX;
using NeosCSInteractive.Shared;
using System.IO;
using System;
using System.Diagnostics;

namespace NeosCSInteractive
{
    public class NeosCSInteractive : NeosMod
    {
        public override string Name => "NeosCSInteractive";
        public override string Author => "hantabaru1014";
        public override string Version => "0.1.0";
        public override string Link => "https://github.com/hantabaru1014/NeosCSInteractive";

        private static Text outputText;
        private static SmartPadConnector padConnector;
        private static ModConfiguration config;

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> AlwaysRunningServer = new ModConfigurationKey<bool>("AlwaysRunningServer", "Keep the WebSocket server running even when the SmartPad is not open", () => false);

        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new Harmony("net.hantabaru1014.NeosCSInteractive");
            harmony.PatchAll();

            Engine.Current.OnShutdownRequest += Engine_OnShutdownRequest;

            if (config.GetValue(AlwaysRunningServer))
            {
                CreateConnectorIfNotExist();
            }
        }

        private void Engine_OnShutdownRequest(string obj)
        {
            padConnector?.Stop();
        }

        [HarmonyPatch(typeof(UserspaceScreensManager))]
        class ModDashScreenPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("SetupDefaults")]
            public static void SetupDefaults(UserspaceScreensManager __instance)
            {
                // If you don't have an account or sign out this will be generated
                GenerateNCSIScreen(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnLoading")]
            public static void OnLoading(UserspaceScreensManager __instance)
            {
                // If you have an account/sign in OnLoading triggers and replaces the contents generated by SetupDefaults
                GenerateNCSIScreen(__instance);
            }
        }

        private static void GenerateNCSIScreen(UserspaceScreensManager instance)
        {
            if (instance.World != Userspace.UserspaceWorld) return;
            var componentInParents = instance.Slot.GetComponentInParents<RadiantDash>();
            var radiantDashScreen = componentInParents.AttachScreen("NCSI", color.Orange, NeosAssets.Graphics.Icons.Dash.Monitor);
            radiantDashScreen.Slot.PersistentSelf = false;

            var ui = new UIBuilder(radiantDashScreen.ScreenCanvas);
            RadiantUI_Constants.SetupDefaultStyle(ui);
            ui.Image(UserspaceRadiantDash.DEFAULT_BACKGROUND);
            ui.VerticalLayout(5, 15, Alignment.TopCenter).ForceExpandWidth.Value = true;

            var btnsPanel = ui.Panel();
            btnsPanel.Slot.GetComponent<LayoutElement>().PreferredHeight.Value = 60;
            ui.HorizontalLayout(10, 5, Alignment.MiddleRight).ForceExpandHeight.Value = true;
            var runBtn = ui.Button("Run");
            runBtn.Slot.GetComponent<LayoutElement>().PreferredWidth.Value = 150;
            var launchPadBtn = ui.Button("Launch SmartPad");
            launchPadBtn.Slot.GetComponent<LayoutElement>().PreferredWidth.Value = 150;
            ui.NestOut();
            ui.NestOut();

            var sourcePanel = ui.Panel(color.DarkGray);
            sourcePanel.Slot.GetComponent<LayoutElement>().FlexibleHeight.Value = 0.7f;
            var sourceField = ui.TextField("Msg(\"Hello from Roslyn!!!\");");
            sourceField.Text.Align = Alignment.TopLeft;
            //ui.FitContent(SizeFit.PreferredSize);
            ui.NestOut();

            var outputPanel = ui.Panel(color.DarkGray);
            outputPanel.Slot.GetComponent<LayoutElement>().FlexibleHeight.Value = 0.3f;
            ui.ScrollArea(Alignment.TopLeft);
            outputText = ui.Text("", false);
            outputText.Align = Alignment.TopLeft;
            outputText.Size.Value = 32;
            ui.NestOut();
            ui.NestOut();

            runBtn.LocalPressed += (IButton btn, ButtonEventData data) =>
            {
                outputText.Content.Value = string.Empty;
                ScriptUtils.RunScript(sourceField.Text.Content.Value, AddOutput);
            };
            launchPadBtn.LocalPressed += (IButton btn, ButtonEventData data) =>
            {
                LaunchSmartPad();
            };
        }

        private static void AddOutput(LogMessage message)
        {
            outputText.RunSynchronously(() =>
            {
                switch (message.Type)
                {
                    case LogMessage.MessageType.Info:
                        Msg(message.Message);
                        outputText.Content.Value += $"{message.Time.ToLongTimeString()} [INFO] {message.Message}\n";
                        break;
                    case LogMessage.MessageType.Warning:
                        Warn(message.Message);
                        outputText.Content.Value += $"{message.Time.ToLongTimeString()} <color=yellow>[WARN] {message.Message}</color>\n";
                        break;
                    case LogMessage.MessageType.Error:
                        Error(message.Message);
                        outputText.Content.Value += $"{message.Time.ToLongTimeString()} <color=red>[ERROR] {message.Message}</color>\n";
                        break;
                    default:
                        break;
                }
            });
        }

        private static void CreateConnectorIfNotExist()
        {
            if (padConnector?.IsListening ?? false) return;
            padConnector = new SmartPadConnector(0, NetUtils.CreatePassword(25), !config.GetValue(AlwaysRunningServer));
            padConnector.Start();
            if (config.GetValue(AlwaysRunningServer))
            {
                Msg($"SmartPad Connector Listen: {padConnector.Url}, Password: {padConnector.Password}");
            }
        }

        private static void LaunchSmartPad()
        {
            if (!File.Exists(FileUtils.SmartPadExePath))
            {
                Error("SmartPad exe does not exist!");
                return;
            }
            if (FileUtils.IsProcessRunning(FileUtils.SmartPadExePath)) return;
            CreateConnectorIfNotExist();
            Process.Start(FileUtils.SmartPadExePath, $"-address \"{padConnector.Url}\" -password \"{padConnector.Password}\"");
        }
    }
}