
/*
 *
 * Copyright (c) 2022-2024 Carbon Community
 * All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Carbon.Core;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Doorstop.Utility;

namespace Doorstop.Patches;

internal sealed class FacepunchConsole : MarshalByRefObject
{
	private static DefaultAssemblyResolver _resolver;
	private AssemblyDefinition _assembly;
	private string _filename;

	public void Init()
	{
		_filename = Path.Combine(Defines.GetRustManagedFolder(), "Facepunch.Console.dll");

		if (!File.Exists(_filename))
			throw new Exception($"Assembly file '{_filename}' was not found");

		_resolver = new DefaultAssemblyResolver();
		_resolver.AddSearchDirectory(Defines.GetLibFolder());
		_resolver.AddSearchDirectory(Defines.GetManagedModulesFolder());
		_resolver.AddSearchDirectory(Defines.GetManagedFolder());
		_resolver.AddSearchDirectory(Defines.GetRustManagedFolder());
		foreach (var search in _resolver.GetSearchDirectories())
		{
			Console.WriteLine($"FacepunchConsole Searching : {search}");
		}

		_assembly = AssemblyDefinition.ReadAssembly(_filename,
			parameters: new ReaderParameters { AssemblyResolver = _resolver });
	}

	internal bool IsPublic(string Type, string Property)
	{
		try
		{
			if (_assembly == null) throw new Exception($"Loaded assembly is null: {_filename}");

			TypeDefinition t = _assembly.MainModule.GetType(Type);
			if (t == null) throw new Exception($"Unable to get type definition for '{Type}'");

			PropertyDefinition m = t.Properties.First(x => x.Name == Property);
			if (m == null) throw new Exception($"Unable to get property definition for '{Property}'");

			return m.SetMethod.IsPublic;
		}
		catch (System.Exception ex)
		{
			Logger.Error(ex.Message);
			throw ex;
		}
	}

	internal static void Publicize(TypeDefinition type, MethodReference ctor)
	{
		try
		{
			if (Blacklist.IsBlacklisted(type.Name))
			{
				Logger.Warn($"Excluded '{type.Name}' due to blacklisting");
				return;
			}

			if (type.IsNested)
			{
				type.IsNestedPublic = true;
			}
			else
			{
				type.IsPublic = true;
			}

			foreach (MethodDefinition Method in type.Methods)
			{
				if (Blacklist.IsBlacklisted($"{type.Name}.{Method.Name}"))
				{
					Logger.Warn($"Excluded '{type.Name}.{Method.Name}' due to blacklisting");
					continue;
				}

				Method.IsPublic = true;
			}

			foreach (FieldDefinition Field in type.Fields)
			{
				if (Blacklist.IsBlacklisted($"{type.Name}.{Field.Name}"))
				{
					Logger.Warn($"Excluded '{type.Name}.{Field.Name}' due to blacklisting");
					continue;
				}

				// Prevent publicize auto-generated fields
				if (type.Events.Any(x => x.Name == Field.Name)) continue;

				if (ctor != null && !Field.IsPublic && !Field.CustomAttributes.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeField"))
				{
					Field.IsNotSerialized = true;
					Field.CustomAttributes.Add(item: new CustomAttribute(ctor));
				}

				Field.IsPublic = true;
			}
		}
		catch (System.Exception ex)
		{
			Logger.Error(ex.Message);
			throw ex;
		}

		foreach (TypeDefinition childType in type.NestedTypes)
			Publicize(childType, ctor);
	}

	internal void Patch()
	{
		try
		{
			Override_IndexAll_Setter();
			Override_Constructor_Modifier();
		}
		catch (System.Exception ex)
		{
			Logger.Error(ex.Message);
			throw ex;
		}
	}

	private void Override_IndexAll_Setter()
	{
		TypeDefinition type = _assembly.MainModule.GetType("ConsoleSystem/Index");
		string[] Items = { "All" };

		foreach (string Item in Items)
		{
			try
			{
				Logger.Debug($" - Patching {type.Name}.{Item}");

				PropertyDefinition method = type.Properties.Single(x => x.Name == Item);
				method.SetMethod.IsPublic = true;
			}
			catch (System.Exception e)
			{
				Logger.Debug($" - Patching failed: {e.Message}");
			}
		}
	}

	private void Override_Constructor_Modifier()
	{
		TypeDefinition type = _assembly.MainModule.GetType("ConsoleSystem/Arg");
		MethodDefinition ctor = type.GetConstructors().FirstOrDefault();

		try
		{
			ctor.IsPublic = true;
		}
		catch (Exception e)
		{
			Logger.Debug($" - Patching failed: {e.Message}");
		}
	}

	internal void Write()
	{
		try
		{
			Logger.Debug(" - Validating changes in-memory");

			using MemoryStream memoryStream = new MemoryStream();
			_assembly.Write(memoryStream);
			memoryStream.Position = 0;
			_assembly.Dispose();

			Logger.Debug(" - Writing changes to disk");

			using FileStream outputStream = File.Open(_filename, FileMode.Create); //  + ".new.dll"
			memoryStream.CopyTo(outputStream);
		}
		catch (System.Exception ex)
		{
			Logger.Error(ex.Message);
			throw ex;
		}
	}
}
