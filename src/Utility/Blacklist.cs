using System.Text.RegularExpressions;

namespace Doorstop.Utility;

internal static class Blacklist
{
	private static readonly string[] Items =
	{
		@"^SpawnGroup.(GetSpawnPoint|PostSpawnProcess|Spawn)$",
		@"^ScientistNPC.OverrideCorpseName$",
		@"^TriggerParentElevator.IsClipping$",
		@"^DroppedItem.TransformHasMoved$",
		@"^HiddenValueBase$",
		@"^HiddenValue`1$"
	};

	internal static bool IsBlacklisted(string Name)
	{
		foreach (string Item in Items)
			if (Regex.IsMatch(Name, Item)) return true;
		return false;
	}
}
