namespace Convallaria;

public static class ConvallariaHelpers {
	public static Table? RemoveClosures(Table? table) {
		if (table == null) {
			return table;
		}

		var newTable = new Table();

		foreach (var (key, value) in table) {
			switch (value) {
				case Closure or FunctionCall:
					continue;
				case Table subTable:
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
