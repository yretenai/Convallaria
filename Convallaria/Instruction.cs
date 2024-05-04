using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Convallaria;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public readonly record struct Instruction {
	public const int SAxOffset = 0xFFFFFF;
	public const int SBxOffset = 0xFFFF;
	public const int SOffset = 0x7F;
	public uint Value { get; init; }

	public Opcode Opcode => (Opcode) (Value & 0x7F);
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

	public override string ToString() {
		var sb = new StringBuilder();
		Dump(sb);
		return sb.ToString();
	}

	public void Dump(StringBuilder sb, List<object?>? constants = null, List<string>? UpValueNames = null) {
		constants ??= [];
		UpValueNames ??= [];
		var op = Opcode.ToString().ToUpperInvariant();
		switch (Opcode) {
			case Opcode.VarargPrep:
			case Opcode.Return0:
				sb.Append($"{op}");
				break;
			case Opcode.LoadFalse:
			case Opcode.LFalseSkip:
			case Opcode.LoadTrue:
			case Opcode.Return1:
			case Opcode.Close:
			case Opcode.Tbc:
				sb.Append($"{op} {A}");
				break;
			case Opcode.Test:
				sb.Append($"{op} {A} {K}");
				break;
			case Opcode.ExtraArg:
				sb.Append($"{op} {Ax}");
				break;
			case Opcode.Jmp:
				sb.Append($"{op} {SAx}");
				break;
			case Opcode.Move:
			case Opcode.LoadNil:
			case Opcode.Unm:
			case Opcode.BNot:
			case Opcode.Not:
			case Opcode.Len:
			case Opcode.Concat:
			case Opcode.Return:
				sb.Append($"{op} {A} {B}");
				break;
			case Opcode.Eq:
			case Opcode.Lt:
			case Opcode.Le:
			case Opcode.EqK:
			case Opcode.TestSet:
				sb.Append($"{op} {A} {B} {K}");
				break;
			case Opcode.EqI:
			case Opcode.LtI:
			case Opcode.LeI:
			case Opcode.GtI:
			case Opcode.GeI:
				sb.Append($"{op} {A} {SB} {K}");
				break;
			case Opcode.LoadF:
			case Opcode.LoadI:
				sb.Append($"{op} {A} {SBx}");
				break;
			case Opcode.ForLoop:
			case Opcode.ForPrep:
			case Opcode.TForPrep:
			case Opcode.LoadKx:
			case Opcode.TForLoop:
			case Opcode.Closure:
				sb.Append($"{op} {A} {Bx}");
				break;
			case Opcode.Vararg:
			case Opcode.TForCall:
				sb.Append($"{op} {A} {C}");
				break;
			case Opcode.AddI:
			case Opcode.ShrI:
			case Opcode.ShlI:
				sb.Append($"{op} {A} {B} {SC}");
				break;
			case Opcode.GetTable:
			case Opcode.GetI:
			case Opcode.Add:
			case Opcode.Sub:
			case Opcode.Mul:
			case Opcode.Mod:
			case Opcode.Pow:
			case Opcode.Div:
			case Opcode.IDiv:
			case Opcode.BAnd:
			case Opcode.BOr:
			case Opcode.BXor:
			case Opcode.Shl:
			case Opcode.Shr:
			case Opcode.Call:
				sb.Append($"{op} {A} {B} {C}");
				break;
			case Opcode.NewTable:
			case Opcode.TailCall:
			case Opcode.SetList:
				sb.Append($"{op} {A} {B} {K} {C}");
				break;
			case Opcode.LoadK: {
				sb.Append($"{op} {A} {Bx}");

				if (constants.Count > Bx) {
					sb.Append($" // {constants[(int) Bx].StripNewlines() ?? "<null>"}");
				}

				break;
			}
			case Opcode.GetUpval:
			case Opcode.SetUpval:
				sb.Append($"{op} {A} {B}");

				if (UpValueNames.Count > B) {
					sb.Append($" // {UpValueNames[(int) B]}");
				}

				break;
			case Opcode.GetTabUp: {
				sb.Append($"{op} {A} {B} {C}");

				if (constants.Count > C) {
					sb.Append($" // {UpValueNames.ElementAtOrDefault((int) B) ?? "<null>"}[{constants[(int) C].StripNewlines() ?? "<null>"}]");
				}

				break;
			}
			case Opcode.GetField: {
				sb.Append($"{op} {A} {B} {C}");

				if (constants.Count > C) {
					sb.Append($" // {constants[(int) C].StripNewlines()?.ToString() ?? "<null>"}");
				}

				break;
			}
			case Opcode.SetTabup:
			case Opcode.SetField: {
				sb.Append($"{op} {A} {B} {K} {C}");

				if (constants.Count > B) {
					sb.Append($" // \"{constants[(int) B].StripNewlines() ?? "<null>"}\"");
					if (K && constants.Count > C) {
						sb.Append(" = ");
						sb.Append(constants[(int) C].StripNewlines()?.ToString() ?? "<null>");
					}
				}

				break;
			}
			case Opcode.SetTable: {
				sb.Append($"{op} {A} {B} {K} {C}");

				if (K && constants.Count > C) {
					sb.Append($" // {constants[(int) C].StripNewlines()?.ToString() ?? "<null>"}");
				}

				break;
			}
			case Opcode.MmBin:
				sb.Append($"{op} {A} {B} {(MetaEvent) C}");
				break;
			case Opcode.MmBinI:
				sb.Append($"{op} {A} {SB} {K} {(MetaEvent) C}");
				break;
			case Opcode.MmBinK: {
				sb.Append($"{op} {A} {B} {K} {(MetaEvent) C}");

				if (constants.Count > B) {
					sb.Append($"{op} {A} {B} {K} {(MetaEvent) C} // {constants[(int) B].StripNewlines()?.ToString() ?? "<null>"}");
				}

				break;
			}
			case Opcode.SetI: {
				sb.Append($"{op} {A} {B} {K} {C}");

				if (K && constants.Count > C) {
					sb.Append($"{op} {A} {B} {K} {C} // {constants[(int) C].StripNewlines() ?? "<null>"}");
				}

				break;
			}
			case Opcode.Self: {
				sb.Append($"{op} {A} {B} {C}");

				if (K && constants.Count > C) {
					sb.Append($" // {constants[(int) C].StripNewlines()?.ToString() ?? "<null>"}");
				}

				break;
			}
			case Opcode.AddK:
			case Opcode.SubK:
			case Opcode.MulK:
			case Opcode.ModK:
			case Opcode.PowK:
			case Opcode.DivK:
			case Opcode.IDivK:
			case Opcode.BAndK:
			case Opcode.BOrK:
			case Opcode.BXorK: {
				sb.Append($"{op} {A} {B} {C}");

				if (constants.Count > C) {
					sb.Append($" // {constants[(int) C].StripNewlines()?.ToString() ?? "<null>"}");
				}

				break;
			}
			default:
				sb.Append($"{op} // A = {A}, B = {B}, C = {C}, K = {K}, Ax = {Ax}, Bx = {Bx}");
				PrintMembers(sb);
				break;
		}
	}
}
