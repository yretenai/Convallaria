using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Convallaria;

public class LuaReader(ReadOnlyMemory<byte> Data) : ILuaReader {
	public ReadOnlyMemory<byte> Data { get; } = Data;
	public int Offset { get; set; }

#if !DEBUG
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	public virtual string? ReadString() {
		var size = (int) ReadULEB128();
		switch (size) {
			case < 0:
				throw new UnreachableException();
			case <= 1:
				return null;
		}

		size -= 1;
		var value = Encoding.UTF8.GetString(Data.Span.Slice(Offset, size));
		Offset += size;
		return value;
	}

	public virtual byte ReadByte() {
		return Data.Span[Offset++];
	}

	public virtual T Read<T>() where T : struct {
		var value = MemoryMarshal.Read<T>(Data.Span[Offset..]);
		Offset += Unsafe.SizeOf<T>();
		return value;
	}

#if !DEBUG
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	public virtual ReadOnlyMemory<T> ReadArray<T>(int size) where T : struct {
		size *= Unsafe.SizeOf<T>();
		if (size == 0) {
			return ReadOnlyMemory<T>.Empty;
		}

		var value = MemoryMarshal.Cast<byte, T>(Data.Span.Slice(Offset, size));
		Offset += size;
		return value.ToArray();
	}

#if !DEBUG
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
	public virtual ulong ReadULEB128() {
		var result = 0ul;
		byte lastByte;
		do {
			lastByte = ReadByte();
			result = ((ulong) lastByte & 0x7F) | (result << 7);
		} while (lastByte < 0x80);

		return result;
	}

	public virtual ReadOnlyMemory<byte> Slice(int size) {
		var slice = Data.Slice(Offset, size);
		Offset += size;
		return slice;
	}
}
