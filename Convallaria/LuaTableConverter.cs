using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Convallaria;

public class LuaTableSerializer : JsonConverter<Table> {
	public override Table Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		var dict = new Table();
		switch (reader.TokenType) {
			case JsonTokenType.StartArray: {
				reader.Read();
				while (reader.TokenType != JsonTokenType.EndArray) {
					dict[(long) (dict.Count + 1)] = JsonSerializer.Deserialize<object?>(ref reader, options);
					reader.Read();
				}

				break;
			}
			case JsonTokenType.StartObject: {
				reader.Read();
				while (reader.TokenType != JsonTokenType.EndObject) {
					var key = reader.GetString();
					if (key == null) {
						throw new InvalidOperationException("needs string");
					}

					reader.Read();

					dict[key] = JsonSerializer.Deserialize<object?>(ref reader, options);
					reader.Read();
				}

				break;
			}
			case JsonTokenType.Null:
				return [];
			default:
				throw new InvalidOperationException("invalid type");
		}

		return dict;
	}

	public override void Write(Utf8JsonWriter writer, Table value, JsonSerializerOptions options) {
		var keys = value.Keys.ToArray();
		var isLinearArray = value.Count > 0 && keys[0].Equals(1L) && keys[^1].Equals((long) value.Count) && keys.Distinct().Count() == keys.Length;
		if (isLinearArray) {
			JsonSerializer.Serialize(writer, value.Values.ToArray(), options);
		} else {
			writer.WriteStartObject();
			foreach (var (key, v) in value) {
				writer.WritePropertyName(key.ToString()!);
				JsonSerializer.Serialize(writer, v, options);
			}

			writer.WriteEndObject();
		}
	}
}
