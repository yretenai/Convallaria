using System;
using System.Collections.Generic;

namespace Convallaria;

public record DebugInfo {
	public DebugInfo(ILuaReader reader) {
		LineInfo = reader.Slice((int) reader.ReadULEB128());
		var absLineInfoCount = (int) reader.ReadULEB128();
		AbsLineInfo.EnsureCapacity(absLineInfoCount);
		for (var i = 0; i < absLineInfoCount; ++i) {
			AbsLineInfo.Add(new LineInfo {
				Counter = reader.ReadULEB128(),
				Line = reader.ReadULEB128(),
			});
		}

		var locVarInfoCount = (int) reader.ReadULEB128();
		LocVarInfo.EnsureCapacity(locVarInfoCount);
		for (var i = 0; i < locVarInfoCount; ++i) {
			LocVarInfo.Add(new LocVariable {
				VarName = reader.ReadString() ?? "__var__",
				StartCounter = reader.ReadULEB128(),
				EndCounter = reader.ReadULEB128(),
			});
		}

		var upVarNameCount = (int) reader.ReadULEB128();
		UpValueNames.EnsureCapacity(upVarNameCount);
		for (var i = 0; i < upVarNameCount; ++i) {
			UpValueNames.Add(reader.ReadString() ?? "__var__");
		}
	}

	public ReadOnlyMemory<byte> LineInfo { get; }
	public List<LineInfo> AbsLineInfo { get; } = [];
	public List<LocVariable> LocVarInfo { get; } = [];
	public List<string> UpValueNames { get; } = [];
}
