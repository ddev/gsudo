﻿using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class ConfigCommand : ICommand
    {
        public string key { get; set; }

        public IEnumerable<string> value { get; set; }

        public Task<int> Execute()
        {
            RegistrySetting setting = null;

            if (key == null)
            {
                // print all configs
                foreach (var k in Settings.AllKeys)
                    Console.WriteLine($"{k.Value.Name} = \"{ k.Value.GetStringValue().ToString()}\"".PadRight(50) + (k.Value.HasGlobalValue() ? "(global)" : (k.Value.HasLocalValue() ? "(user)" : string.Empty)));

                return Task.FromResult(0);
            }

            Settings.AllKeys.TryGetValue(key, out setting);

            if (setting == null)
            {
                Console.WriteLine($"Invalid Setting '{key}'.", LogLevel.Error);
                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }

            if (value != null && value.Any())
            {
                if (value.Any(v => v.In("--global")))
                {
                    InputArguments.Global = true;
                    value = value.Where(v => !v.In("--global"));
                }

                string unescapedValue =
                    (value.Count() == 1)
                    ? ArgumentsHelper.UnQuote(value.FirstOrDefault()).Replace("\\%", "%")
                    : string.Join(" ", value.ToArray());

                if (!InputArguments.Global && setting.Scope == RegistrySettingScope.GlobalOnly)
                {
                    Logger.Instance.Log($"Config Setting for '{setting.Name}' will be set as global system setting.", LogLevel.Warning);
                    InputArguments.Global = true;
                }

                bool reset = value.Any(v => v.In("--reset"));
                if (InputArguments.Global && !ProcessExtensions.IsAdministrator())
                {
                    Logger.Instance.Log($"Global system settings requires elevation. Elevating...", LogLevel.Info);
                    return new RunCommand()
                    {
                        CommandToRun = new string[]
                            { Process.GetCurrentProcess().MainModule.FileName, "--piped", "--global", "config", key, reset ? "--reset" : $"\"{unescapedValue}\""}
                    }.Execute();
                }

                if (reset)
                    setting.Reset(InputArguments.Global); // reset to default value
                else 
                    setting.Save(unescapedValue, InputArguments.Global);

                if (setting.Name == Settings.CredentialsCacheDuration.Name)
                    return new KillCacheCommand().Execute();
            }

            // READ
            setting.ClearRunningValue();
            Console.WriteLine($"{setting.Name} = \"{ setting.GetStringValue().ToString()}\" {(setting.HasGlobalValue() ? "(global)" : (setting.HasLocalValue() ? "(user)" : "(default)"))}");
            return Task.FromResult(0);
        }
    }
}
