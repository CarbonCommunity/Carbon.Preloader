using System;
using System.IO;
using System.Linq;
using Carbon.Core;
using Doorstop;
using Doorstop.Utility;
using Mono.Cecil;

namespace Carbon.Utilities;

public class Patch : IDisposable
{
	protected static AssemblyDefinition bootstrap;

	public static void Init()
	{
		try
		{
			bootstrap = AssemblyDefinition.ReadAssembly(
				new MemoryStream(File.ReadAllBytes(Path.Combine(Defines.GetManagedFolder(), "Carbon.Bootstrap.dll"))));
		}
		catch { }
	}
	public static void Uninit()
	{
		bootstrap?.Dispose();
		bootstrap = null;
	}

	private ReaderParameters readerParameters;
	protected AssemblyDefinition assembly;

	public string GetFullPath() => Path.Combine(filePath, fileName);

	public string filePath;
	public string fileName;

	public virtual bool IsAlreadyPatched => assembly.MainModule.Types.FirstOrDefault(x => x.Name == "<Module>").Fields.Any(x => x.Name == "CarbonPatched");
	public bool ShouldPublicize => Config.Singleton.Publicizer.PublicizedAssemblies.Any(x => fileName.StartsWith(x, StringComparison.OrdinalIgnoreCase));

	public Patch(string path, string name)
	{
		filePath = path;
		fileName = name;

		var resolver = new DefaultAssemblyResolver();
		readerParameters = new ReaderParameters { AssemblyResolver = resolver };
		resolver.AddSearchDirectory(Defines.GetRustManagedFolder());
	}

	public virtual bool Execute()
	{
		assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(GetFullPath())), readerParameters);

		if (IsAlreadyPatched || !ShouldPublicize)
		{
			return false;
		}

		Publicize();
		Logger.Log($"Publicized '{fileName}'");
		return true;

	}

	public void Write()
	{
		var module = assembly.MainModule.Types.FirstOrDefault(x => x.Name == "<Module>");
		module.Fields.Add(new FieldDefinition("CarbonPatched", FieldAttributes.Private | FieldAttributes.NotSerialized, assembly.MainModule.ImportReference(typeof(int))));

		try
		{
			Logger.Debug(" - Validating changes in-memory");

			using MemoryStream memoryStream = new MemoryStream();
			assembly.Write(memoryStream);
			memoryStream.Position = 0;

			Logger.Debug(" - Writing changes to disk");

			Dispose();

			using var outputStream = new MemoryStream();
			memoryStream.CopyTo(outputStream);
			File.WriteAllBytes(GetFullPath(), memoryStream.GetBuffer());
		}
		catch (Exception ex)
		{
			Logger.Error(ex);
		}
	}

	public void Dispose()
	{
		readerParameters = null;
		assembly?.Dispose();
		assembly = null;
	}

	protected bool IsPublic(string type, string method)
	{
		try
		{
			if (assembly == null)
			{
				throw new Exception($"Loaded assembly is null: {GetFullPath()}");
			}

			var typeDef = assembly.MainModule.Types.First(x => x.Name == type) ?? throw new Exception($"Unable to get type definition for '{type}'");
			var methodDef = typeDef.Methods.First(x => x.Name == method) ?? throw new Exception($"Unable to get method definition for '{method}'");
			return methodDef.IsPublic;
		}
		catch (Exception ex)
		{
			Logger.Error(ex.Message);
			throw ex;
		}
	}

	protected void Publicize()
	{
		if (assembly == null)
		{
			throw new Exception($"Loaded assembly is null: {GetFullPath()}");
		}

		Logger.Debug($" - Publicize assembly");

		var scope = assembly.MainModule.AssemblyReferences.OrderByDescending(a => a.Version).FirstOrDefault(a => a.Name == "mscorlib");
		var ctor = new MethodReference(".ctor", assembly.MainModule.TypeSystem.Void, declaringType: new TypeReference("System", "NonSerializedAttribute", assembly.MainModule, scope))
		{
			HasThis = true
		};

		foreach (var type in assembly.MainModule.Types)
		{
			Publicize(type, ctor);
		}
	}

	protected static void Publicize(TypeDefinition type, MethodReference ctor)
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

			foreach (var method in type.Methods)
			{
				if (Blacklist.IsBlacklisted($"{type.Name}.{method.Name}"))
				{
					Logger.Warn($"Excluded '{type.Name}.{method.Name}' due to blacklisting");
					continue;
				}

				method.IsPublic = true;
			}

			foreach (var field in type.Fields)
			{
				if (Blacklist.IsBlacklisted($"{type.Name}.{field.Name}"))
				{
					Logger.Warn($"Excluded '{type.Name}.{field.Name}' due to blacklisting");
					continue;
				}

				// Prevent publicize auto-generated fields
				if (type.Events.Any(x => x.Name == field.Name))
				{
					continue;
				}

				if (ctor != null && !field.IsPublic && field.CustomAttributes.All(a => a.AttributeType.FullName != "UnityEngine.SerializeField"))
				{
					field.IsNotSerialized = true;
					field.CustomAttributes.Add(item: new CustomAttribute(ctor));
				}

				field.IsPublic = true;
			}
		}
		catch (Exception ex)
		{
			Logger.Error(ex.Message);
			throw ex;
		}

		foreach (var subtype in type.NestedTypes)
		{
			Publicize(subtype, ctor);
		}
	}

}
