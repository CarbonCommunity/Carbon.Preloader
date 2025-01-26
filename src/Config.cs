using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Carbon.Core;
using Newtonsoft.Json;

namespace Doorstop;

[Serializable]
public class Config
{
	public static Config Singleton;

	public bool DeveloperMode { get; set; } = false;
	public SelfUpdatingConfig SelfUpdating { get; set; } = new();
	public PublicizerConfig Publicizer { get; set; } = new();

	public class SelfUpdatingConfig
	{
		public bool Enabled { get; set; } = true;
	}

	public class PublicizerConfig
	{
		public string[] PublicizedAssemblies { get; set; } =
		[
			"Assembly-CSharp.dll",
			"Facepunch.Console.dll",
			"Facepunch.Network.dll",
			"Facepunch.Nexus.dll",
			"Rust.Clans.Local.dll",
			"Rust.Harmony.dll",
			"Rust.Data.dll"
		];
		public string[] PublicizerMemberIgnores { get; set; } =
		{
			@"^HiddenValueBase$",
			@"^HiddenValue`1$",
			@"^Pool$"
		};

		public bool IsMemberIgnored(string name)
		{
			foreach (var item in PublicizerMemberIgnores)
			{
				if (Regex.IsMatch(name, item))
				{
					return true;
				}
			}
			return false;
		}
	}

	public static void Init()
	{
		if (Singleton != null)
		{
			return;
		}

		if (!File.Exists(Defines.GetConfigFile()))
		{
			Singleton = new();
			return;
		}

		Singleton = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Defines.GetConfigFile()));
	}
}
