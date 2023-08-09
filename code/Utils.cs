using Editor;
using Sandbox;
using SM1Minigames;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TerrorTown;

/// <summary>
/// This class exists because sandbox has bugs :(
/// </summary>
public static class SM1Utils
{
	public static string Lists2Json( IList<Minigame> allgames, IList<Minigame> enabledgames )
	{
		// Merge the two lists to get a combined list of all Minigames and all enabled Minigames
		var allMinigames = allgames.Concat( enabledgames );

		// Group the Minigames by their names and check if they exist in both lists
		var minigameDictionary = allMinigames
			.GroupBy( minigame => minigame.Name )
			.ToDictionary(
				group => group.Key,
				group => group.Count() == 2 // If count is 2, it exists in both lists; otherwise, it exists in only one list
			);

		// Return the resulting dictionary as a JSON
		string json = Json.Serialize(minigameDictionary);
		return json;
	}
}
