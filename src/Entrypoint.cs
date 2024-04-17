using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Patches;
using Utility;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

namespace Doorstop;

[SuppressUnmanagedCodeSecurity]
public sealed class Entrypoint
{
	private static readonly string[] Preload =
	{
		Path.Combine(Context.CarbonLib, "0Harmony.dll"),
		Path.Combine(Context.CarbonLib, "Ben.Demystifier.dll"),
		Path.Combine(Context.CarbonManaged, "Carbon.Compat.dll"),
	};

	private static readonly string[] Delete =
	{
		Path.Combine(Context.CarbonExtensions, "CCLBootstrap.dll"),
		Path.Combine(Context.CarbonExtensions, "Carbon.Ext.Discord.dll"),
		Context.CarbonReport,
		Path.Combine(Context.GameManaged, "x64"),
		Path.Combine(Context.GameManaged, "x86"),
		Path.Combine(Context.GameManaged, "Oxide.Common.dll"),
		Path.Combine(Context.GameManaged, "Oxide.Core.dll"),
		Path.Combine(Context.GameManaged, "Oxide.CSharp.dll"),
		Path.Combine(Context.GameManaged, "Oxide.MySql.dll"),
		Path.Combine(Context.GameManaged, "Oxide.References.dll"),
		Path.Combine(Context.GameManaged, "Oxide.Rust.dll"),
		Path.Combine(Context.GameManaged, "Oxide.SQLite.dll"),
		Path.Combine(Context.GameManaged, "Oxide.Unity.dll")
	};

	private static readonly Dictionary<KeyValuePair<string, string>, string> WildcardMove = new()
	{
		[new KeyValuePair<string, string>(Context.GameManaged, "Oxide.Ext.")] = Path.Combine(Context.CarbonExtensions)
	};

	private static readonly Dictionary<string, string> CopyTargetEmpty = new()
	{
		[Path.Combine(Context.Game, "oxide", "config")] = Path.Combine(Context.Carbon, "configs"),
		[Path.Combine(Context.Game, "oxide", "data")] = Path.Combine(Context.Carbon, "data"),
		[Path.Combine(Context.Game, "oxide", "plugins")] = Path.Combine(Context.Carbon, "plugins"),
		[Path.Combine(Context.Game, "oxide", "lang")] = Path.Combine(Context.Carbon, "lang")
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

	#region Native MonoProfiler

	[DllImport("CarbonNative")]
	public static unsafe extern void init_profiler(char* ptr, int length);

	[DllImport("__Internal", CharSet = CharSet.Ansi)]
	public static extern void mono_dllmap_insert(ModuleHandle assembly, string dll, string func, string tdll, string tfunc);

	public static unsafe void InitNative()
	{
#if UNIX
        mono_dllmap_insert(ModuleHandle.EmptyHandle, "CarbonNative", null, Path.Combine(Context.Carbon, "native", "libCarbonNative.so"), null);
#elif WIN
		mono_dllmap_insert(ModuleHandle.EmptyHandle, "CarbonNative", null, Path.Combine(Context.Carbon, "native", "CarbonNative.dll"), null);
#endif

		var path = Path.Combine(Context.Carbon, "config.profiler.json");

		fixed (char* ptr = path)
		{
			init_profiler(ptr, path.Length);
		}
	}

	#endregion

	public static void Start()
	{
		try
		{
			InitNative();
		}
		catch (Exception ex)
		{
			Logger.Error("Failed to init native", ex);
		}

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
		PerformWildcardMove();
		PerformRename();
		PerformCleanup();
		PerformCopyTargetEmpty();
	}

	public static void PerformCleanup()
	{
		foreach (var path in Delete)
		{
			try
			{
				if (!File.Exists(path))
				{
					if (Directory.Exists(path))
					{
						Logger.Log($" Removed '{Path.GetFileName(path)}' from RustDedicated_Data/Managed");
						Directory.Delete(path, true);
					}

					continue;
				}

				Logger.Log($" Removed '{Path.GetFileName(path)}' from RustDedicated_Data/Managed");
				File.Delete(path);
			}
			catch (Exception ex)
			{
				Logger.Error($" Cleanup process error! Failed removing '{path}'", ex);
			}
		}
	}

	public static void PerformWildcardMove()
	{
		foreach (var fileWildcard in WildcardMove)
		{
			var files = Directory.GetFiles(fileWildcard.Key.Key);

			foreach (var file in files)
			{
				if (!file.Contains(fileWildcard.Key.Value))
				{
					continue;
				}

				Logger.Log($" Moved {Path.GetFileName(file)} -> carbon/{Path.GetFileName(fileWildcard.Value)}");
				File.Move(file, $"{Path.Combine(fileWildcard.Value, Path.GetFileName(file))}");
			}
		}
	}

	public static void PerformCopyTargetEmpty()
	{
		if (CopyTargetEmpty.Any(x => Directory.Exists(x.Value) && new DirectoryInfo(x.Value).GetFiles().Any()))
		{
			return;
		}

		if (!Directory.Exists(Path.Combine(Context.Game, "oxide")))
		{
			return;
		}

		Logger.Log($" Fresh Carbon installation detected. Migrating Oxide directories.");

		foreach (var folder in CopyTargetEmpty)
		{
			if (!Directory.Exists(folder.Key))
			{
				continue;
			}

			var target = new DirectoryInfo(folder.Value);

			if (target.GetFiles().Any())
			{
				continue;
			}

			try
			{
				Logger.Log($" Copied oxide/{Path.GetFileName(folder.Key)} -> carbon/{Path.GetFileName(folder.Value)}");
				IO.Copy(folder.Key, folder.Value);
			}
			catch (Exception e)
			{
				Logger.Debug($" Unable to copy '{folder.Key}' -> '{folder.Value}' ({e?.Message})");
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
				Logger.Debug($" Unable to move '{folder.Key}' -> '{folder.Value}' ({e?.Message})");
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
				Logger.Debug($" Unable to rename '{file.Key}' -> '{file.Value}' ({e?.Message})");
			}
		}
	}
}
