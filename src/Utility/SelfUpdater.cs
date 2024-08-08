using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Carbon.Core;
using Carbon.Extensions;
using SharpCompress.Readers;

namespace Doorstop.Utility;

public static class SelfUpdater
{
	private const string Repository = "CarbonCommunity/Carbon";
	private const string CarbonVersionsEndpoint = "https://carbonmod.gg/api";

	private static OsType Platform;
	private static ReleaseType Release;
	private static string Target;
	private static bool IsMinimal;
	private static readonly string[] Files =
	[
		"carbon/managed",
		"carbon/native/CarbonNative.dll"
	];
	private static string Tag => Release switch
	{
		ReleaseType.Edge => "edge_build",
		ReleaseType.Preview => "preview_build",
		ReleaseType.RustRelease => "rustbeta_release_build",
		ReleaseType.RustStaging => "rustbeta_staging_build",
		ReleaseType.RustAux01 => "rustbeta_aux01_build",
		ReleaseType.RustAux02 => "rustbeta_aux02_build",
		ReleaseType.Production => "production_build",
		_ => throw new ArgumentOutOfRangeException()
	};
	private static string File => Platform switch
	{
		OsType.Windows => $"Carbon.Windows.{Target}.zip",
		OsType.Linux => $"Carbon.Linux.{Target}.tar.gz",
		_ => throw new ArgumentOutOfRangeException()
	};

	private enum OsType { Windows, Linux }
	private enum ReleaseType { Edge, Preview, RustRelease, RustStaging, RustAux01, RustAux02, Production }

	internal static void Init()
	{
		Platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) switch
		{
			true => OsType.Windows,
			false => OsType.Linux
		};

		Release =
#if PROD
		ReleaseType.Production;
#elif PREVIEW
		ReleaseType.Preview;
#elif RUST_STAGING
		ReleaseType.RustStaging;
#elif RUST_RELEASE
		ReleaseType.RustRelease;
#elif RUST_AUX01
		ReleaseType.RustAux01;
#elif RUST_AUX02
		ReleaseType.RustAux02;
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
		"Release";
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

		IO.ExecuteProcess("curl", $"-H \"Cache-Control: no-store, no-cache, must-revalidate, max-age=0\" -H \"Pragma: no-cache\" -fSL -o \"{Path.Combine(Defines.GetTempFolder(), "patch.zip")}\" \"{url}\"");

		var count = 0;

		try
		{
			using FileStream archive = System.IO.File.OpenRead(Path.Combine(Defines.GetTempFolder(), "patch.zip"));
			using IReader reader = ReaderFactory.Open(archive);
			{
				Console.Write(" Updating Carbon... ");

				while (reader.MoveToNextEntry())
				{
					var entry = reader.Entry;

					if (entry.IsDirectory || !Files.Any(x => entry.Key.Contains(x))) continue;

					var destination = Path.Combine(Defines.GetRustRootFolder(), entry.Key);
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
		var tempPath = Path.Combine(Defines.GetTempFolder(), "versions.json");
		var gotVersions = IO.ExecuteProcess("curl", $"-H \"Cache-Control: no-store, no-cache, must-revalidate, max-age=0\" -H \"Pragma: no-cache\" -fSL -o \"{tempPath}\" \"{CarbonVersionsEndpoint}\"");

		return gotVersions && Versions.Init(System.IO.File.ReadAllText(tempPath));
	}

	internal static string GithubReleaseUrl()
	{
		return $"http://github.com/{Repository}/releases/download/{Tag}/{File}";
	}
}
