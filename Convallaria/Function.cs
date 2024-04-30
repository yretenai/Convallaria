using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Convallaria;

public record Function {
	public Function(LuaFile root, ILuaReader reader) {
		Root = root;
		Source = reader.ReadString() ?? "";
		LineDefined = reader.ReadULEB128();
		LastLineDefined = reader.ReadULEB128();
		NumParams = reader.ReadByte();
		IsVarArg = reader.ReadByte();
		MaxStackSize = reader.ReadByte();
		Instructions = reader.ReadArray<Instruction>((int) reader.ReadULEB128());

		var constantSize = (int) reader.ReadULEB128();
		Constants.EnsureCapacity(constantSize);
		for (var i = 0; i < constantSize; ++i) {
			var type = reader.Read<LuaValueType>();

			switch (type) {
				case LuaValueType.Null:
					Constants.Add(null);
					break;
				case LuaValueType.False:
					Constants.Add(false);
					break;
				case LuaValueType.True:
					Constants.Add(true);
					break;
				case LuaValueType.Float:
					Constants.Add(Root.Header.NumberSize == 4 ? reader.Read<float>() : reader.Read<double>());
					break;
				case LuaValueType.Int:
					Constants.Add(Root.Header.IntSize == 4 ? reader.Read<int>() : reader.Read<long>());
					break;
				case LuaValueType.ShortString:
				case LuaValueType.LongString:
					Constants.Add(reader.ReadString());
					break;
			}
		}

		var upValSize = (int) reader.ReadULEB128();
		Upvalues = reader.ReadArray<UpValueInfo>(upValSize);

		var funcSize = (int) reader.ReadULEB128();
		Protos.EnsureCapacity(funcSize);
		for (var i = 0; i < funcSize; ++i) {
			Protos.Add(new Function(Root, reader));
		}

		Debug = new DebugInfo(reader);
	}

