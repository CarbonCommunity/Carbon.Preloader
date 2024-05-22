using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Carbon.Extensions;
using Bootstrap;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;

namespace Utility;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

public static class SelfUpdater
{
	internal static readonly Random Random = new();

	internal static OsType Platform;
	internal static ReleaseType Release;
	internal static string Repository;
	internal static string Target;
	internal static bool IsMinimal;
	internal static readonly string[] Files =
	[
		@"carbon/managed/Carbon.dll",
		@"carbon/managed/Carbon.Common.dll",
		@"carbon/managed/Carbon.Common.Client.dll",
		@"carbon/managed/Carbon.Bootstrap.dll",
		@"carbon/managed/Carbon.Compat.dll",
		@"carbon/managed/Carbon.Preloader.dll",
		@"carbon/managed/Carbon.SDK.dll",
		@"carbon/managed/hooks/Carbon.Hooks.Base.dll",
		@"carbon/managed/lib",
		@"carbon/managed/modules",
		@"carbon/native/CarbonNative.dll"
	];
	internal static string Tag => Release switch
	{
		ReleaseType.Edge => "edge_build",
		ReleaseType.Preview => "preview_build",
		ReleaseType.Release => "release_build",
		ReleaseType.Staging => "rustbeta_staging_build",
		ReleaseType.Aux01 => "rustbeta_aux01_build",
		ReleaseType.Aux02 => "rustbeta_aux02_build",
		ReleaseType.Production => "production_build",
		_ => throw new ArgumentOutOfRangeException()
	};
	internal static string File => Platform switch
	{
		OsType.Windows => $"Carbon.Windows.{Target}.zip",
		OsType.Linux => $"Carbon.Linux.{Target}.tar.gz",
		_ => throw new ArgumentOutOfRangeException()
	};

	public enum OsType { Windows, Linux }
	public enum ReleaseType { Edge, Preview, Release, Staging, Aux01, Aux02, Production }

	public const string CarbonVersionsEndpoint = "https://carbonmod.gg/api";

	internal static void Init()
	{
		Platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) switch
		{
			true => OsType.Windows,
			false => OsType.Linux
		};

		Repository = @"CarbonCommunity/Carbon";

		Release =
#if PREVIEW
		ReleaseType.Preview;
#elif RELEASE
		ReleaseType.Release;
#elif STAGING
		ReleaseType.Staging;
#elif AUX01
		ReleaseType.Aux01;
#elif AUX02
		ReleaseType.Aux02;
#elif PROD
		ReleaseType.Production;
#else
		ReleaseType.Edge;
#endif

		IsMinimal =
#if MINIMAL
			true;
#else
			false;
#endif

		Target = IsMinimal ? "Minimal" :
#if PROD
		"Release"
#else
		"Debug";
#endif
	}

	internal static void Execute()
	{
		var tag = Versions.GetVersion(Tag);

		if (tag == null || string.IsNullOrEmpty(tag.Version))
		{
			return;
		}

		if (tag.Version.Equals(Versions.CurrentVersion))
		{
			Logger.Log($" Carbon {Target} is up to date, no self-updating necessary. Running {Release} build [{Versions.CurrentVersion}] on tag '{Tag}'.");
			return;
		}

		var url = GithubReleaseUrl();
		Logger.Log($" Carbon {Target} is out of date and now self-updating - {Release} [{Tag}] on {Platform} [{Versions.CurrentVersion} -> {tag.Version}]");

		IO.ExecuteProcess("curl", $"-fSL -o \"{Path.Combine(Context.CarbonTemp, "patch.zip")}\" \"{url}\"");

		var count = 0;

		try
		{
			using FileStream archive = System.IO.File.OpenRead(Path.Combine(Context.CarbonTemp, "patch.zip"));
			using IReader reader = ReaderFactory.Open(archive);
			{
				Console.Write($" Updating Carbon... ");

				while (reader.MoveToNextEntry())
				{
					var entry = reader.Entry;

					if (entry.IsDirectory || !Files.Any(x => entry.Key.Contains(x))) continue;

					var destination = Path.Combine(Context.Game, entry.Key);
					using var fileStream = new FileStream(destination, FileMode.OpenOrCreate);
					using var entryStream = reader.OpenEntryStream();
					entryStream.CopyTo(fileStream);

					Console.Write($"{Environment.NewLine} - {entry.Key} ({ByteEx.Format(entry.Size).ToUpper()})");
					count++;
				}
			}
			Console.WriteLine(string.Empty);
		}
		catch (Exception e)
		{
			Logger.Error($"Error while updating 'Carbon [{Platform}]'", e);
		}

		Logger.Log($" Carbon {Target} finished self-updating {count:n0} files. You're now running the latest {Release} build.");
	}

	internal static bool GetCarbonVersions()
	{
		var tempPath = Path.Combine(Context.CarbonTemp, "versions.json");
		var gotVersions = IO.ExecuteProcess("curl", $"-fSL -o \"{tempPath}\" \"{CarbonVersionsEndpoint}\"");

		return gotVersions && Versions.Init(System.IO.File.ReadAllText(tempPath));
	}

	internal static string GithubReleaseUrl()
	{
		return $"http://github.com/{Repository}/releases/download/{Tag}/{File}";
	}
}
