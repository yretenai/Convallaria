using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Convallaria;

public delegate List<object?> FunctionCall(object?[] args);

public record Function {
	public Function(LuaFile root, ILuaReader reader) {
		Root = root;
		Source = reader.ReadString() ?? "<unnamed>";
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

	public override string ToString() => $"Function for {Source} -> {Debug}";


#if !DEBUG
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	public void DumpInstructions(StringBuilder sb) {
		if (Source.Length > 0) {
			sb.AppendLine($"// function {Source}");
		}

		if (Upvalues.Length > 0) {
			sb.AppendLine();
			for (var i = 0; i < Upvalues.Length; ++i) {
				sb.AppendLine($"// upval {i}: {Upvalues.Span[i]} {Debug.UpValueNames.ElementAtOrDefault(i) ?? "unnamed"}");
			}
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
				case Opcode.SetI: {
					var c = instruction.K && Constants.Count > instruction.C ? Constants[(int) instruction.C] : null;
					sb.AppendLine($"{instruction.OP} {instruction.A} {instruction.B} {instruction.K} {instruction.C} // \"{c.StripNewlines()}\"");
					break;
				}
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

	private static double DoMath(MetaEvent method, object? lhs, object? rhs) {
		if (lhs is long && rhs is long) {
			return DoMathI(method, lhs, rhs);
		}

		var vL = lhs switch {
			         long vI => vI,
			         double vF => vF,
			         _ => 0.0d,
		         };
		var vR = rhs switch {
			         long vI => vI,
			         double vF => vF,
			         _ => 0.0d,
		         };

		return method switch {
			       MetaEvent.Add => vL + vR,
			       MetaEvent.Subtract => vL - vR,
			       MetaEvent.Multiply => vL * vR,
			       MetaEvent.Modulo => vL % vR,
			       MetaEvent.Power => Math.Pow(vL, vR),
			       MetaEvent.Divide => vL / vR,
			       MetaEvent.UnaryMinus => -vL,
			       _ => DoMathI(method, lhs, rhs),
		       };
	}

	private static double DoMathI(MetaEvent method, object? lhs, object? rhs) {
		var vL = lhs switch {
			         long vI => vI,
			         double vF => (long) vF,
			         _ => 0L,
		         };
		var vR = rhs switch {
			         long vI => vI,
			         double vF => (long) vF,
			         _ => 0L,
		         };
		return method switch {
			       MetaEvent.Add => vL + vR,
			       MetaEvent.Subtract => vL - vR,
			       MetaEvent.Multiply => vL * vR,
			       MetaEvent.Modulo => vL % vR,
			       MetaEvent.Power => Math.Pow(vL, vR),
			       // ReSharper disable twice PossibleLossOfFraction
			       MetaEvent.Divide => vL / vR,
			       MetaEvent.IntegerDivide => vL / vR,
			       MetaEvent.BitwiseAND => vL & vR,
			       MetaEvent.BitwiseOR => vL | vR,
			       MetaEvent.BitwiseXOR => vL ^ vR,
			       MetaEvent.ShiftLeft => vL << (int) vR,
			       MetaEvent.ShiftRight => vL >> (int) vR,
			       MetaEvent.UnaryMinus => -vL,
			       MetaEvent.BitwiseNOT => ~vL,
			       _ => 0,
		       };
	}

#if !DEBUG
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	// ReSharper disable once UnusedMethodReturnValue.Global
	public List<object?> RunVM(int pc, object?[] args, object?[] upvalues) {
		var R = new object?[MaxStackSize];
		var upvC = Math.Max(Upvalues.Length, Root.UpValueCount);
		object?[] upv;
		if (upvalues.Length < upvC) {
			upv = new object?[upvC];
			for (var i = 0; i < upvalues.Length; i++) {
				upv[i] = upvalues[i];
			}
		} else {
			upv = upvalues[..upvC];
		}

		for (; pc < Instructions.Length; pc++) {
			var inst = Instructions.Span[pc];
			switch (inst.Opcode) {
				case Opcode.Move: {
					R[inst.A] = R[inst.B];
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
				case Opcode.LoadK: {
					R[inst.A] = Constants[(int) inst.Bx];
					break;
				}
				case Opcode.LoadKx: {
					var next = Instructions.Span[++pc];
					R[inst.A] = Constants[(int) next.Ax];
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
				case Opcode.LoadTrue: {
					R[inst.A] = true;
					break;
				}
				case Opcode.LoadNil: {
					R[inst.A] = null;
					for (var i = 0; i < inst.B; i++) {
						R[inst.A + i + 1] = null;
					}

					break;
				}
				case Opcode.GetUpval: {
					R[inst.A] = upv[inst.B];
					break;
				}
				case Opcode.SetUpval: {
					upv[inst.B] = R[inst.A];
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
				case Opcode.GetTable: {
					var b = R[inst.B];
					if (b is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("get tab upval not on table");
					}

					var c = R[inst.C] ?? obj.Keys.LastOrDefault();
					R[inst.A] = c == null ? null : obj.GetValueOrDefault(c, null);
					break;
				}
				case Opcode.GetI: {
					var b = R[inst.B];
					var c = (long) inst.C;
					var v = b is not Dictionary<object, object?> obj ? null : obj.GetValueOrDefault(c, null);
					R[inst.A] = v;
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
				case Opcode.SetTabup: {
					var up = upv[inst.A];
					if (up is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("set tab upval not on table");
					}

					var b = Constants[(int) inst.B];
					if (b is not string) {
						throw new InvalidOperationException("set tab upval key is not string");
					}

					obj[b] = inst.K ? Constants[(int) inst.C] : R[inst.C];
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

					obj[(long) inst.B] = inst.K ? Constants[(int) inst.C] : R[inst.C];
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
				case Opcode.Self: { // is this correct?
					var key = inst.K ? Constants[(int) inst.C] : R[inst.C];
					if (key is null) {
						throw new InvalidOperationException("self null");
					}

					var self = R[inst.B];
					R[inst.A + 1] = self;
					switch (self) {
						case Dictionary<object, object?> obj: {
							R[inst.A] = obj.GetValueOrDefault(key, null);
							break;
						}
						case null: {
							break;
						}
						default: {
							var t = self.GetType();
							Console.Error.WriteLine($"unhandled self call {(t == typeof(Dictionary<object, object?>) ? "Table" : t.Name)}.{key}");
							break;
						}
					}

					break;
				}
				case Opcode.AddI:
					R[inst.A] = DoMath(MetaEvent.Add, R[inst.B], inst.SC);
					break;
				case Opcode.AddK:
					R[inst.A] = DoMath(MetaEvent.Add, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.SubK:
					R[inst.A] = DoMath(MetaEvent.Subtract, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.MulK:
					R[inst.A] = DoMath(MetaEvent.Multiply, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.ModK:
					R[inst.A] = DoMath(MetaEvent.Modulo, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.PowK:
					R[inst.A] = DoMath(MetaEvent.Power, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.DivK:
					R[inst.A] = DoMath(MetaEvent.Divide, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.IDivK:
					R[inst.A] = DoMath(MetaEvent.IntegerDivide, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.BAndK:
					R[inst.A] = DoMath(MetaEvent.BitwiseAND, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.BOrK:
					R[inst.A] = DoMath(MetaEvent.BitwiseOR, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.BXorK:
					R[inst.A] = DoMath(MetaEvent.BitwiseXOR, R[inst.B], Constants[(int) inst.C]);
					break;
				case Opcode.ShrI:
					R[inst.A] = DoMath(MetaEvent.ShiftRight, R[inst.B], inst.SC);
					break;
				case Opcode.ShlI:
					R[inst.A] = DoMath(MetaEvent.ShiftLeft, inst.SC, R[inst.B]);
					break;
				case Opcode.Add: {
					R[inst.A] = DoMath(MetaEvent.Add, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.Sub: {
					R[inst.A] = DoMath(MetaEvent.Subtract, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.Mul: {
					R[inst.A] = DoMath(MetaEvent.Multiply, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.Mod: {
					R[inst.A] = DoMath(MetaEvent.Modulo, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.Pow: {
					R[inst.A] = DoMath(MetaEvent.Power, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.Div: {
					R[inst.A] = DoMath(MetaEvent.Divide, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.IDiv: {
					R[inst.A] = DoMath(MetaEvent.IntegerDivide, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.BAnd: {
					R[inst.A] = DoMath(MetaEvent.BitwiseAND, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.BOr: {
					R[inst.A] = DoMath(MetaEvent.BitwiseOR, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.BXor: {
					R[inst.A] = DoMath(MetaEvent.BitwiseXOR, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.Shl: {
					R[inst.A] = DoMath(MetaEvent.ShiftLeft, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.Shr: {
					R[inst.A] = DoMath(MetaEvent.ShiftRight, R[inst.B], R[inst.C]);
					break;
				}
				case Opcode.MmBin: continue; // ??
				case Opcode.MmBinI: continue; // ??
				case Opcode.MmBinK: continue; // ??
				case Opcode.Unm:
					R[inst.A] = DoMath(MetaEvent.UnaryMinus, R[inst.B], null);
					break;
				case Opcode.BNot: {
					R[inst.A] = DoMath(MetaEvent.BitwiseNOT, R[inst.B], null);
					break;
				}
				case Opcode.Not: {
					R[inst.A] = R[inst.B] is false or null;
					break;
				}
				case Opcode.Len: {
					if (R[inst.B] is not Dictionary<object, object?> obj) {
						R[inst.A] = 0L;
					} else {
						R[inst.A] = (long) obj.Count;
					}

					break;
				}
				case Opcode.Concat: {
					var dict = new Dictionary<object, object?>();
					for (var i = 0L; i < inst.B; ++i) {
						dict[i + 1] = R[inst.A + i];
					}

					R[inst.A] = dict;
					break;
				}
				case Opcode.Close: continue;
				case Opcode.Tbc: continue;
				case Opcode.Jmp: {
					pc += inst.SAx;
					break;
				}
				case Opcode.Eq: {
					var pass = R[inst.A] == R[inst.B];
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
				case Opcode.EqK: {
					var pass = R[inst.A] == Constants[(int) inst.B];
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.EqI: {
					if (R[inst.A] is not long v) {
						v = 0;
					}

					var pass = v == inst.SB;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.LtI: {
					if (R[inst.A] is not long v) {
						v = 0;
					}

					var pass = v < inst.SB;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.LeI: {
					if (R[inst.A] is not long v) {
						v = 0;
					}

					var pass = v <= inst.SB;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.GtI: {
					if (R[inst.A] is not long v) {
						v = 0;
					}

					var pass = v > inst.SB;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.GeI: {
					if (R[inst.A] is not long v) {
						v = 0;
					}

					var pass = v >= inst.SB;
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.Test: {
					var pass = R[inst.A] is not (null or 0);
					if (pass != inst.K) {
						pc++;
					}

					break;
				}
				case Opcode.TestSet: {
					var pass = R[inst.B] is not (null or 0);
					if (pass != inst.K) {
						pc++;
					} else {
						R[inst.A] = R[inst.B];
					}

					break;
				}
				case Opcode.Call:
				case Opcode.TailCall: {
					var func = R[inst.A];
					if (func is not (FunctionCall or Closure)) {
						if (inst.Opcode is Opcode.Call) {
							for (var i = 0; i < inst.C; ++i) {
								R[inst.A + i] = new Dictionary<object, object?>();
							}
						} else {
							var returnResult = new List<object?>();
							for (var i = 0; i < inst.C; ++i) {
								returnResult.Add(new Dictionary<object, object?>());
							}

							return returnResult;
						}

						break;
					}

					object?[] callArgs;
					if (inst.B > 1) {
						callArgs = new object?[inst.B - 1];
						for (var i = 0; i < inst.B - 1; ++i) {
							callArgs[i] = R[inst.A + i + 1];
						}
					} else {
						callArgs = [];
					}

					var result = func switch {
						             FunctionCall call => call(callArgs),
						             Closure closure => closure.Execute(0, callArgs),
						             _ => throw new UnreachableException(),
					             };

					if (inst.Opcode is Opcode.TailCall) {
						return result;
					}

					for (var i = 0; i < inst.C; ++i) {
						R[inst.A + i] = i < result.Count ? result[i] : new Dictionary<object, object?>();
					}

					break;
				}
				case Opcode.Return: {
					var start = (int) inst.A;
					var end = (int) (inst.A + inst.B) - 2;

					if (start == end || end < 0) {
						return [R[start]];
					}

					if (start > end) {
						(start, end) = (end, start);
					}

					return [..R[start.. end]];
				}
				case Opcode.Return0: return [];
				case Opcode.Return1: return [R[inst.A]];
				case Opcode.ForLoop: {
					var c = R[inst.A] switch {
						        long vI => vI > 0,
						        double vF => vF > 0.0,
						        _ => false,
					        };

					if (c) {
						pc -= (int) inst.Bx;
						pc += 1;
					} else {
						R[inst.A] = R[inst.A] switch {
							            long vI => vI - 1,
							            double vF => vF - 1,
							            _ => 0,
						            };
					}

					break;
				}
				case Opcode.ForPrep: {
					var c = R[inst.A] switch {
						        long vI => vI > 0,
						        double vF => vF > 0.0,
						        _ => false,
					        };

					if (!c) {
						pc += (int) inst.Bx;
					}

					break;
				}
				case Opcode.TForPrep: throw new NotImplementedException("Opcode TForPrep not implemented");
				case Opcode.TForCall: throw new NotImplementedException("Opcode TForCall not implemented");
				case Opcode.TForLoop: throw new NotImplementedException("Opcode TForLoop not implemented");
				case Opcode.SetList: {
					var n = inst.B;
					var offset = inst.C;
					if (inst.K) {
						offset += Instructions.Span[++pc].Ax * 256;
					}

					if (n == 0) {
						break;
					}

					if (R[inst.A] is not Dictionary<object, object?> obj) {
						throw new InvalidOperationException("set list not on table");
					}

					for (var i = 1L; i <= n; ++i) {
						obj[offset + i] = R[inst.A + i];
					}

					break;
				}
				case Opcode.Closure: {
					var proto = Protos[(int) inst.Bx];
					var protoUpV = new object?[proto.Upvalues.Length];
					for (var i = 0; i < protoUpV.Length; ++i) {
						var up = proto.Upvalues.Span[i];
						protoUpV[i] = up.InStack ? R[up.Index] : upv[up.Index];
					}

					R[inst.A] = new Closure(protoUpV, proto);
					break;
				}
				case Opcode.Vararg: throw new NotImplementedException("Opcode Vararg not implemented");
				case Opcode.VarargPrep: continue; // pass
				case Opcode.ExtraArg: continue; // pass
				default: throw new InvalidOperationException("invalid opcode");
			}
		}

		return [];
	}

	public static List<object?> NullCall(object?[] args) => [];
}
