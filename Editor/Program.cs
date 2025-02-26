using ImGuiNET;
using SharpDX.Direct3D11;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Sentry;
using T3.Core.Animation;
using T3.Core.IO;
using T3.Core.Logging;
using T3.Core.Model;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Core.Resource;
using T3.Editor.App;
using T3.Editor.Compilation;
using T3.Editor.Gui;
using T3.Editor.Gui.AutoBackup;
using T3.Editor.Gui.Commands;
using T3.Editor.Gui.Graph;
using T3.Editor.Gui.Graph.Interaction;
using T3.Editor.Gui.Graph.Modification;
using T3.Editor.Gui.Interaction.Camera;
using T3.Editor.Gui.Interaction.StartupCheck;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;
using T3.Editor.Gui.Windows;
using T3.Editor.SystemUi;
using T3.MsForms;
using T3.SystemUi;
using Vector2 = System.Numerics.Vector2;

namespace T3.Editor
{
    public static class Program
    {
        private static IRenderGui<SharpDX.Direct3D11.Device, ImDrawDataPtr> _t3RenderForm;
        public static Device Device { get; private set; }

        public static readonly bool IsStandAlone = File.Exists("StartT3.exe");
        public const string Version = "3.7.1";

        /// <summary>
        /// Generate a release string with 
        /// </summary>
        public static string GetReleaseVersion(bool indicateDebugBuild= true)
        {
            var isDebug = "";
            #if DEBUG
            if (indicateDebugBuild)
            {
                isDebug = "Debug";
            }
            #endif

            var dev = IsStandAlone ? string.Empty : "Dev";
            return $"v{Version} {dev} {isDebug}";
        }

