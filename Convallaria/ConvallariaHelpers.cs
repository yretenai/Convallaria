using System.Collections.Generic;

namespace Convallaria;

public static class ConvallariaHelpers {
	public static Dictionary<object, object?>? RemoveClosures(Dictionary<object, object?>? table) {
		if (table == null) {
			return table;
		}

		var newTable = new Dictionary<object, object?>();

		foreach (var (key, value) in table) {
			switch (value) {
				case Closure or FunctionCall:
					continue;
				case Dictionary<object, object?> subTable:
					newTable[key] = RemoveClosures(subTable);
					break;
				default:
					newTable[key] = value;
					break;
			}
		}

		return newTable;
	}
}
