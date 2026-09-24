using Godot;

public partial class Phone
{
	// Bal oldalt beúszó ajándék sáv: avatar, név, "küldött: Rózsa", nagy emoji, felpörgő kombó (x12).
	private partial class GiftBanner : Control
	{
		public bool Busy { get; private set; }
		public ulong StartedAt { get; private set; }

		private Panel _pill;
		private Control _avatar;
		private Label _name;
		private Label _what;
		private Label _emoji;
		private Label _combo;
		private Tween _tween;

		public override void _Ready()
		{
			MouseFilter = MouseFilterEnum.Ignore;
			Modulate = new Color(1, 1, 1, 0);

			var gradient = new Gradient { Offsets = new[] { 0.0f, 1.0f }, Colors = new[] { new Color(0.1f, 0.05f, 0.2f, 0.85f), new Color(0.1f, 0.05f, 0.2f, 0.0f) } };
			AddChild(new TextureRect
			{
				Texture = new GradientTexture2D { Gradient = gradient, Width = 64, Height = 4 },
				Size = new Vector2(190, 40),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = MouseFilterEnum.Ignore,
			});

			_name = Text(this, new Vector2(42, 3), 12, Colors.White);
			_what = Text(this, new Vector2(42, 20), 10, Grey);
			_emoji = Text(this, new Vector2(128, 2), 26, Colors.White);

			_combo = Text(this, new Vector2(166, 4), 22, new Color(1, 0.9f, 0.3f));
			_combo.AddThemeColorOverride("font_outline_color", new Color(0.4f, 0.1f, 0));
			_combo.AddThemeConstantOverride("outline_size", 4);
			_combo.PivotOffset = new Vector2(10, 14);
		}

		public void Show(string name, string gift, string emoji, int count, Color color)
		{
			Busy = true;
			StartedAt = Time.GetTicksMsec();

			_avatar?.QueueFree();
			_avatar = Avatar(this, new Vector2(4, 4), 32, color, name);

			_name.Text = name.Length > 11 ? name.Substring(0, 11) + "…" : name;
			_what.Text = "küldött: " + gift;
			_emoji.Text = emoji;

			_tween?.Kill();
			Position = new Vector2(-200, Position.Y);
			_tween = CreateTween();
			_tween.TweenProperty(this, "position:x", 6.0f, 0.25f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_tween.Parallel().TweenProperty(this, "modulate:a", 1.0f, 0.2f);

			// a kombó számláló egyesével felpörög, minden lépésnél "ütés"
			for (int i = 1; i <= count; i++)
			{
				int n = i;
				_tween.TweenCallback(Callable.From(() =>
				{
					_combo.Text = "x" + n;
					_combo.Scale = new Vector2(1.5f, 1.5f);
				}));
				_tween.TweenProperty(_combo, "scale", Vector2.One, 0.12f);
			}

			_tween.TweenInterval(1.8f);
			_tween.TweenProperty(this, "modulate:a", 0.0f, 0.3f);
			_tween.TweenCallback(Callable.From(() => Busy = false));
		}
	}
}
