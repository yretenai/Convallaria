using System.Runtime.InteropServices;

namespace Convallaria;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public readonly record struct Instruction {
	public const int SAxOffset = 0xFFFFFF;
	public const int SBxOffset = 0xFFFF;
	public const int SOffset = 0x7F;

	public uint Bytes { get; init; }

	public uint A => (Bytes >> 7) & 0xFF;

	public uint B => (Bytes >> 16) & 0xFF;

	public uint C => (Bytes >> 24) & 0xFF;

	public bool K => ((Bytes >> 15) & 1) == 1;
	public uint Ax => Bytes >> 7;
	public uint Bx => Bytes >> 15;
	public uint Cx => Bytes >> 23;
	public int SAx => (int)Ax - SAxOffset;
	public int SBx => (int)Bx - SBxOffset;
	public int SCx => (int)Cx - SOffset;
	public int SA => (sbyte)A;
	public int SB => (sbyte)B;
	public int SC => (sbyte)C;

	public Opcode Opcode => (Opcode)(Bytes & 0x7F);
	public string OP => Opcode.ToString().ToUpper();
}
