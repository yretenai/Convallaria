using System.Runtime.InteropServices;

namespace Convallaria;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public readonly record struct Instruction {
	public const int SAxOffset = 0xFFFFFF;
	public const int SBxOffset = 0xFFFF;
	public const int SOffset = 0x7F;
	public uint Value { get; init; }

	public uint A => (Value >> 7) & 0xFF;

	public uint B => (Value >> 16) & 0xFF;

	public uint C => (Value >> 24) & 0xFF;

	public bool K => ((Value >> 15) & 1) == 1;
	public uint Ax => Value >> 7;
	public uint Bx => Value >> 15;
	public uint Cx => Value >> 23;
	public int SAx => (int) Ax - SAxOffset;
	public int SBx => (int) Bx - SBxOffset;
	public int SCx => (int) Cx - SOffset;
	public int SA => (sbyte) A;
	public int SB => (sbyte) B;
	public int SC => (sbyte) C;

	public Opcode Opcode => (Opcode) (Value & 0x7F);
	public string OP => Opcode.ToString().ToUpper();

	public Instruction() { }

	public Instruction(uint value) => Value = value;

	public Instruction(Opcode opcode, uint a) {
		Value = (uint) opcode & 0x7F;
		Value |= a << 7;
	}

	public Instruction(Opcode opcode, uint a, uint b) {
		Value = (uint) opcode & 0x7F;
		Value |= (a & 0xFF) << 7;
		Value |= b << 15;
	}

	public Instruction(Opcode opcode, uint a, uint b, bool k, uint c) {
		Value = (uint) opcode & 0x7F;
		Value |= (a & 0xFF) << 7;
		Value |= k ? 0x8000U : 0;
		Value |= (b & 0xFF) << 16;
		Value |= (c & 0xFF) << 24;
	}

	public Instruction(Opcode opcode, int a) {
		Value = (uint) opcode & 0x7F;
		Value |= (uint) (a + SAxOffset) << 7;
	}

	public Instruction(Opcode opcode, uint a, int b) {
		Value = (uint) opcode & 0x7F;
		Value |= (a & 0xFF) << 7;
		Value |= (uint) (b + SBxOffset) << 15;
	}

	public Instruction(Opcode opcode, uint a, uint b, bool k, int c) {
		Value = (uint) opcode & 0x7F;
		Value |= (a & 0xFF) << 7;
		Value |= k ? 0x8000U : 0;
		Value |= (b & 0xFF) << 16;
		Value |= ((uint) (c + SOffset) & 0xFF) << 24;
	}

	public static Instruction NOP() => new(Opcode.Jmp, 0);
}
