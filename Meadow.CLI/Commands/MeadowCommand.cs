﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands
{
    public abstract class MeadowCommand : ICommand
    {
        private protected ILoggerFactory LoggerFactory;
        private protected DownloadManager DownloadManager;

        private protected MeadowCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
        {
            DownloadManager = downloadManager;
            LoggerFactory = loggerFactory;
        }

        [CommandOption("LogVerbosity", 'g', Description = "Log verbosity")]
        public string[] Verbosity { get; init; }

        public virtual async ValueTask ExecuteAsync(IConsole console)
        {
            var logger = LoggerFactory.CreateLogger(typeof(MeadowCommand));
            var lastUpdateCheckString = SettingsManager.GetSetting(Setting.LastUpdateCheck);
            var lastUpdateCheck = string.IsNullOrWhiteSpace(lastUpdateCheckString)
                                      ? DateTimeOffset.MinValue
                                      : DateTimeOffset.FromUnixTimeSeconds(long.Parse(lastUpdateCheckString));

            bool updateExists;
            Version currentVersion, latestVersion;
            if (lastUpdateCheck.AddDays(1) < DateTimeOffset.UtcNow || SettingsManager.GetSetting(Setting.LatestVersion) == null)
            {
                var (ue, lv, cv) = await DownloadManager.CheckForUpdatesAsync();
                SettingsManager.SaveSetting(Setting.LastUpdateCheck, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                SettingsManager.SaveSetting(Setting.LatestVersion, lv);
                updateExists = ue;
                latestVersion = lv.ToVersion();
                currentVersion = cv.ToVersion();
            }
            else
            {
                var currentVersionString = Assembly.GetEntryAssembly()!
                                             .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                                             .Version;

                if (string.IsNullOrWhiteSpace(currentVersionString))
                {
                    logger.LogWarning("Cannot determine current application version.");
                    return;
                }

                var latestVersionString = SettingsManager.GetSetting(Setting.LatestVersion);
                if (latestVersionString == null)
                {
                    logger.LogWarning("Cannot determine latest application version.");
                    return;
                }

                currentVersion = currentVersionString.ToVersion();
                latestVersion = latestVersionString.ToVersion();
                updateExists = latestVersion > currentVersion;
            }
            
            if (updateExists)
            {
                logger.LogInformation(
                    "An update is available. Current Version {currentVersion} Latest Version {latestVersion}. Run `dotnet tool update WildernessLabs.Meadow.CLI --global` to update",
                    currentVersion,
                    latestVersion);
            }
        }
    }
}
