using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Carbon.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Doorstop.Utility;

namespace Carbon.Utilities.Patches;

public class FacepunchConsole() : Patch(Defines.GetRustManagedFolder(), "Facepunch.Console.dll")
{
	public override bool IsAlreadyPatched => IsPublic("ConsoleSystem", "Internal");

	public override bool Execute()
	{
		if (!base.Execute()) return false;

		try
		{
			ExposeIndexAllSetter();
			ExposeConsoleSystemArgConstructor();
		}
		catch (Exception ex)
		{
			Logger.Error(ex);
			return false;
		}

		return true;
	}

	private void ExposeIndexAllSetter()
	{
		var type = assembly.MainModule.GetType("ConsoleSystem/Index");
		var items = (string[])[ "All" ];

		foreach (string item in items)
		{
			try
			{
				Logger.Debug($" - Patching {type.Name}.{item}");
				type.Properties.Single(x => x.Name == item).SetMethod.IsPublic = true;
			}
			catch (System.Exception e)
			{
				Logger.Debug($" - Patching failed: {e.Message}");
			}
		}
	}
	private void ExposeConsoleSystemArgConstructor()
	{
		var type = assembly.MainModule.GetType("ConsoleSystem/Arg");
		var ctor = type.GetConstructors().FirstOrDefault();

		try
		{
			ctor.IsPublic = true;
		}
		catch (Exception e)
		{
			Logger.Debug($" - Patching failed: {e.Message}");
		}
	}
}
