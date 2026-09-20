using Godot;
using System.Collections.Generic;

// Nappal/éjszaka váltás az N gombbal ("night" akció).
// A tile-ok custom data-ja dönti el, mi világít, így az editorban festett új lámpa is működik:
//   "light" (Color, alfa = a fény mérete) -> PointLight2D a tile-nál
//   "glow"  (Vector2i atlas koordináta)   -> világító másolat az Emissive rétegen
public partial class DayNight : CanvasModulate
{
	[Export]
	public TileMapLayer Map { get; set; }

	[Export]
	public TileMapLayer Props { get; set; }

	[Export]
	public TileMapLayer Emissive { get; set; }

	[Export]
	public Texture2D LightTexture { get; set; }

	[Export]
	public Color NightColor { get; set; } = new Color(0.26f, 0.28f, 0.46f);

	[Export]
	public float FadeTime { get; set; } = 1.5f;

	[Export]
	public bool StartAtNight { get; set; } = false;

	private readonly List<PointLight2D> _lights = new();
	private bool _isNight;
	private float _amount;
	private Tween _tween;

	public override void _Ready()
	{
		foreach (TileMapLayer layer in new[] { Map, Props })
		{
			foreach (Vector2I cell in layer.GetUsedCells())
			{
				TileData data = layer.GetCellTileData(cell);

				Vector2I glow = data.GetCustomData("glow").AsVector2I();
				if (glow != Vector2I.Zero)
					Emissive.SetCell(cell, 0, glow);

				Color light = data.GetCustomData("light").AsColor();
				if (light.R + light.G + light.B > 0)
					AddLight(layer.MapToLocal(cell) + new Vector2(0, 8), light);
			}
		}

		foreach (Node node in GetTree().GetNodesInGroup("night_lights"))
		{
			if (node is PointLight2D light)
				_lights.Add(light);
		}

		_isNight = StartAtNight;
		SetNight(_isNight ? 1.0f : 0.0f);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("night"))
			return;

		_isNight = !_isNight;
		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenMethod(Callable.From<float>(SetNight), _amount, _isNight ? 1.0f : 0.0f, FadeTime);
	}

	private void AddLight(Vector2 position, Color light)
	{
		Color color = new Color(light, 1.0f);

		// ponytail: O(n²) összevonás (egymás melletti neon/kirakat tile-ok = 1 fény), pár száz fénynél bőven elég
		foreach (PointLight2D other in _lights)
		{
			if (other.Color == color && other.Position.DistanceTo(position) < 96)
				return;
		}

		var pointLight = new PointLight2D
		{
			Position = position,
			Texture = LightTexture,
			Color = color,
			TextureScale = light.A,
		};
		AddChild(pointLight);
		_lights.Add(pointLight);
	}

	private void SetNight(float amount)
	{
		_amount = amount;
		Color = Colors.White.Lerp(NightColor, amount);
		Emissive.Modulate = new Color(1, 1, 1, amount);

		foreach (PointLight2D light in _lights)
		{
			light.Energy = amount;
			light.Visible = amount > 0;
		}
	}
}
