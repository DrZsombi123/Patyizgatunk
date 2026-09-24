using Godot;

/// <summary>
/// Árkád stílusú aura számláló. Egy Label node-ra kell tenni.
/// Feliratkozik a Player.AuraChanged signaljára.
/// </summary>
public partial class AuraLabel : Label
{
	[Export] public Player Player { get; set; }
	[Export] public Font ArcadeFont { get; set; }   // pl. Press Start 2P
	[Export] public int FontSize { get; set; } = 32;

	private static readonly Color Normal = new Color(1.0f, 0.85f, 0.1f);   // árkád sárga
	private static readonly Color Gain = new Color(0.3f, 1.0f, 0.3f);
	private static readonly Color Loss = new Color(1.0f, 0.25f, 0.25f);

	private LabelSettings _settings;
	private Tween _colorTween;
	private Tween _punchTween;

	private int _target;
	private float _shown;
	private int _lastShown = int.MinValue;

	public override void _Ready()
	{
		if (Player == null)
		{
			GD.PushError("AuraLabel: állítsd be a 'Player' mezőt az Inspectorban.");
			SetProcess(false);
			return;
		}

		_settings = new LabelSettings
		{
			Font = ArcadeFont,
			FontSize = FontSize,
			FontColor = Normal,
			OutlineSize = 8,
			OutlineColor = Colors.Black,
			ShadowColor = new Color(0f, 0f, 0f, 0.6f),
			ShadowOffset = new Vector2(4f, 4f)
		};
		LabelSettings = _settings;

		_target = Player.Aura;
		_shown = _target;

		Player.AuraChanged += OnAuraChanged;
	}

	private void OnAuraChanged(int total)
	{
		int diff = total - _target;
		_target = total;

		Flash(diff >= 0 ? Gain : Loss);
		Punch();
	}

	public override void _Process(double delta)
	{
		// árkád "gurulós" számláló: gyorsan odagurul az új értékhez
		float speed = Mathf.Max(40.0f, Mathf.Abs(_target - _shown) * 5.0f);
		_shown = Mathf.MoveToward(_shown, _target, speed * (float)delta);

		int now = Mathf.RoundToInt(_shown);

		if (now != _lastShown)
		{
			_lastShown = now;
			Text = $"AURA {now:D5}";
		}
	}

	private void Flash(Color color)
	{
		_colorTween?.Kill();
		_settings.FontColor = color;

		_colorTween = CreateTween();
		_colorTween.TweenProperty(_settings, "font_color", Normal, 0.6f);
	}

	private void Punch()
	{
		_punchTween?.Kill();
		PivotOffset = new Vector2(0.0f, Size.Y / 2.0f);
		Scale = new Vector2(1.25f, 1.25f);

		_punchTween = CreateTween();
		_punchTween
			.TweenProperty(this, "scale", Vector2.One, 0.3f)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}
}