﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Doorstop;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Utility;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

public static class SelfUpdater
{
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
			Logger.Log($" Carbon is up to date. No self-updating necessary.");
			return;
		}

		string url = GithubReleaseUrl();
		Logger.Log($"Updating component 'Carbon' using the '{Release} [{Platform}]' branch [{tag.Version}] {Versions.CurrentVersion}");

		IO.ExecuteProcess("curl", $"-fSL -o \"{Path.Combine(Context.CarbonTemp, "patch.zip")}\" \"{url}\"");

		try
		{
			using FileStream archive = System.IO.File.OpenRead(Path.Combine(Context.CarbonTemp, "patch.zip"));
			using IReader reader = ReaderFactory.Open(archive);

			while (reader.MoveToNextEntry())
			{
				// Logger.Log($" - Seeking {reader.Entry.Key} ?{reader.Entry.IsDirectory}");

				if (!Files.Any(x => reader.Entry.Key.Contains(x))) continue;

				string destination = Path.Combine(Context.Game, reader.Entry.Key);
				using EntryStream entry = reader.OpenEntryStream();
				using var fs = new FileStream(destination, FileMode.OpenOrCreate);
				entry.CopyTo(fs);
			}
		}
		catch (Exception e)
		{
			Logger.Error($"Error while updating 'Carbon [{Platform}]'", e);
		}
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
