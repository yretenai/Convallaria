using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
#if !DEBUG
using System.Runtime.CompilerServices;
#endif

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
					Constants.Add(reader.Read<double>());
					break;
				case LuaValueType.Int:
					Constants.Add(reader.Read<long>());
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
	public string? Name { get; set; }
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
		for (var index = 0; index < Instructions.Span.Length - 1; index++) {
			var inst = Instructions.Span[index];
			if (inst.Opcode is Opcode.Closure) {
				var proto = Protos[(int) inst.Bx];
				var next = Instructions.Span[index + 1];
				switch (next.Opcode) {
					case Opcode.SetField:
					case Opcode.SetTabup:
						if (Constants.Count > next.B) {
							proto.Name = Constants[(int) next.B]?.StripNewlines() as string;
						}

						break;
				}
			}
		}

		if (Name?.Length > 0) {
			sb.AppendLine($"// function {Name}");
		}

		if (Source.Length > 0) {
			sb.AppendLine($"// source {Source}");
		}

		if (Upvalues.Length > 0) {
			sb.AppendLine();
			for (var i = 0; i < Upvalues.Length; ++i) {
				sb.AppendLine($"// upval {i}: {Upvalues.Span[i]} {Debug.UpValueNames.ElementAtOrDefault(i) ?? "unnamed"}");
			}
		}

		if (Debug.LocVarInfo.Count > 0) {
			sb.AppendLine();
			for (var i = 0; i < Debug.LocVarInfo.Count; ++i) {
				sb.AppendLine($"// var {i}: {Debug.LocVarInfo[i]}");
			}
		}

		sb.AppendLine();

		foreach (var instruction in Instructions.Span) {
			instruction.Dump(sb, Constants, Debug.UpValueNames);
			sb.AppendLine();
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

		for (var i = 0; i < Math.Min(args.Length, NumParams); ++i) {
			R[i] = args[i];
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
					if (up is not Table obj) {
						if (up == null) {
							obj = new Table();
							upv[inst.B] = obj;
						} else {
							throw new InvalidOperationException("get tab upval not on table");
						}
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
					if (b is not Table obj) {
						if (b == null) {
							obj = new Table();
							R[inst.B] = obj;
						} else {
							throw new InvalidOperationException("get tab upval not on table");
						}
					}

					var c = R[inst.C] ?? obj.Keys.LastOrDefault();
					R[inst.A] = c == null ? null : obj.GetValueOrDefault(c, null);
					break;
				}
				case Opcode.GetI: {
					var b = R[inst.B];
					var c = (long) inst.C;
					var v = b is not Table obj ? null : obj.GetValueOrDefault(c, null);
					R[inst.A] = v;
					break;
				}
				case Opcode.GetField: {
					var b = R[inst.B];
					var c = Constants[(int) inst.C];
					object? v;
					if (c is null || b is not Table obj) {
						v = null;
					} else {
						v = obj.GetValueOrDefault(c, null);
					}

					R[inst.A] = v;
					break;
				}
				case Opcode.SetTabup: {
					var up = upv[inst.A];
					if (up is not Table obj) {
						if (up == null) {
							obj = new Table();
							upv[inst.A] = obj;
						} else {
							throw new InvalidOperationException("set tab upval not on table");
						}
					}

					var b = Constants[(int) inst.B];
					if (b is not string) {
						throw new InvalidOperationException("set tab upval key is not string");
					}

					obj[b] = inst.K ? Constants[(int) inst.C] : R[inst.C];
					break;
				}
				case Opcode.SetTable: {
					var ob = R[inst.A];
					if (ob is not Table obj) {
						if (ob == null) {
							obj = new Table();
							R[inst.A] = obj;
						} else {
							throw new InvalidOperationException("set field not on table");
						}
					}

					var b = R[inst.B];
					if (b is null) {
						throw new NullReferenceException("key is null");
					}

					obj[b] = inst.K ? Constants[(int) inst.C] : R[inst.C];
					break;
				}
				case Opcode.SetI: {
					var ob = R[inst.A];
					if (ob is not Table obj) {
						if (ob == null) {
							obj = new Table();
							R[inst.A] = obj;
						} else {
							throw new InvalidOperationException("set field not on table");
						}
					}

					obj[(long) inst.B] = inst.K ? Constants[(int) inst.C] : R[inst.C];
					break;
				}
				case Opcode.SetField: {
					var ob = R[inst.A];
					if (ob is not Table obj) {
						if (ob == null) {
							obj = new Table();
							R[inst.A] = obj;
						} else {
							throw new InvalidOperationException("set field not on table");
						}
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

					var dict = new Table();
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
						case Table obj: {
							R[inst.A] = obj.GetValueOrDefault(key, null);
							break;
						}
						case null: {
							break;
						}
						default: {
							var t = self.GetType();
							Console.Error.WriteLine($"unhandled self call {(t == typeof(Table) ? "Table" : t.Name)}.{key}");
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
					if (R[inst.B] is not Table obj) {
						R[inst.A] = 0L;
					} else {
						R[inst.A] = (long) obj.Count;
					}

					break;
				}
				case Opcode.Concat: {
					var dict = new Table();
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
								R[inst.A + i] = new Table();
							}
						} else {
							var returnResult = new List<object?>();
							for (var i = 0; i < inst.C; ++i) {
								returnResult.Add(new Table());
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
						R[inst.A + i] = i < result.Count ? result[i] : new Table();
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

					var ob = R[inst.A];
					if (ob is not Table obj) {
						if (ob == null) {
							obj = new Table();
							R[inst.A] = obj;
						} else {
							throw new InvalidOperationException("set list not on table");
						}
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
