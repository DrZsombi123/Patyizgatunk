using Godot;

// Győzelmi oldal: elértük a Player.WinAura-t -> buli a klubban, mindenki táncol.
// ponytail: a klub egy sötét háttér villogó fénnyel, nem a World klubjának másolata.
// Ha kell a valódi belső tér, ide jöhet a klub tile-ja és a táncosok pozíciói.
public partial class Victory : Control
{
	private const float Zoom = 3.0f;

	// törzs, fej, haj (üres = nincs külön)
	private static readonly string[][] Crowd =
	{
		new[] { "tancos1", "", "" },
		new[] { "brendon", "head_brendon", "" },
		new[] { "dj", "", "" },
		new[] { "tancos2", "", "" },
		new[] { "player", "head_player", "hair_good" },
		new[] { "tancos3", "", "" },
		new[] { "ferike", "head_ferike", "" },
		new[] { "csapos", "", "" },
		new[] { "kukabuvar", "", "" },
	};

	public override void _Ready()
	{
		SaveGame.Delete();   // megnyertük, a következő kör elölről indul
		SetAnchorsPreset(LayoutPreset.FullRect);

		var background = new ColorRect { Color = new Color(0.06f, 0.0f, 0.1f) };
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		// diszkófény: a háttér színe körbe-körbe vált
		Tween lights = CreateTween().SetLoops();
		foreach (Color color in new[] { new Color(0.25f, 0, 0.3f), new Color(0, 0.1f, 0.3f), new Color(0.3f, 0.05f, 0.1f) })
			lights.TweenProperty(background, "color", color, 0.45f);

		Vector2 screen = GetViewportRect().Size;

		for (int i = 0; i < Crowd.Length; i++)
		{
			var dancer = new Node2D
			{
				Position = new Vector2(screen.X * (i + 1) / (Crowd.Length + 1), screen.Y * 0.68f),
				Scale = new Vector2(Zoom, Zoom),
			};

			AddPart(dancer, Crowd[i][0], new Vector2(0, -6), 1.0f);
			AddPart(dancer, Crowd[i][1], new Vector2(0, -15), 0.5f);
			AddPart(dancer, Crowd[i][2], new Vector2(0, -23), 0.5f);

			dancer.AddChild(new Dancer());
			AddChild(dancer);
		}

		var title = new Label
		{
			Text = "GYŐZTÉL, MÁRIÓ!",
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(0, screen.Y * 0.12f),
			Size = new Vector2(screen.X, 80),
			LabelSettings = new LabelSettings
			{
				FontSize = 64,   // nem Aurafont: abban nincs ő, é, á, ó
				FontColor = new Color(1, 1, 0),
				OutlineSize = 10,
				OutlineColor = Colors.Black,
			},
		};
		AddChild(title);

		var subtitle = new Label
		{
			Text = "A tiéd a város legnagyobb aurája. Ma este mindenki érted táncol.",
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(0, screen.Y * 0.12f + 90),
			Size = new Vector2(screen.X, 30),
		};
		subtitle.AddThemeFontSizeOverride("font_size", 20);
		AddChild(subtitle);

		var back = new Button
		{
			Text = "Vissza a főmenübe",
			Position = new Vector2(screen.X / 2 - 120, screen.Y * 0.85f),
			Size = new Vector2(240, 44),
		};
		back.AddThemeFontSizeOverride("font_size", 20);
		back.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Menu/TitleScreen.tscn");
		AddChild(back);
		back.GrabFocus();

		var music = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://Art/Music/uoai.mp3") };
		music.Finished += () => music.Play();
		AddChild(music);
		music.Play();
	}

	private static void AddPart(Node2D dancer, string name, Vector2 position, float scale)
	{
		if (name == "")
			return;

		dancer.AddChild(new Sprite2D
		{
			Texture = GD.Load<Texture2D>($"res://Art/Characters/{name}.png"),
			Position = position,
			Scale = new Vector2(scale, scale),
			TextureFilter = TextureFilterEnum.Nearest,
		});
	}
}
