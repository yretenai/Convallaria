using System.Runtime.InteropServices;

namespace Convallaria;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct UpValueInfo {
	public bool InStack { get; set; }
	public byte Index { get; set; }
	public byte Kind { get; set; }

	public override string ToString() => $"{(InStack ? "R" : "UPV")}[{Index}]";
}
