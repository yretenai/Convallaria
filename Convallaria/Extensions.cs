using System;
using System.Collections.Generic;

namespace Convallaria;

public static class Extensions {
	public static object? StripNewlines(this object? val) {
		if (val is string str) {
			return str.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
		}

		return val;
	}

	public static Dictionary<object, object?>? RemoveClosures(this Dictionary<object, object?>? table) {
		if (table == null) {
			return table;
		}

		foreach (var (key, value) in table) {
			switch (value) {
				case Closure or FunctionCall:
					table.Remove(key);
					continue;
				case Dictionary<object, object?> subTable:
					RemoveClosures(subTable);
					break;
			}
		}

		return table;
	}
}
