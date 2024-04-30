namespace Convallaria;

public enum LuaValueType : byte {
	Variant1 = 1 << 4,
	Variant2 = 2 << 4,

	Null = 0,
	Empty = Null | Variant1,
	AbsentKey = Null | Variant2,
	False = 1,
	True = False | Variant1,
	LightUserData = 2,
	Int = 3,
	Float = Int | Variant1,
	ShortString = 4,
	LongString = ShortString | Variant1,
	Table = 5,
	Closure = 6,
	CFunc = 6 | Variant1,
	CClosure = 6 | Variant2,
	UserData = 7,
	Thread = 8,
	Upval = 9,
	Proto = 10,
	Deadkey = 11,
}
