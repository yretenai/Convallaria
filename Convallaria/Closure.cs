using System.Collections.Generic;

namespace Convallaria;

public record Closure(object?[] UpValues, Function Proto) {
	public List<object?> Execute(int pc, object?[] args) {
		return Proto.RunVM(pc, args, UpValues);
	}

	public override string ToString() => $"Closure for {Proto}";
}
