using System;
using System.IO;
using System.Reflection;
using System.Security;
using Carbon.Core;
using Doorstop.Utility;

namespace Doorstop;

[SuppressUnmanagedCodeSecurity]
public sealed class Entrypoint
{
	private static readonly string[] PreloadPreUpdate =
	[
		Path.Combine(Defines.GetLibFolder(), "0Harmony.dll"),
		Path.Combine(Defines.GetLibFolder(), "Ben.Demystifier.dll"),
		Path.Combine(Defines.GetLibFolder(), "ZstdSharp.dll"),
		Path.Combine(Defines.GetLibFolder(), "SharpCompress.dll")
	];

	public static void Start()
	{
		Defines.Initialize();
		Config.Init();

		foreach (string file in PreloadPreUpdate)
		{
			try
			{
				var harmony = Assembly.LoadFile(file);
				Logger.Log($" Preloaded {harmony.GetName().Name} {harmony.GetName().Version}");
			}
			catch (Exception e)
			{
				Logger.Log($"Unable to preload '{file}' ({e?.Message})");
			}
		}

		if (Config.Singleton.SelfUpdating.Enabled)
		{
			try
			{
				SelfUpdater.Init();
				SelfUpdater.GetCarbonVersions();
				SelfUpdater.Execute();
			}
			catch (Exception ex)
			{
				Logger.Error("Failed self-updating process! Report to developers.", ex);
			}
		}
		else
		{
			Logger.Log(" Skipped self-updating process as it's disabled in the config.");
		}

		try
		{
			Assembly.Load(File.ReadAllBytes(Path.Combine(Defines.GetManagedFolder(), "Carbon.Startup.dll")))
				.GetType("Startup.Entrypoint")
				.GetMethod("Start", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
		}
		catch (Exception ex)
		{
			Logger.Error("Failed Entrypoint.Startup! Report to developers.", ex);
		}
	}
}
