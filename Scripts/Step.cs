using Godot;
using System.Collections.Generic;

// Nincs járás-spritesheet, ezért kódból lépünk: a sprite-ok bólintanak és súlyt váltanak.
// ponytail: három helyen kell (player, Brendon, táncosok), ezért közös. Forgatás helyett
// oldalirányú eltolás, mert a fej külön Sprite2D és a saját közepe körül csúnyán fordulna.
public static class Step
{
	public const float Height = 2.5f;   // px, a bólintás magassága
	public const float Sway = 1.0f;     // px, oldalra billenés

	// A node Sprite2D gyerekei a kiinduló pozíciójukkal. (A Label nem mozog.)
	public static Dictionary<Sprite2D, Vector2> Parts(Node node)
	{
		Dictionary<Sprite2D, Vector2> parts = new();

		foreach (Node child in node.GetChildren())
			if (child is Sprite2D sprite)
				parts[sprite] = sprite.Position;

		return parts;
	}

	// time: felhalmozott lépésidő radiánban. amount: 0 = áll, 1 = normál lépés.
	public static void Apply(Dictionary<Sprite2D, Vector2> parts, float time, float amount)
	{
		float s = Mathf.Sin(time) * amount;
		Vector2 offset = new(s * Sway, -Mathf.Abs(s) * Height);

		foreach (KeyValuePair<Sprite2D, Vector2> part in parts)
			part.Key.Position = part.Value + offset;
	}
}
