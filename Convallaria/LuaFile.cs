namespace Convallaria;

public record LuaFile {
	public LuaFile(ILuaReader reader) {
		Header = reader.Read<Header>();
		Header.Validate();
		UpValueCount = reader.ReadByte();
		Entry = new Function(this, reader);
	}

	public Header Header { get; }
	public int UpValueCount { get; }
	public Function Entry { get; }
}