	public string Source { get; }
	public ulong LineDefined { get; }
	public ulong LastLineDefined { get; }
	public byte NumParams { get; }
	public byte IsVarArg { get; }
	public byte MaxStackSize { get; }
	public ReadOnlyMemory<Instruction> Instructions { get; }
	public List<object?> Constants { get; } = [];
	public ReadOnlyMemory<UpValueInfo> Upvalues { get; }
	public List<Function> Protos { get; } = [];
	public DebugInfo Debug { get; }
	public LuaFile Root { get; }

#if !DEBUG
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	public void DumpInstructions(StringBuilder sb) {
		if (Source.Length > 0) {
			sb.AppendLine($"// function {Source}");
		}

		sb.AppendLine();

		foreach (var instruction in Instructions.Span) {
			switch (instruction.Opcode) {
				case Opcode.VarargPrep:
				case Opcode.Return0:
					sb.AppendLine($"{instruction.OP}");
					break;
				case Opcode.LoadFalse:
				case Opcode.LFalseSkip:
				case Opcode.LoadTrue:
				case Opcode.Return1:
				case Opcode.Close:
				case Opcode.Tbc:
					sb.AppendLine($"{instruction.OP} {instruction.A}");
					break;
				case Opcode.Test:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.K}");
					break;
				case Opcode.ExtraArg:
					sb.AppendLine($"{instruction.OP} {instruction.Ax}");
					break;
				case Opcode.Jmp:
					sb.AppendLine($"{instruction.OP} {instruction.SAx}");
					break;
				case Opcode.Move:
				case Opcode.LoadNil:
				case Opcode.Unm:
				case Opcode.BNot:
				case Opcode.Not:
				case Opcode.Len:
				case Opcode.Concat:
				case Opcode.Return:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B}");
					break;
				case Opcode.Eq:
				case Opcode.Lt:
				case Opcode.Le:
				case Opcode.EqK:
				case Opcode.TestSet:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.K}");
					break;
				case Opcode.EqI:
				case Opcode.LtI:
				case Opcode.LeI:
				case Opcode.GtI:
				case Opcode.GeI:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.SB} {instruction.K}");
					break;
				case Opcode.LoadF:
				case Opcode.LoadI:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.SBx}");
					break;
				case Opcode.ForLoop:
				case Opcode.ForPrep:
				case Opcode.TForPrep:
				case Opcode.LoadKx:
				case Opcode.TForLoop:
				case Opcode.Closure:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.Bx}");
					break;
				case Opcode.Vararg:
				case Opcode.TForCall:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.C}");
					break;
				case Opcode.AddI:
				case Opcode.ShrI:
				case Opcode.ShlI:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.SC}");
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
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.C}");
					break;
				case Opcode.NewTable:
				case Opcode.TailCall:
				case Opcode.SetList:
					// A = R[A], B = log2(hash) + 1, C = size, K = use extra arg
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.K} {instruction.C}");
					break;
				case Opcode.LoadK: {
					var b = Constants.Count > instruction.Bx ? Constants[(int) instruction.Bx] : null;
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.Bx} // {b.StripNewlines()}");
					break;
				}
				case Opcode.GetUpval:
				case Opcode.SetUpval:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} // {Debug.UpValueNames.ElementAtOrDefault((int) instruction.B) ?? "unknown"}");
					break;
				case Opcode.GetTabUp: {
					var b = Constants.Count > instruction.C ? Constants[(int) instruction.C] : "<unknown>";
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.C} // {Debug.UpValueNames.ElementAtOrDefault((int) instruction.B) ?? "unknown"}[{b.StripNewlines()}]");
					break;
				}
				case Opcode.GetField: {
					var b = Constants.Count > instruction.C ? Constants[(int) instruction.C] : null;
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.C} // {b.StripNewlines()?.ToString() ?? "<null>"}");
					break;
				}
				case Opcode.SetTabup: {
					var b = Constants.Count > instruction.B ? Constants[(int) instruction.B] : null;
					b ??= "<null>";
					var c = instruction.K && Constants.Count > instruction.C ? Constants[(int) instruction.C] : null;

					sb.Append($"{instruction.OP} {instruction.A} {instruction.B} {instruction.K} {instruction.C} // \"{b.StripNewlines()}\"");

					if (instruction.K) {
						sb.Append(" = ");
						sb.AppendLine(c.StripNewlines()?.ToString() ?? "<null>");
					} else {
						sb.AppendLine();
					}

					break;
				}
				case Opcode.SetTable: {
					var c = instruction.K && Constants.Count > instruction.C ? Constants[(int) instruction.C] : null;

					sb.Append($"{instruction.OP} {instruction.A} {instruction.B} {instruction.K} {instruction.C}");

					if (instruction.K) {
						sb.Append(" // ");
						sb.AppendLine(c.StripNewlines()?.ToString() ?? "<null>");
					} else {
						sb.AppendLine();
					}

					break;
				}
				case Opcode.MmBin:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {(MetaEvent) instruction.C}");
					break;
				case Opcode.MmBinI:
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.SB} {instruction.K} {(MetaEvent) instruction.C}");
					break;
				case Opcode.MmBinK: {
					var b = Constants.Count > instruction.B ? Constants[(int) instruction.B] : null;
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.K} {(MetaEvent) instruction.C} // {b.StripNewlines()?.ToString() ?? "<null>"}");

					break;
				}
				case Opcode.SetI:
				case Opcode.SetField: {
					var b = Constants.Count > instruction.B ? Constants[(int) instruction.B] : null;
					var c = instruction.K && Constants.Count > instruction.C ? Constants[(int) instruction.C] : null;

					b ??= "<null>";

					sb.Append($"{instruction.OP} {instruction.A} {instruction.B} {instruction.K} {instruction.C} // \"{b.StripNewlines()}\"");

					if (instruction.K) {
						sb.Append(" = ");
						sb.AppendLine(c.StripNewlines()?.ToString() ?? "<null>");
					} else {
						sb.AppendLine();
					}

					break;
				}
				case Opcode.Self: {
					var c = instruction.K && Constants.Count > instruction.C ? Constants[(int) instruction.C] : null;

					sb.Append($"{instruction.OP} {instruction.A} {instruction.B} {instruction.C}");

					if (instruction.K) {
						sb.Append(" // ");
						sb.AppendLine(c.StripNewlines()?.ToString() ?? "<null>");
					} else {
						sb.AppendLine();
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
					var c = Constants.Count > instruction.C ? Constants[(int) instruction.C] : null;
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.C} // {c.StripNewlines() ?? 0}");
					break;
				}
				default:
					sb.AppendLine($"{instruction.OP} // {instruction}");
					break;
			}
		}

		if (Protos.Count > 0) {
			sb.AppendLine();
			sb.AppendLine("// begin nested meta");
			sb.AppendLine();

			for (var index = 0; index < Protos.Count; index++) {
				var proto = Protos[index];
				sb.AppendLine($"// proto: {index}");
				proto.DumpInstructions(sb);
				sb.AppendLine();
			}

			sb.AppendLine("// end nested meta");
		}
	}

