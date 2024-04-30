using System.Runtime.InteropServices;

namespace Convallaria;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct Header {
	public uint Magic { get; set; }
	public ulong Version { get; set; }
	public byte InstrSize { get; set; }
	public byte IntSize { get; set; }
	public byte NumberSize { get; set; }
	public ulong Sanity1 { get; set; }
	public double Sanity2 { get; set; }
}
