using System;

namespace Convallaria;

public interface ILuaReader {
	public string? ReadString();
	public byte ReadByte();
	public T Read<T>() where T : struct;
	public ReadOnlyMemory<T> ReadArray<T>(int size) where T : struct;
	public ulong ReadULEB128();
	public ReadOnlyMemory<byte> Slice(int size);
}
