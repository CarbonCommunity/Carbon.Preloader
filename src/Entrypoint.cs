using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Patches;
using Utility;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

namespace Doorstop;

public sealed class Entrypoint
{
	private static readonly string[] Preload =
	{
		Path.Combine(Context.CarbonLib, "0Harmony.dll"),
		Path.Combine(Context.CarbonLib, "Ben.Demystifier.dll"),
		Path.Combine(Context.CarbonManaged, "Carbon.Compat.dll"),
	};

	private static readonly string[] Cleanup =
	{
		Path.Combine(Context.CarbonExtensions, "CCLBootstrap.dll"),
		Path.Combine(Context.CarbonExtensions, "Carbon.Ext.Discord.dll")
	};

	private static readonly Dictionary<string, string> Move = new()
	{
		[Path.Combine(Context.Carbon, "CCL", "oxide")] = Path.Combine(Context.CarbonExtensions),
		[Path.Combine(Context.Carbon, "CCL", "harmony")] = Path.Combine(Context.CarbonHarmony)
	};

	private static readonly Dictionary<string, string> Rename = new()
	{
		[Path.Combine(Context.Carbon, "config_client.json")] = Path.Combine(Context.Carbon, "config.client.json"),
		[Path.Combine(Context.Carbon, "carbonauto.cfg")] = Path.Combine(Context.Carbon, "config.auto.cfg")
	};

	public static void Start()
	{
		PerformCleanup();

		string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
		Logger.Debug($">> {assemblyName} is using a mono injector as entrypoint");

		using Sandbox<AssemblyCSharp> isolated1 = new Sandbox<AssemblyCSharp>();
		{
			if (!isolated1.Do.IsPublic("ServerMgr", "Shutdown"))
			{
				isolated1.Do.Publicize();
			}

			isolated1.Do.Patch();
			isolated1.Do.Write();
		}

		using Sandbox<RustHarmony> isolated2 = new Sandbox<RustHarmony>();
		{
			isolated2.Do.Patch();
			isolated2.Do.Write();
		}

		using Sandbox<FacepunchConsole> isolated3 = new Sandbox<FacepunchConsole>();
		{
			isolated3.Do.Patch();
			isolated3.Do.Write();
		}

		using Sandbox<FacepunchNetwork> isolated4 = new Sandbox<FacepunchNetwork>();
		{
			if (!isolated4.Do.IsPublic("Networkable", "sv"))
			{
				isolated4.Do.Publicize();
			}

			isolated4.Do.Write();
		}

		using Sandbox<RustClansLocal> isolated5 = new Sandbox<RustClansLocal>();
		{
			if (!isolated5.Do.IsPublic("LocalClanDatabase"))
			{
				isolated5.Do.Publicize();
			}

			isolated5.Do.Write();
		}

		using Sandbox<FacepunchNexus> isolated6 = new Sandbox<FacepunchNexus>();
		{
			if (!isolated6.Do.IsPublic("Util"))
			{
				isolated6.Do.Publicize();
			}

			isolated6.Do.Write();
		}

		foreach (string file in Preload)
		{
			try
			{
				Assembly harmony = Assembly.LoadFile(file);
				Logger.Log($" Preloaded {harmony.GetName().Name} {harmony.GetName().Version}");
			}
			catch (Exception e)
			{
				Logger.Log($"Unable to preload '{file}' ({e?.Message})");
			}
		}

		PerformMove();
		PerformRename();
	}

	public static void PerformCleanup()
	{
		foreach (var file in Cleanup)
		{
			if (!File.Exists(file))
			{
				continue;
			}

			try
			{
				File.Delete(file);
			}
			catch (Exception ex)
			{
				Logger.Error($"Cleanup process error! Failed removing '{file}'", ex);
			}
		}
	}

	public static void PerformMove()
	{
		foreach (var folder in Move)
		{
			if (!Directory.Exists(folder.Key))
			{
				continue;
			}

			if (!Directory.Exists(folder.Value))
			{
				Directory.CreateDirectory(folder.Value);
			}

			try
			{
				IO.Move(folder.Key, folder.Value);
			}
			catch (Exception e)
			{
				Logger.Debug($"Unable to move '{folder.Key}' -> '{folder.Value}' ({e?.Message})");
			}
		}
	}

	public static void PerformRename()
	{
		foreach (var file in Rename)
		{
			try
			{
				if (!File.Exists(file.Key)) continue;

				File.Move(file.Key, file.Value);
			}
			catch (Exception e)
			{
				Logger.Debug($"Unable to rename '{file.Key}' -> '{file.Value}' ({e?.Message})");
			}
		}
	}
}
