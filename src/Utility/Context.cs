using System;
using System.IO;
using Carbon.Extensions;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

namespace Utility;

internal sealed class Context
{
	private static readonly string[]
		Needles = { ".", "..", "../.." };

	internal static readonly string
		Game, GameManaged,
		Carbon, CarbonManaged, CarbonLib, CarbonHooks, CarbonModules, CarbonExtensions, CarbonReport, CarbonHarmony, CarbonLogs, CarbonLang, CarbonTemp, CarbonConfig;

	static Context()
	{
		Game = null;
		foreach (string Needle in Needles)
		{
			string t = Path.GetFullPath(Path.Combine(
				AppDomain.CurrentDomain.BaseDirectory, Needle));

			if (!System.IO.Directory.Exists(Path.Combine(t, "RustDedicated_Data"))) continue;
			Game = t;
			break;
		}

		try
		{
			if (Game == null) throw new System.Exception("Unable to find root folder");
			GameManaged = Path.GetFullPath(Path.Combine(Game, "RustDedicated_Data", "Managed"));

			Carbon = Path.GetFullPath(CommandLineEx.GetArgumentResult("-carbon.rootdir", Path.Combine(Game, "carbon")));
			if (!Directory.Exists(Carbon)) throw new Exception("Carbon folder is missing");

			CarbonLogs = Path.Combine(Carbon, "logs");
			if (!Directory.Exists(CarbonLogs)) Directory.CreateDirectory(CarbonLogs);

			CarbonManaged = Path.Combine(Carbon, "managed");
			if (!Directory.Exists(CarbonManaged)) Directory.CreateDirectory(CarbonManaged);

			CarbonLib = Path.Combine(CarbonManaged, "lib");
			if (!Directory.Exists(CarbonLib)) Directory.CreateDirectory(CarbonLib);

			CarbonHooks = Path.Combine(CarbonManaged, "hooks");
			if (!Directory.Exists(CarbonHooks)) Directory.CreateDirectory(CarbonModules);

			CarbonExtensions = Path.Combine(Carbon, "extensions");
			if (!Directory.Exists(CarbonExtensions)) Directory.CreateDirectory(CarbonExtensions);

			CarbonLang = Path.Combine(Carbon, "lang");
			if (!Directory.Exists(CarbonLang)) Directory.CreateDirectory(CarbonLang);

			CarbonHarmony = Path.Combine(Carbon, "harmony");
			if (!Directory.Exists(CarbonHarmony)) Directory.CreateDirectory(CarbonHarmony);

			CarbonModules = Path.Combine(CarbonManaged, "modules");
			if (!Directory.Exists(CarbonModules)) Directory.CreateDirectory(CarbonModules);

			CarbonTemp = Path.Combine(Carbon, "temp");
			if (!Directory.Exists(CarbonTemp)) Directory.CreateDirectory(CarbonTemp);

			CarbonConfig = Path.Combine(Carbon, "config.json");
			CarbonReport = Path.Combine(Carbon, "reports");
		}
		catch (System.Exception e)
		{
			Logger.Error("Critical error while loading Carbon", e);
			System.Environment.Exit(1);
			throw;
		}

	}
}