#if !DEBUG
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	// ReSharper disable once UnusedMethodReturnValue.Global
	public object? RunVM(int pc, params object[] upvalues) {
		var R = new object?[MaxStackSize];
		var upv = new object?[Math.Max(Upvalues.Length, Root.UpValueCount)];
		for (var i = 0; i < Math.Min(upv.Length, upvalues.Length); i++) {
			upv[i] = upvalues[i];
		}

		for (; pc < Instructions.Length; pc++) {
			var inst = Instructions.Span[pc];
			switch (inst.Opcode) {
				case Opcode.Move: {
					R[inst.A] = R[inst.B];
					break;
				}
				case Opcode.LoadK: {
					R[inst.A] = Constants[(int) inst.Bx];
					break;
				}
				case Opcode.LoadKx: {
					var next = Instructions.Span[++pc];
					R[inst.A] = Constants[(int) next.Ax];
					break;
				}
				case Opcode.LoadI: {
					R[inst.A] = (long) inst.SBx;
					break;
				}
				case Opcode.LoadF: {
					R[inst.A] = (double) inst.SBx;
					break;
				}
				case Opcode.LoadTrue: {
					R[inst.A] = true;
					break;
				}
				case Opcode.LoadFalse: {
					R[inst.A] = false;
					break;
				}
				case Opcode.LFalseSkip: {
					R[inst.A] = false;
					pc++;
					break;
				}
				case Opcode.LoadNil: {
					R[inst.A] = null;
					break;
				}
				case Opcode.Jmp: {
					pc += inst.SAx;
					break;
				}
				case Opcode.Test: {
					var pass = R[inst.A] is true or 1 or 1u or 1.0 or 1.0d;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.Lt: {
					if (R[inst.A] is not long l) {
						l = 0;
					}
					if (R[inst.B] is not long r) {
						r = 0;
					}
					var pass = l < r;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.Le: {
					if (R[inst.A] is not long l) {
						l = 0;
					}
					if (R[inst.B] is not long r) {
						r = 0;
					}
					var pass = l <= r;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.Eq: {
					var pass = R[inst.A] == R[inst.B];
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.EqK: {
					var pass = R[inst.A] == Constants[(int) inst.B];
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.TestSet: {
					var pass = R[inst.B] is true or 1 or 1u or 1.0 or 1.0d;
					if (pass != inst.K) {
						pc++;
					} else {
						R[inst.A] = R[inst.B];
					}

					break;
				}
				case Opcode.GetTabUp: {
					var up = upv[inst.B];
					if (up is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("get tab upval not on table");
					}

					var k = Constants[(int) inst.C];
					if (k is not string) {
						throw new InvalidOperationException("get tab upval key is not string");
					}

					R[inst.A] = obj.GetValueOrDefault(k, null);
					break;
				}
				case Opcode.GetUpval: {
					R[inst.A] = upv[inst.B];
					break;
				}
				case Opcode.GetField: {
					var b = R[inst.B];
					var c = Constants[(int) inst.C];
					object? v;
					if (c is null || b is not Dictionary<object, object?> obj) {
						v = null;
					} else {
						v = obj.GetValueOrDefault(c, null);
					}

					R[inst.A] = v;
					break;
				}
				case Opcode.GetTable: {
					var b = R[inst.B];
					if (b is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("get tab upval not on table");
					}

					var c = R[inst.C];
					if (c == null) {
						throw new InvalidOperationException("null key on table");
					}

					R[inst.A] = obj.GetValueOrDefault(c, null);
					break;
				}
				case Opcode.SetTable: {
					if (R[inst.A] is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("set field not on table");
					}

					var b = R[inst.B];
					if (b is null) {
						throw new NullReferenceException("key is null");
					}

					obj[b] = inst.K ? Constants[(int) inst.C] : R[inst.C];
					break;
				}
				case Opcode.SetI: {
					if (R[inst.A] is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("set field not on table");
					}

					obj[inst.B] = inst.K ? Constants[(int) inst.C] : R[inst.C];
					break;
				}
				case Opcode.SetField: {
					if (R[inst.A] is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("set field not on table");
					}

					var b = Constants[(int) inst.B];
					if (b is null) {
						throw new NullReferenceException("key is null");
					}

					obj[b] = inst.K ? Constants[(int) inst.C] : R[inst.C];
					break;
				}
				case Opcode.Self: { // is this correct?
					var key = inst.K ? Constants[(int) inst.C] : R[inst.C];
					if (key is null) {
						throw new InvalidOperationException("self null");
					}

					R[inst.A + 1] = R[inst.B];
					if (R[inst.B] is Dictionary<object, object?> obj) {
						R[inst.A] = obj.GetValueOrDefault(key, null);
					}
					break;
				}
				case Opcode.SetList: {
					var n = inst.B;
					var offset = inst.C;
					if (inst.K) {
						offset += Instructions.Span[++pc].Ax * 256;
					}

					if (n == 0) { }

					if (R[inst.A] is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("set list not on table");
					}

					for (var i = 1L; i <= n; ++i) {
						obj[inst.C + offset + i] = R[inst.A + i];
					}

					break;
				}
				case Opcode.NewTable: {
					var size = inst.C;
					pc++;
					if (inst.K) {
						size += Instructions.Span[pc].Ax * 256;
					}

					var dict = new Dictionary<object, object?>();
					for (var i = 0L; i < size; ++i) {
						dict[i + 1] = null;
					}

					R[inst.A] = dict;
					break;
				}
				case Opcode.VarargPrep:
					// pass
					break;
				case Opcode.ExtraArg:
					// pass
					break;
				case Opcode.Return: {
					var start = (int) inst.A;
					var end = (int) (inst.A + inst.B) - 2;

					if (start == end || end < 0) {
						return R[start];
					}

					if (start > end) {
						(start, end) = (end, start);
					}

					return R[start.. end];
				}
				case Opcode.Closure:
					// todo
					R[inst.A] = new Dictionary<object, object?>();
					break;
				case Opcode.Call:
					// todo
					R[inst.A] = new Dictionary<object, object?>();
					for (var i = 1; i <= inst.C; ++i) {
						R[inst.A + i] = new Dictionary<object, object?>();
					}
					break;
				case Opcode.Close:
					// skip
					break;
				case Opcode.Return0:
					return null;
				case Opcode.Return1:
					return R[inst.A];
				default:
					throw new NotImplementedException($"{inst.OP} not implemented");
			}
		}

		return null;
	}
}
