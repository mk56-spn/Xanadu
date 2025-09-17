using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using System;

namespace XanaduProject.IO
{
    // Custom JsonConverter for Godot.Color
    public class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected string for Color deserialization.");
            }

            string? colorString = reader.GetString();
            if (string.IsNullOrEmpty(colorString))
            {
                // Return a default color, e.g., transparent black, or throw an exception
                return new Color(0, 0, 0, 0);
            }

            string[] components = colorString.Split(',');
            if (components.Length != 4)
            {
                throw new JsonException("Expected Color string in format 'R,G,B,A'.");
            }

            if (byte.TryParse(components[0], out byte r) &&
                byte.TryParse(components[1], out byte g) &&
                byte.TryParse(components[2], out byte b) &&
                byte.TryParse(components[3], out byte a))
            {
                return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            }
            else
            {
                throw new JsonException("Failed to parse Color components as bytes.");
            }
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            string colorString = $"{value.R8},{value.G8},{value.B8},{value.A8}";
            writer.WriteStringValue(colorString);
        }
    }
}