        [STAThread]
        private static void Main(string[] args)
        {
            InitializeCrashReporting();

            EditorUi.Instance = new MsFormsEditor();

            if (!IsStandAlone)
                StartupValidation.CheckInstallation();

            var startupStopWatch = new Stopwatch();
            startupStopWatch.Start();

            // Enable DPI aware scaling
            EditorUi.Instance.EnableDpiAwareScaling();

            ISplashScreen splashScreen = new SplashScreen.SplashScreen();

            splashScreen.Show("Resources/t3-editor/images/t3-SplashScreen.png");
            Log.AddWriter(splashScreen);

            Log.AddWriter(new ConsoleWriter());
            Log.AddWriter(FileWriter.CreateDefault());
            Log.Debug($"Starting {Version}");

            StartUp.FlagBeginStartupSequence();

            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            new UserSettings(saveOnQuit: true);
            new ProjectSettings(saveOnQuit: true);

            ProgramWindows.InitializeMainWindow(GetReleaseVersion(), out var device);

            Device = device;

            _t3RenderForm = new T3RenderForm();
            _t3RenderForm.Initialize(device, ProgramWindows.Main.Width, ProgramWindows.Main.Height);

            var spaceMouse = new SpaceMouse(ProgramWindows.Main.HwndHandle);
            CameraInteraction.ManipulationDevices = new ICameraManipulator[] { spaceMouse };
            ProgramWindows.SetInteractionDevices(spaceMouse);

            ResourceManager.Init(device);
            ResourceManager resourceManager = ResourceManager.Instance();
            SharedResources.Initialize(resourceManager);

            // Initialize UI and load complete symbol model
            try
            {
                _t3Ui = new T3Ui();
            }
            catch (Exception e)
            {
                Log.Error(e.Message + "\n\n" + e.StackTrace);
                var innerException = e.InnerException != null ? e.InnerException.Message.Replace("\\r", "\r") : string.Empty;
                EditorUi.Instance
                        .ShowMessageBox($"Loading Operators failed:\n\n{e.Message}\n{innerException}\n\nThis is liked caused by a corrupted operator file.\nPlease try restarting and restore backup.",
                                        @"Error", PopUpButtons.Ok);
                EditorUi.Instance.ExitApplication();
                return;
            }

            SymbolAnalysis.UpdateUsagesOnly();

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DpiEnableScaleFonts;
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DpiEnableScaleViewports;

            GenerateFontsWithScaleFactor(UserSettings.Config.UiScaleFactor);

            // Setup file watching the operator source
            resourceManager.OperatorsAssembly = T3Ui.UiSymbolData.OperatorsAssembly;
            foreach (var (_, symbol) in SymbolRegistry.Entries)
            {
                var sourceFilePath = SymbolData.BuildFilepathForSymbol(symbol, SymbolData.SourceExtension);
                ResourceManager.Instance().CreateOperatorEntry(sourceFilePath, symbol.Id.ToString(), OperatorUpdating.ResourceUpdateHandler);
            }

            ShaderResourceView viewWindowBackgroundSrv = null;

            unsafe
            {
                // Disable ImGui ini file settings
                ImGui.GetIO().NativePtr->IniFilename = null;
            }

            Log.RemoveWriter(splashScreen);
            splashScreen.Close();
            splashScreen.Dispose();

            // Initialize optional Viewer Windows
            ProgramWindows.InitializeSecondaryViewerWindow("T3 Viewer", 640, 360);

            StartUp.FlagStartupSequenceComplete();

            startupStopWatch.Stop();
            Log.Debug($"Startup took {startupStopWatch.ElapsedMilliseconds}ms.");

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Int64 lastElapsedTicks = stopwatch.ElapsedTicks;

            T3Style.Apply();

            // Main loop
            void RenderCallback()
            {
                CursorPosOnScreen = new Vector2(Cursor.Position.X, Cursor.Position.Y);
                IsCursorInsideAppWindow = ProgramWindows.Main.IsCursorOverWindow;

                // Update font atlas texture if UI-Scale changed
                if (Math.Abs(UserSettings.Config.UiScaleFactor - _lastUiScale) > 0.005f)
                {
                    GenerateFontsWithScaleFactor(UserSettings.Config.UiScaleFactor);
                    _lastUiScale = UserSettings.Config.UiScaleFactor;
                }

                if (ProgramWindows.Main.IsMinimized)
                {
                    Thread.Sleep(100);
                    return;
                }

                Int64 ticks = stopwatch.ElapsedTicks;
                Int64 ticksDiff = ticks - lastElapsedTicks;
                ImGui.GetIO().DeltaTime = (float)((double)(ticksDiff) / Stopwatch.Frequency);
                lastElapsedTicks = ticks;
                ImGui.GetIO().DisplaySize = ProgramWindows.Main.Size;

                ProgramWindows.HandleFullscreenToggle();

                GraphOperations.UpdateChangedOperators();

                DirtyFlag.IncrementGlobalTicks();
                T3Metrics.UiRenderingStarted();

                if (!string.IsNullOrEmpty(RequestImGuiLayoutUpdate))
                {
                    ImGui.LoadIniSettingsFromMemory(RequestImGuiLayoutUpdate);
                    RequestImGuiLayoutUpdate = null;
                }

                ImGui.NewFrame();
                ProgramWindows.Main.PrepareRenderingFrame();

                // Render 2nd view
                ProgramWindows.Viewer.SetVisible(T3Ui.ShowSecondaryRenderWindow);

                if (T3Ui.ShowSecondaryRenderWindow)
                {
                    ProgramWindows.Viewer.PrepareRenderingFrame();

                    if (ResourceManager.ResourcesById[SharedResources.FullScreenVertexShaderId] is VertexShaderResource vsr)
                        ProgramWindows.SetVertexShader(vsr);

                    if (ResourceManager.ResourcesById[SharedResources.FullScreenPixelShaderId] is PixelShaderResource psr)
                        ProgramWindows.SetPixelShader(psr);

                    if (resourceManager.SecondRenderWindowTexture != null && !resourceManager.SecondRenderWindowTexture.IsDisposed)
                    {
                        //Log.Debug($"using TextureId:{resourceManager.SecondRenderWindowTexture}, debug name:{resourceManager.SecondRenderWindowTexture.DebugName}");
                        if (viewWindowBackgroundSrv == null ||
                            viewWindowBackgroundSrv.Resource.NativePointer != resourceManager.SecondRenderWindowTexture.NativePointer)
                        {
                            viewWindowBackgroundSrv?.Dispose();
                            viewWindowBackgroundSrv = new ShaderResourceView(device, resourceManager.SecondRenderWindowTexture);
                        }

                        ProgramWindows.SetRasterizerState(SharedResources.ViewWindowRasterizerState);
                        ProgramWindows.SetPixelShaderResource(viewWindowBackgroundSrv);
                    }
                    else if (ResourceManager.ResourcesById[SharedResources.ViewWindowDefaultSrvId] is ShaderResourceViewResource srvr)
                    {
                        ProgramWindows.SetPixelShaderResource(srvr.ShaderResourceView);
                        //Log.Debug($"using Default TextureId:{srvr.TextureId}, debug name:{srvr.ShaderResourceView.DebugName}");
                    }
                    else
                    {
                        Log.Debug($"Invalid {nameof(ShaderResourceView)} for 2nd render view");
                    }

                    ProgramWindows.CopyToSecondaryRenderOutput();
                }

                _t3Ui.ProcessFrame();

                ProgramWindows.RefreshViewport();

                ImGui.Render();
                _t3RenderForm.RenderDrawData(ImGui.GetDrawData());

                T3Metrics.UiRenderingCompleted();

                ProgramWindows.Present(T3Ui.UseVSync, T3Ui.ShowSecondaryRenderWindow);
            }

            ProgramWindows.Main.RunRenderLoop(RenderCallback);

            try
            {
                _t3RenderForm.Dispose();
            }
            catch (Exception e)
            {
                Log.Warning("Exception during shutdown: " + e.Message);
            }

            // Release all resources
            try
            {
                ProgramWindows.Release();
            }
            catch (Exception e)
            {
                Log.Warning("Exception freeing resources: " + e.Message);
            }

            Log.Debug("Shutdown complete");
        }

