using System;
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

	public enum OsType { Windows, Linux }

	public enum ReleaseType { Edge, Preview, Release, Staging, Aux01, Aux02, Production }

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

	internal static string GithubReleaseUrl()
	{
		string tag = Release switch
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

		string file = Platform switch
		{
			OsType.Windows => $"Carbon.Windows.{Target}.zip",
			OsType.Linux => $"Carbon.Linux.{Target}.tar.gz",
			_ => throw new ArgumentOutOfRangeException()
		};

		return $"http://github.com/{Repository}/releases/download/{tag}/{file}";
	}

	public static byte[] ReadFully(Stream input)
	{
		using MemoryStream ms = new MemoryStream();

		byte[] buffer = new byte[4096];
		int read;

		while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
		{
			ms.Write(buffer, 0, read);
		}

		return ms.ToArray();
	}

	internal static bool Execute()
	{
		string[] files =
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

		string url = GithubReleaseUrl();
		Logger.Log($"Updating component 'Carbon' using the '{Release} [{Platform}]' branch");

		var processInfo = new ProcessStartInfo
		{
			FileName = "curl",
			Arguments = $"-fSL -o {Path.Combine(Context.CarbonTemp, "patch.zip")} \"{url}\"",
			WindowStyle = ProcessWindowStyle.Hidden
		};
		var downloadProcess = Process.Start(processInfo);

		if (downloadProcess == null)
		{
			return false;
		}

		downloadProcess.WaitForExit();

		try
		{
			using FileStream archive = File.OpenRead(Path.Combine(Context.CarbonTemp, "patch.zip"));
			using IReader reader = ReaderFactory.Open(archive);
			
			while (reader.MoveToNextEntry())
			{
				if (reader.Entry.IsDirectory && files.Any(x => reader.Entry.Key.Contains(x)))
				{
					reader.WriteEntryToDirectory(Context.Game);
					continue;
				}

				if (!files.Contains(reader.Entry.Key, StringComparer.OrdinalIgnoreCase)) continue;
				string destination = Path.Combine(Context.Game, reader.Entry.Key);
				using EntryStream entry = reader.OpenEntryStream();
				using var fs = new FileStream(destination, FileMode.OpenOrCreate);
				Logger.Log($" - Updated {destination}");
				entry.CopyTo(fs);
			}

			return true;
		}
		catch (Exception e)
		{
			Logger.Error($"Error while updating 'Carbon [{Platform}]'", e);
			return false;
		}
	}
}
