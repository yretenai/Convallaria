using System;

namespace Convallaria;

public static class Extensions {
	public static object? StripNewlines(this object? val) {
		if (val is string str) {
			return str.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
		}

		return val;
	}
}
