namespace Convallaria;

public record struct LocVariable {
	public string VarName { get; set; }
	public ulong StartCounter { get; set; }
	public ulong EndCounter { get; set; }

	public override string ToString() => $"\"{VarName.StripNewlines()}\" ({StartCounter} to {EndCounter})";
}
