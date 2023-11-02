using System.Collections.Generic;
using System.IO;
namespace Utility
{
	public static class IO
	{
		public static void Create(string folder, bool recreate = false)
		{
			if (!string.IsNullOrEmpty(folder))
			{
				if (recreate)
				{
					if (Directory.Exists(folder))
					{
						Directory.Delete(folder, true);
					}
				}

				if (!Directory.Exists(folder))
				{
					Directory.CreateDirectory(folder);
				}
			}
		}

		public static Dictionary<string, string> Copy(string folder, string destination, bool subdirectories = true, bool overwrite = true, SearchOption option = SearchOption.AllDirectories)
		{
			if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(destination))
			{
				if (Directory.Exists(folder))
				{
					var folderInfo = new DirectoryInfo(folder);
					var retDictionary = new Dictionary<string, string>();

					var folders = folderInfo.GetDirectories();
					Create(destination);
					retDictionary.Add(folderInfo.FullName, destination);

					var files = folderInfo.GetFiles();
					foreach (var file in files)
					{
						var tempPath = Path.Combine(destination, file.Name);
						file.CopyTo(tempPath, overwrite);
						retDictionary.Add(file.FullName, tempPath);
					}

					foreach (var subDirectory in folders)
					{
						var tempPath = Path.Combine(destination, subDirectory.Name);
						Copy(subDirectory.FullName, tempPath, subdirectories, overwrite, option);
						retDictionary.Add(subDirectory.FullName, tempPath);
					}

					return retDictionary;
				}
			}

			return null;
		}

		public static void Delete(string folder)
		{
			if (!string.IsNullOrEmpty(folder))
			{
				if (Directory.Exists(folder) && !string.IsNullOrEmpty(folder))
				{
					Directory.Delete(folder, true);
				}
			}
		}

		public static List<string> Move(string folder, string destination, bool subdirectories = true, bool overwrite = true, SearchOption option = SearchOption.AllDirectories)
		{
			if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(destination))
			{
				if (Directory.Exists(folder))
				{
					var folderInfo = new DirectoryInfo(folder);
					var retList = new List<string>();

					var folders = folderInfo.GetDirectories();
					Create(destination);
					retList.Add(destination);

					var files = folderInfo.GetFiles();
					foreach (var file in files)
					{
						var tempPath = Path.Combine(destination, file.Name);
						file.CopyTo(tempPath, overwrite);
						retList.Add(tempPath);
						file.Delete();
					}

					foreach (var subDirectory in folders)
					{
						var tempPath = Path.Combine(destination, subDirectory.Name);
						Copy(subDirectory.FullName, tempPath, subdirectories, overwrite, option);
						retList.Add(tempPath);
						subDirectory.Delete(subdirectories);
					}

					Delete(folder);

					return retList;
				}
			}

			return null;
		}

	}
}
