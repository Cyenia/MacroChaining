using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace MacroChaining
{
    internal unsafe partial class MacroChaining : IDalamudPlugin
    {
        private static string Name => "Macro Chaining";

        private const string LoopCommand = "/mcloop";
        private const string RunCommand = "/mcrun";
        private const string NextCommand = "/mcnext";
        private const string StopCommand = "/mcstop";

        private static bool _stopLoop;

        private delegate void MacroCallDelegate(RaptureShellModule* rSm, RaptureMacroModule.Macro* m);

        private readonly Hook<MacroCallDelegate>? _mHook;
        private RaptureMacroModule.Macro* _lastMacro = null;
        private readonly Stopwatch _resetLastMacroTimer = new();

        private RaptureMacroModule.Macro* _nextUp = null;
        private RaptureMacroModule.Macro* _nextDown = null;
        private RaptureMacroModule.Macro* _nextLeft = null;
        private RaptureMacroModule.Macro* _nextRight = null;

        [GeneratedRegex(@"\s+")]
        private static partial Regex Regex();
        
        public MacroChaining(IDalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            try
            {
                _mHook = Service.HookProvider.HookFromAddress<MacroCallDelegate>(new IntPtr(RaptureShellModule.MemberFunctionPointers.ExecuteMacro), MacroCallDetour);
                _mHook.Enable();

                Service.CommandManager.AddHandler(LoopCommand, new CommandInfo(OnLoopCommand)
                {
                    HelpMessage = $"Loops the current macro. - {LoopCommand}.",
                    ShowInHelp = true
                });
                Service.CommandManager.AddHandler(NextCommand, new CommandInfo(OnNextCommand)
                {
                    HelpMessage = $"Executes the next macro. - {NextCommand} (right|left|up|down).",
                    ShowInHelp = true
                });
                Service.CommandManager.AddHandler(RunCommand, new CommandInfo(OnRunCommand)
                {
                    HelpMessage = $"Execute a macro. - {RunCommand} ## (individual|shared).",
                    ShowInHelp = true
                });
                Service.CommandManager.AddHandler(StopCommand, new CommandInfo(OnStopCommand)
                {
                    HelpMessage = $"Stops at the next call of {NextCommand} or {RunCommand}",
                    ShowInHelp = true
                });

                Service.Framework.Update += FrameworkUpdate;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex.ToString());
            }
        }

        public void Dispose()
        {
            _mHook?.Dispose();
            Service.CommandManager.RemoveHandler(LoopCommand);
            Service.CommandManager.RemoveHandler(NextCommand);
            Service.CommandManager.RemoveHandler(RunCommand);
            Service.CommandManager.RemoveHandler(StopCommand);
            Service.Framework.Update -= FrameworkUpdate;
        }

        private void MacroCallDetour(RaptureShellModule* rSm, RaptureMacroModule.Macro* m)
        {
            var macroChanged = false;

            _mHook?.Original(rSm, m);
            if (RaptureShellModule.Instance()->MacroLocked) return;
            _lastMacro = m;

            // up macro
            for (uint i = 0; i < 10; i++)
            {
                if (_lastMacro != RaptureMacroModule.Instance()->GetMacro(0, i) &&
                    _lastMacro != RaptureMacroModule.Instance()->GetMacro(1, i)) continue;
                _nextUp = m + 90;
                macroChanged = true;
            }
            
            if (!macroChanged) _nextUp = m - 10;
            macroChanged = false;

            // down macro
            for (uint i = 90; i < 100; i++)
            {
                if (_lastMacro != RaptureMacroModule.Instance()->GetMacro(0, i) &&
                    _lastMacro != RaptureMacroModule.Instance()->GetMacro(0, i)) continue;
                _nextDown = m - 90;
                macroChanged = true;
            }
            
            if (!macroChanged) _nextDown = m + 10;

            // left macro
            if (_lastMacro == RaptureMacroModule.Instance()->GetMacro(0, 0) ||
                _lastMacro == RaptureMacroModule.Instance()->GetMacro(1, 0))
            {
                _nextLeft = m + 99;
            }
            else
            {
                _nextLeft = m - 1;
            }

            // right macro
            if (_lastMacro == RaptureMacroModule.Instance()->GetMacro(1, 99) ||
                _lastMacro == RaptureMacroModule.Instance()->GetMacro(0, 99))
            {
                _nextRight = m - 99;
            }
            else
            {
                _nextRight = m + 1;
            }
        }

        private void OnLoopCommand(string command, string args)
        {
            try
            {
                if (CheckMacroRequirements()) return;

                RaptureShellModule.Instance()->MacroLocked = false;

                RaptureShellModule.Instance()->ExecuteMacro(_lastMacro);
                
                RaptureShellModule.Instance()->MacroLocked = false;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex.ToString());
            }
        }
        
        private void OnNextCommand(string command, string args)
        {
            try
            {
                if (CheckMacroRequirements()) return;

                const string commandHelp = $"\nCommand: {NextCommand} - Help\n\n" +
                                           $"{NextCommand} (right/r|left/l|up/u|down/d)\n                " +
                                           "right - eg. Macro #00 executes Macro #01\n                " +
                                           "left - eg. Macro #00 executes Macro #99\n                " +
                                           "up - eg. #00 executes Macro #90\n                " +
                                           "down - eg. Macro #00 executes Macro #10";

                RaptureShellModule.Instance()->MacroLocked = false;

                switch (args.ToLower())
                {
                    case "up":
                    case "u":
                        RaptureShellModule.Instance()->ExecuteMacro(_nextUp);
                        break;
                    case "down":
                    case "d":
                        RaptureShellModule.Instance()->ExecuteMacro(_nextDown);
                        break;
                    case "left":
                    case "l":
                        RaptureShellModule.Instance()->ExecuteMacro(_nextLeft);
                        break;
                    case "right":
                    case "r":
                        RaptureShellModule.Instance()->ExecuteMacro(_nextRight);
                        break;
                    default:
                        PrintChat(commandHelp);
                        break;
                }

                RaptureShellModule.Instance()->MacroLocked = false;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex.ToString());
            }
        }

        private void OnRunCommand(string command, string args)
        {
            try
            {
                if (CheckMacroRequirements(true)) return;

                const string commandHelp = $"\nCommand: {RunCommand} - Help\n\n" +
                                           $"{RunCommand} ## (shared|individual)\n        " +
                                           "## - Macro number\n        " +
                                           "(shared/share/s|individual/i)\n                " +
                                           "shared - all character macros\n                " +
                                           "individual - current character macros";

                if (args == string.Empty)
                {
                    PrintChat(commandHelp);
                    return;
                }

                args = Regex().Replace(args, " ");
                var split = args.Split(' ');

                if (!uint.TryParse(split[0], out var num) || num > 99)
                {
                    PrintError("Invalid Macro number. (0-99)");
                    return;
                }

                switch (split[1])
                {
                    case "shared":
                    case "share":
                    case "s":
                    {
                        RaptureShellModule.Instance()->ExecuteMacro(RaptureMacroModule.Instance()->GetMacro(1, num));
                        break;
                    }
                    case "individual":
                    case "i":
                    {
                        RaptureShellModule.Instance()->ExecuteMacro(RaptureMacroModule.Instance()->GetMacro(1, num));
                        break;
                    }
                    default:
                    {
                        PrintChat(commandHelp);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex.ToString());
            }
        }

        private void OnStopCommand(string command, string args)
        {
            if (_lastMacro == null)
            {
                PrintError("No macro is running.");
                return;
            }

            _stopLoop = true;
        }

        private void FrameworkUpdate(IFramework framework)
        {
            if (_lastMacro == null) return;

            if (!Service.ClientState.IsLoggedIn)
            {
                _lastMacro = null;
                _resetLastMacroTimer.Stop();
                _resetLastMacroTimer.Reset();
                return;
            }

            if (RaptureShellModule.Instance()->MacroCurrentLine >= 0)
            {
                _resetLastMacroTimer.Restart();
                return;
            }

            if (_resetLastMacroTimer.ElapsedMilliseconds <= 2000) return;
            _lastMacro = null;
            _resetLastMacroTimer.Stop();
            _resetLastMacroTimer.Reset();
        }

        private bool CheckMacroRequirements(bool run = false)
        {
            if (run == false && _lastMacro == null)
            {
                PrintError("No macro is running.");
                return true;
            }

            if (_lastMacro == null || !_stopLoop) return false;
            
            _stopLoop = false;
            PrintError("Stopped loop");

            return true;
        }
        
        private static void PrintChat(string message)
        {
            Service.Chat.Print($"[{Name}]: {message}");
        }
        
        private static void PrintError(string message)
        {
            Service.Chat.PrintError($"[{Name}]: {message}");
        }
    }
}