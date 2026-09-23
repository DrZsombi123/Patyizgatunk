using Godot;

// Telefon: F1-re feljön a jobb alsó sarokból (mint a GTA-ban), még egyszer F1 és lecsúszik.
// Rajta a TikTok live: amíg élőzünk, jönnek a nézők, a nézők rózsát küldenek,
// a rózsát pénzre váltjuk és a boltban / klubban / sikátorban elköltjük.
// Kocsiból élőzni több aurát és gyorsabban növő nézettséget ad -> több rózsát.
// ponytail: a UI kódból épül, mint a Dialogue és a Hotbar - egy CanvasLayer node a World-ben.
public partial class Phone : CanvasLayer
{
	[Export]
	public Player Player { get; set; }

	[Export]
	public int RoseValue { get; set; } = 50;   // Ft / rózsa kivételkor

	[Export]
	public float RosesPerViewer { get; set; } = 0.01f;   // rózsa / néző / mp -> 100 néző = 1 rózsa/mp

	[Export]
	public int ViewersPerAura { get; set; } = 5;   // nézettség plafon: 50 + aura * ennyi

	[Export]
	public float CarAuraSeconds { get; set; } = 4.0f;   // kocsiból élőzve ennyi mp-enként +1 aura

	private static readonly Color Yellow = new Color(1, 1, 0);
	private static readonly Color Red = new Color(1, 0.2f, 0.3f);

	private static readonly Vector2 PhoneSize = new Vector2(260, 430);
	private static readonly Vector2 Shown = new Vector2(-PhoneSize.X - 24, -PhoneSize.Y - 24);
	private static readonly Vector2 Hidden = new Vector2(-PhoneSize.X - 24, 24);

	private PanelContainer _phone;
	private Label _status;
	private Label _viewersLabel;
	private Label _rosesLabel;
	private Label _liveBadge;
	private Button _liveButton;
	private Button _cashButton;
	private Tween _slide;

	private bool _open = false;
	private bool _live = false;
	private float _viewers = 0.0f;
	private float _roses = 0.0f;
	private float _carAuraTime = 0.0f;

	public override void _Ready()
	{
		Layer = 11;
		BuildUi();
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("phone"))
			Toggle();

		if (_live && Player != null)
			Stream((float)delta);

		Refresh();
	}

	private void Toggle()
	{
		_open = !_open;

		_slide?.Kill();
		_slide = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(_open ? Tween.EaseType.Out : Tween.EaseType.In);
		_slide.TweenProperty(_phone, "position", _open ? Shown : Hidden, 0.35f);
	}

	// A nézettség az aurával nő, kocsiból duplán. Minden néző csöpögteti a rózsát.
	private void Stream(float delta)
	{
		float growth = (1.0f + Player.Aura / 25.0f) * (Player.InCar ? 2.0f : 1.0f);

		_viewers = Mathf.Min(_viewers + growth * delta, 50 + Mathf.Max(Player.Aura, 0) * ViewersPerAura);
		_roses += _viewers * RosesPerViewer * delta;

		if (!Player.InCar)
			return;

		_carAuraTime += delta;

		if (_carAuraTime >= CarAuraSeconds)
		{
			_carAuraTime -= CarAuraSeconds;
			Player.AddAura(1);
		}
	}

	private void ToggleLive()
	{
		_live = !_live;
		_viewers = 0.0f;   // új élő, nulláról indul a nézettség
		_carAuraTime = 0.0f;
	}

	private void CashOut()
	{
		int roses = (int)_roses;

		if (roses == 0 || Player == null)
			return;

		_roses -= roses;
		Player.Money += roses * RoseValue;
	}

	private void Refresh()
	{
		int viewers = (int)_viewers;
		int roses = (int)_roses;

		_status.Text = _live ? "● ÉLŐ" : "Offline";
		_status.AddThemeColorOverride("font_color", _live ? Red : new Color(1, 1, 1, 0.55f));
		_viewersLabel.Text = $"Nézők: {viewers}";
		_rosesLabel.Text = $"Rózsák: {roses}";
		_liveButton.Text = _live ? "Élő leállítása" : "Élő indítása";
		_cashButton.Text = $"Kivétel: {roses * RoseValue} Ft";
		_cashButton.Disabled = roses == 0;

		// csukott telefonnál is lássuk, hogy megy az élő
		_liveBadge.Visible = _live && !_open;
		_liveBadge.Text = $"● LIVE  {viewers}";
	}

	private void BuildUi()
	{
		// nulla méretű sarokpont jobb lent, a telefon ehhez képest csúszik
		var corner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		corner.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		AddChild(corner);

		_phone = new PanelContainer { Size = PhoneSize, Position = Hidden };
		_phone.AddThemeStyleboxOverride("panel", Frame());
		corner.AddChild(_phone);

		var inner = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			inner.AddThemeConstantOverride("margin_" + side, 16);
		_phone.AddChild(inner);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		inner.AddChild(box);

		var title = new Label { Text = "TikTok LIVE", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 22);
		title.AddThemeColorOverride("font_color", Yellow);
		box.AddChild(title);

		_status = NewLabel(box, 18);
		_status.HorizontalAlignment = HorizontalAlignment.Center;

		box.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.25f), CustomMinimumSize = new Vector2(0, 1) });

		_viewersLabel = NewLabel(box, 18);
		_rosesLabel = NewLabel(box, 18);

		// kitölti a helyet, a gombok a telefon aljára kerülnek
		box.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

		_liveButton = NewButton(box);
		_liveButton.Pressed += ToggleLive;

		_cashButton = NewButton(box);
		_cashButton.Pressed += CashOut;

		var hint = NewLabel(box, 12);
		hint.Text = "F1: telefon eltevése";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.5f));

		_liveBadge = new Label { Position = new Vector2(-150, -40), Size = new Vector2(130, 24), HorizontalAlignment = HorizontalAlignment.Right };
		_liveBadge.AddThemeFontSizeOverride("font_size", 16);
		_liveBadge.AddThemeColorOverride("font_color", Red);
		_liveBadge.AddThemeColorOverride("font_outline_color", Colors.Black);
		_liveBadge.AddThemeConstantOverride("outline_size", 4);
		corner.AddChild(_liveBadge);
	}

	private static Label NewLabel(Container parent, int size)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", size);
		parent.AddChild(label);
		return label;
	}

	// FocusMode.None: a Space (ugrás) ne nyomja meg a kijelölt gombot
	private static Button NewButton(Container parent)
	{
		var button = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 38) };
		button.AddThemeFontSizeOverride("font_size", 17);
		parent.AddChild(button);
		return button;
	}

	private static StyleBoxFlat Frame()
	{
		var frame = new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.9f),
			BorderColor = new Color(1, 1, 1),
			CornerRadiusTopLeft = 22,
			CornerRadiusTopRight = 22,
			CornerRadiusBottomLeft = 22,
			CornerRadiusBottomRight = 22,
			ShadowSize = 10,
			ShadowColor = new Color(0, 0, 0, 0.5f),
		};

		frame.SetBorderWidthAll(4);
		return frame;
	}
}
