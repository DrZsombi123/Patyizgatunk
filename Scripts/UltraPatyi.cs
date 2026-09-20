using Godot;

// Rejtett gomb a sikátorban, a graffiti alatt: E -> ULTRA PATYI MODE.
// Duration másodpercig kristály hullik a képernyőre, pulzál a szín és lüktet a kamera.
// ponytail: az egész effekt kódból épül, a scene-ben csak az Area2D + a shape van.
public partial class UltraPatyi : Area2D
{
	[Export]
	public float Duration { get; set; } = 20.0f;

	private Player _player;
	private Camera2D _camera;
	private Vector2 _cameraZoom;

	private ColorRect _tint;
	private CpuParticles2D _por;
	private Label _cim;

	private float _left = 0.0f;

	public override void _Ready()
	{
		BodyEntered += body => { if (body is Player p) _player = p; };
		BodyExited += body => { if (body == _player) _player = null; };

		BuildFx();
	}

	private void BuildFx()
	{
		CanvasLayer fx = new() { Layer = 100 };
		AddChild(fx);

		_tint = new ColorRect
		{
			Color = new Color(1, 1, 1, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_tint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fx.AddChild(_tint);

		// 4x4 fehér pötty = a kristály szemcse, így nem kell hozzá art
		Image szemcse = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
		szemcse.Fill(Colors.White);

		_por = new CpuParticles2D
		{
			Texture = ImageTexture.CreateFromImage(szemcse),
			Emitting = false,
			Amount = 300,
			Lifetime = 3.5f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			Direction = Vector2.Down,
			Spread = 25.0f,
			Gravity = new Vector2(0, 220),
			InitialVelocityMin = 40.0f,
			InitialVelocityMax = 140.0f,
			AngularVelocityMin = -180.0f,
			AngularVelocityMax = 180.0f,
			ScaleAmountMin = 1.0f,
			ScaleAmountMax = 3.0f,
			Color = new Color(0.85f, 0.95f, 1.0f),
		};
		fx.AddChild(_por);

		_cim = new Label
		{
			Text = "ULTRA PATYI MODE",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0),
		};
		_cim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_cim.AddThemeFontSizeOverride("font_size", 48);
		_cim.AddThemeConstantOverride("outline_size", 10);
		_cim.AddThemeColorOverride("font_outline_color", Colors.Black);
		fx.AddChild(_cim);
	}

	public override void _Process(double delta)
	{
		if (_left <= 0.0f)
		{
			if (_player != null && Input.IsActionJustPressed("pickup"))
				Start();

			return;
		}

		_left -= (float)delta;

		float t = Duration - _left;

		Color szin = Color.FromHsv(Mathf.PosMod(t * 0.5f, 1.0f), 0.85f, 1.0f);
		szin.A = 0.12f + 0.06f * Mathf.Sin(t * 14.0f);
		_tint.Color = szin;

		_cim.Modulate = new Color(1, 1, 1, Mathf.Clamp(2.5f - t, 0.0f, 1.0f));

		if (_camera != null)
			_camera.Zoom = _cameraZoom * (1.0f + 0.05f * Mathf.Sin(t * 11.0f));

		if (_left <= 0.0f)
			Stop();
	}

	private void Start()
	{
		_left = Duration;

		Vector2 screen = GetViewport().GetVisibleRect().Size;

		_por.Position = new Vector2(screen.X * 0.5f, -24.0f);
		_por.EmissionRectExtents = new Vector2(screen.X * 0.5f + 40.0f, 8.0f);
		_por.Emitting = true;

		_camera = _player.GetNodeOrNull<Camera2D>("Camera2D");

		if (_camera != null)
			_cameraZoom = _camera.Zoom;

		_player.TakePatyi();
	}

	private void Stop()
	{
		_left = 0.0f;

		_por.Emitting = false;          // a levegőben lévő szemcsék még leesnek
		_tint.Color = new Color(1, 1, 1, 0);
		_cim.Modulate = new Color(1, 1, 1, 0);

		if (_camera != null)
		{
			_camera.Zoom = _cameraZoom;
			_camera = null;
		}
	}
}