        private static void InitializeCrashReporting()
        {
            SentrySdk.Init(o =>
                           {
                               // Tells which project in Sentry to send events to:
                               o.Dsn = "https://52e37e10dc9cebcc2328cc63fab57211@o4505681078059008.ingest.sentry.io/4505681082384384";
                               o.Debug = false;
                               o.TracesSampleRate = 0.0;
                               o.IsGlobalModeEnabled = true;
                               o.SendClientReports = false;
                               o.AutoSessionTracking = false;
                               o.SendDefaultPii = false;
                               o.Release = GetReleaseVersion(indicateDebugBuild:false);
                               o.SetBeforeSend(CrashHandler);
                           });

            SentrySdk.ConfigureScope(scope => { scope.SetTag("IsStandAlone", IsStandAlone ? "Yes" : "No"); });
            
            var configuration = "Release";
            #if DEBUG
                configuration = "Debug";
            #endif
            
            SentrySdk.ConfigureScope(scope => { scope.SetTag("Configuration", configuration); });

            // Configure WinForms to throw exceptions so Sentry can capture them.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        }

        private static SentryEvent CrashHandler(SentryEvent sentryEvent, Hint hint)
        {
            var timeOfLastBackup = AutoBackup.GetTimeOfLastBackup();
            var timeSpan = THelpers.GetReadableRelativeTime(timeOfLastBackup);

            var result = MessageBox.Show(string.Join("\n",
                                                     "Oh noooo, how embarrassing! T3 just crashed.",
                                                     $"Last backup was saved {timeSpan} to .t3/backups/",
                                                     "We copied the current operator to your clipboard.",
                                                     "Check the FAQ on what to do next.",
                                                     "",
                                                     "Click Yes to send a crash report to tooll.sentry.io.",
                                                     "This will hopefully help us to fix this issue."
                                                    ),
                                         "☠🙈 Damn!",
                                         MessageBoxButtons.YesNo);

            SentrySdk.ConfigureScope(scope => { scope.SetTag("IsStandAlone", IsStandAlone ? "Yes" : "No"); });
            sentryEvent.SetExtra("UndoStack", UndoRedoStack.GetUndoStackAsString());
            sentryEvent.SetExtra("Selection", string.Join("\n", NodeSelection.Selection));
            sentryEvent.SetExtra("Runtime", Playback.RunTimeInSecs);

            try
            {
                var primaryComposition = GraphWindow.GetMainComposition();
                if (primaryComposition != null)
                {
                    var compositionUi = SymbolUiRegistry.Entries[primaryComposition.Symbol.Id];
                    var json = GraphOperations.CopyNodesAsJson(
                                                               primaryComposition.Symbol.Id,
                                                               compositionUi.ChildUis,
                                                               compositionUi.Annotations.Values.ToList());
                    EditorUi.Instance.SetClipboardText(json);
                }
            }
            catch (Exception e)
            {
                sentryEvent.SetExtra("CurrentOpExportFailed", e.Message);
            }

            return result == DialogResult.Yes ? sentryEvent : null;
        }

        private static void GenerateFontsWithScaleFactor(float scaleFactor)
        {
            // See https://stackoverflow.com/a/5977638
            T3Ui.DisplayScaleFactor = ProgramWindows.Main.GetDpi().X / 96f;
            var dpiAwareScale = scaleFactor * T3Ui.DisplayScaleFactor;

            T3Ui.UiScaleFactor = dpiAwareScale;

            var fontAtlasPtr = ImGui.GetIO().Fonts;
            fontAtlasPtr.Clear();
            Fonts.FontNormal = fontAtlasPtr.AddFontFromFileTTF(@"Resources/t3-editor/fonts/Roboto-Regular.ttf", 18f * dpiAwareScale);
            Fonts.FontBold = fontAtlasPtr.AddFontFromFileTTF(@"Resources/t3-editor/fonts/Roboto-Medium.ttf", 18f * dpiAwareScale);
            Fonts.FontSmall = fontAtlasPtr.AddFontFromFileTTF(@"Resources/t3-editor/fonts/Roboto-Regular.ttf", 13f * dpiAwareScale);
            Fonts.FontLarge = fontAtlasPtr.AddFontFromFileTTF(@"Resources/t3-editor/fonts/Roboto-Light.ttf", 30f * dpiAwareScale);

            _t3RenderForm.CreateDeviceObjects();
        }

        private static float _lastUiScale = 1;

        private static T3Ui _t3Ui;
        public static Vector2 CursorPosOnScreen { get; private set; }
        public static bool IsCursorInsideAppWindow { get; private set; }
        public static string RequestImGuiLayoutUpdate;
    }
}