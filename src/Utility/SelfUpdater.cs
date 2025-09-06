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
	private const string CarbonVersionsEndpoint = "https://api.carbonmod.gg/releases";

	private static OsType Platform;
	private static ReleaseType Release;
	private static string Target;
	private static bool IsMinimal;
	private static string LocalCarbonProtocol;
	private static string LocalRustProtocol;
	private static readonly string[] Files =
	[
		"carbon/managed",
		"carbon/native"
	];
	private static string Tag => Release switch
	{
		ReleaseType.Edge => "edge_build",
		ReleaseType.Preview => "preview_build",
		ReleaseType.RustRelease => "rustbeta_release_build",
		ReleaseType.RustStaging => "rustbeta_staging_build",
		ReleaseType.RustAux01 => "rustbeta_aux01_build",
		ReleaseType.RustAux02 => "rustbeta_aux02_build",
		ReleaseType.RustAux03 => "rustbeta_aux03_build",
		ReleaseType.Production => "production_build",
		ReleaseType.QA => "qa_build",
		_ => throw new ArgumentOutOfRangeException()
	};
	private static string File => Platform switch
	{
		OsType.Windows => $"Carbon.Windows.{Target}.zip",
		OsType.Linux => $"Carbon.Linux.{Target}.tar.gz",
		_ => throw new ArgumentOutOfRangeException()
	};
	private static string LocalProtocolFile => Path.Combine(Defines.GetRustManagedFolder(), ".carbon");

	private enum OsType { Windows, Linux }
	private enum ReleaseType { Edge, Preview, RustRelease, RustStaging, RustAux01, RustAux02, RustAux03, Production, QA }

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
#elif RUST_AUX03
		ReleaseType.RustAux03;
#elif QA
		ReleaseType.QA;
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
#if DEBUG
		"Debug";
#else
		"Release";
#endif

		if (System.IO.File.Exists(LocalProtocolFile))
		{
			var lines = System.IO.File.ReadAllLines(LocalProtocolFile);
			LocalRustProtocol = lines[0];
			LocalCarbonProtocol = lines[1];
		}
	}

	internal static void Execute()
	{
		var versionOverride = GetVersionOverride();
		var hasVersionOverride = !string.IsNullOrEmpty(versionOverride);
		var tag = Versions.GetVersion(Tag);

		if (tag == null || string.IsNullOrEmpty(tag.Version))
		{
			return;
		}

		if (!hasVersionOverride && tag.Version.Equals(Versions.CurrentVersion))
		{
			Logger.Log($" Carbon {Target} is up to date, no self-updating necessary. Running {Release} build [{Versions.CurrentVersion}] on tag '{Tag}'.");
			return;
		}

		var url = versionOverride ?? Config.Singleton?.SelfUpdating?.RedirectUri ?? GithubReleaseUrl();

		if (hasVersionOverride)
		{
			Logger.Log($" Carbon version override detected and now self-updating - {Release} [{Tag}] on {Platform} [{url}]");
		}
		else
		{
			Logger.Log($" Carbon {Target} is out of date and now self-updating - {Release} [{Tag}] on {Platform} [{Versions.CurrentVersion} -> {tag.Version}]");
		}

		OsEx.ExecuteProcess("curl", $"-H \"Cache-Control: no-store, no-cache, must-revalidate, max-age=0\" -H \"Pragma: no-cache\" -fSL -o \"{Path.Combine(Defines.GetTempFolder(), "patch.zip")}\" \"{url}\"");

		var count = 0;

		try
		{
			using FileStream archive = System.IO.File.OpenRead(Path.Combine(Defines.GetTempFolder(), "patch.zip"));
			using IReader reader = ReaderFactory.Open(archive);
			{
				Console.Write(" Updating Carbon... ");

				var carbonRoot = Defines.GetRootFolder();
				while (reader.MoveToNextEntry())
				{
					var entry = reader.Entry;

					if (entry.IsDirectory || !Files.Any(x => entry.Key.Contains(x)))
					{
						continue;
					}

					var relativeFilePath = entry.Key.Replace("carbon/", string.Empty).Replace("carbon\\", string.Empty);
					var destination = Path.Combine(carbonRoot, relativeFilePath);
					using var fileStream = new FileStream(destination, FileMode.OpenOrCreate);
					using var entryStream = reader.OpenEntryStream();
					entryStream.CopyTo(fileStream);

					Console.Write($"{Environment.NewLine} - {relativeFilePath} ({entry.Size.Format().ToUpper()})");
					count++;
				}
			}
			Console.WriteLine(string.Empty);
		}
		catch (Exception e)
		{
			Logger.Error($"Error while updating 'Carbon [{Platform}]'", e);
		}

		if (hasVersionOverride)
		{
			Logger.Log($" Carbon finished self-updating the custom version override with {count:n0} files. You're now running the latest build.");
		}
		else
		{
			Logger.Log($" Carbon {Target} finished self-updating {count:n0} files. You're now running the latest {Release} build.");
		}
	}

	internal static bool GetCarbonVersions()
	{
		var tempPath = Path.Combine(Defines.GetTempFolder(), "versions.json");
		var gotVersions = OsEx.ExecuteProcess("curl", $"-H \"Cache-Control: no-store, no-cache, must-revalidate, max-age=0\" -H \"Pragma: no-cache\" -fSL -o \"{tempPath}\" \"{CarbonVersionsEndpoint}\"");

		return gotVersions && Versions.Init(System.IO.File.ReadAllText(tempPath));
	}

	internal static string GithubReleaseUrl()
	{
		return $"http://github.com/{Repository}/releases/download/{Tag}/{File}";
	}

	internal static string GetVersionOverride()
	{
		var path = Path.Combine(Defines.GetTempFolder(), "versionoverride.txt");
		if (System.IO.File.Exists(path))
		{
			var text = System.IO.File.ReadAllText(path);
			System.IO.File.Delete(path);
			return text;
		}
		return null;
	}
}
