using System;
using System.IO;
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

	public void Validate() {
		if (Magic is not 0x61754C1B) {
			throw new NotSupportedException("Not a Lua file");
		}

		if ((Version & 0xFF) is not 0x54) {
			throw new NotSupportedException("Only Lua 5.4 is supported");
		}

		if (((Version >> 8) & 0xFF) is not 0) {
			throw new NotSupportedException("Only Lua 5.4 Format 0 is supported");
		}

		if (Version >> 16 is not 0xA1A0A0D9319) {
			throw new NotSupportedException("Invalid Lua Header Check (Data Check)");
		}

		if (InstrSize != 4) {
			throw new NotSupportedException("Invalid Lua Header Check (Instruction Size is not 4)");
		}

		if (IntSize != 8) {
			throw new NotSupportedException("Invalid Lua Header Check (Integer Size is not 8)");
		}

		if (NumberSize != 8) {
			throw new NotSupportedException("Invalid Lua Header Check (Number Size is not 8)");
		}

		if (Sanity1 is not 0x5678) {
			throw new InvalidDataException("Failed Integer check");
		}

		if (Sanity2 is not 370.5d) {
			throw new InvalidDataException("Failed Number check");
		}
	}
}
