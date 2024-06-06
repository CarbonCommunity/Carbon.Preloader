using System;
using System.IO;
using Newtonsoft.Json;
using Utility;

/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

namespace Bootstrap;

[Serializable]
public class Config
{
	public static Config Singleton;

	public SelfUpdatingConfig SelfUpdating { get; set; } = new();

	public class SelfUpdatingConfig
	{
		public bool Enabled { get; set; } = true;
	}

	public static void Init()
	{
		if (Singleton != null)
		{
			return;
		}

		if (!File.Exists(Context.CarbonConfig))
		{
			Singleton = new();
			return;
		}

		Singleton = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Context.CarbonConfig));
	}
}
