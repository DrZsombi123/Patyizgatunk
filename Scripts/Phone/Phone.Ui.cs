using Godot;

public partial class Phone
{
	// ---------- UI építés ----------

	private void BuildUi()
	{
		var corner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		corner.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		AddChild(corner);

		// telefon keret és kerekített, vágott képernyő
		_phone = Rounded(corner, Vector2.Zero, PhoneSize, new Color(0.05f, 0.05f, 0.05f), 36);
		_phone.Position = Hidden;
		((StyleBoxFlat)_phone.GetThemeStylebox("panel")).BorderColor = new Color(0.3f, 0.3f, 0.3f);
		((StyleBoxFlat)_phone.GetThemeStylebox("panel")).SetBorderWidthAll(2);

		Control screen = Rounded(_phone, new Vector2(10, 10), ScreenSize, Colors.Black, 28);
		screen.ClipChildren = CanvasItem.ClipChildrenMode.Only;

		BuildCamera(screen);

		// felül-alul sötétítés, hogy a fehér szöveg olvasható legyen a képen
		Shade(screen, 0, 120, 0.55f, 0.0f);
		Shade(screen, ScreenSize.Y - 230, 230, 0.0f, 0.65f);

		_clock = Text(screen, new Vector2(22, 6), 11, Colors.White);
		Text(screen, new Vector2(ScreenSize.X - 70, 6), 10, Colors.White).Text = "5G  ▮▮▮ 87%";

		_home = NewLayer(screen);
		_liveView = NewLayer(screen);
		_summary = NewLayer(screen);

		BuildHome();
		BuildLive();
		BuildSummary();

		_liveBadge = new Label { Position = new Vector2(-190, -40), Size = new Vector2(170, 24), HorizontalAlignment = HorizontalAlignment.Right };
		_liveBadge.AddThemeFontSizeOverride("font_size", 14);
		_liveBadge.AddThemeColorOverride("font_color", Colors.White);
		_liveBadge.AddThemeStyleboxOverride("normal", Flat(Pink, 4));
		corner.AddChild(_liveBadge);
	}

	// élő kamerakép: egy SubViewport, ami ugyanazt a világot nézi egy saját kamerával
	private void BuildCamera(Control screen)
	{
		var container = new SubViewportContainer { Stretch = true, Size = ScreenSize, MouseFilter = Control.MouseFilterEnum.Ignore };
		screen.AddChild(container);

		_camera = new SubViewport
		{
			World2D = GetViewport().World2D,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			HandleInputLocally = false,
		};
		container.AddChild(_camera);

		_cameraEye = new Camera2D { Zoom = new Vector2(2.6f, 2.6f) };
		_camera.AddChild(_cameraEye);
	}

	private void BuildHome()
	{
		Dim(_home, 0.35f);

		Button close = FlatButton(_home, new Vector2(12, 26), new Vector2(28, 28), "✕", Colors.Transparent, 16);
		close.Pressed += () => { if (_open) Toggle(); };

		Label tabs = Text(_home, new Vector2(0, 30), 13, Colors.White);
		tabs.Size = new Vector2(ScreenSize.X, 20);
		tabs.HorizontalAlignment = HorizontalAlignment.Center;
		tabs.Text = "Videó     Fotó     LIVE";
		ColorRect underline = new ColorRect { Color = Colors.White, Position = new Vector2(174, 50), Size = new Vector2(30, 2) };
		_home.AddChild(underline);

		// borító kártya
		Control card = Rounded(_home, new Vector2(14, 70), new Vector2(252, 64), new Color(0, 0, 0, 0.45f), 10);
		PlayerAvatar(card, new Vector2(8, 8), 48);
		Text(card, new Vector2(64, 10), 14, Colors.White).Text = "Kunu Márió élőben 🔥";
		_followersLabel = Text(card, new Vector2(64, 34), 11, Grey);

		// pénztárca
		Control wallet = Rounded(_home, new Vector2(14, 396), new Vector2(252, 56), new Color(0, 0, 0, 0.45f), 10);
		Text(wallet, new Vector2(12, 6), 10, Grey).Text = "LIVE ajándékok egyenlege";
		_walletLabel = Text(wallet, new Vector2(12, 24), 13, Colors.White);
		_cashButton = FlatButton(wallet, new Vector2(176, 14), new Vector2(68, 28), "Beváltás", new Color(1, 1, 1, 0.2f), 12);
		_cashButton.Pressed += CashOut;

		Button go = FlatButton(_home, new Vector2(20, 470), new Vector2(240, 46), "Élő indítása", Pink, 16);
		go.Pressed += StartLive;
	}

	private void BuildLive()
	{
		// házigazda pill: avatar, név, lájkok
		Control host = Rounded(_liveView, new Vector2(8, 24), new Vector2(136, 36), Pill, 18);
		PlayerAvatar(host, new Vector2(3, 3), 30);
		Text(host, new Vector2(38, 2), 12, Colors.White).Text = "kunu.mario";
		_likesLabel = Text(host, new Vector2(38, 18), 10, Grey);

		// jobb felül: nézők kis avatarjai + nézőszám + kilépés
		for (int i = 0; i < 3; i++)
			Avatar(_liveView, new Vector2(150 + i * 18, 28), 26, AvatarColor(Names[i * 4]), Names[i * 4]);

		Control viewers = Rounded(_liveView, new Vector2(206, 30), new Vector2(48, 24), Pill, 12);
		_viewersLabel = Text(viewers, new Vector2(0, 4), 11, Colors.White);
		_viewersLabel.Size = new Vector2(48, 16);
		_viewersLabel.HorizontalAlignment = HorizontalAlignment.Center;

		Button end = FlatButton(_liveView, new Vector2(256, 30), new Vector2(22, 24), "✕", Colors.Transparent, 15);
		end.Pressed += EndLive;

		Control rank = Rounded(_liveView, new Vector2(8, 66), new Vector2(150, 20), Pill, 10);
		_rankLabel = Text(rank, new Vector2(8, 3), 10, Colors.White);

		Control badge = Rounded(_liveView, new Vector2(164, 66), new Vector2(38, 20), Pink, 4);
		Label live = Text(badge, new Vector2(0, 2), 11, Colors.White);
		live.Size = new Vector2(38, 16);
		live.HorizontalAlignment = HorizontalAlignment.Center;
		live.Text = "LIVE";

		for (int i = 0; i < _banners.Length; i++)
		{
			_banners[i] = new GiftBanner { Position = new Vector2(6, 236 + i * 48) };
			_liveView.AddChild(_banners[i]);
		}

		_comments = new VBoxContainer
		{
			Position = new Vector2(10, 336),
			Size = new Vector2(206, 140),
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_comments.AddThemeConstantOverride("separation", 3);
		_liveView.AddChild(_comments);

		_hearts = NewLayer(_liveView);
		_hearts.MouseFilter = Control.MouseFilterEnum.Ignore;

		_waveButton = FlatButton(_liveView, new Vector2(8, 492), new Vector2(92, 32), "", new Color(1, 1, 1, 0.18f), 12);
		_waveButton.Pressed += Wave;

		_askButton = FlatButton(_liveView, new Vector2(106, 492), new Vector2(112, 32), "", new Color(1, 1, 1, 0.18f), 12);
		_askButton.Pressed += AskForGifts;

		_balanceLabel = Text(_liveView, new Vector2(222, 500), 12, Colors.White);
		_balanceLabel.Size = new Vector2(52, 16);
		_balanceLabel.HorizontalAlignment = HorizontalAlignment.Center;
	}

	private void BuildSummary()
	{
		Dim(_summary, 0.85f);

		Label title = Text(_summary, new Vector2(0, 70), 18, Colors.White);
		title.Size = new Vector2(ScreenSize.X, 24);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.Text = "Az élő véget ért";

		PlayerAvatar(_summary, new Vector2(ScreenSize.X / 2 - 36, 110), 72);

		Label name = Text(_summary, new Vector2(0, 190), 13, Grey);
		name.Size = new Vector2(ScreenSize.X, 18);
		name.HorizontalAlignment = HorizontalAlignment.Center;
		name.Text = "@kunu.mario";

		_summaryStats = Text(_summary, new Vector2(40, 232), 14, Colors.White);
		_summaryStats.Size = new Vector2(200, 160);
		_summaryStats.TabStops = new float[] { 120 };

		Button done = FlatButton(_summary, new Vector2(20, 470), new Vector2(240, 46), "Kész", Pink, 16);
		done.Pressed += () => ShowScreen(_home);
	}

	// ---------- UI segédek ----------

	private static Control NewLayer(Control parent)
	{
		var layer = new Control { Size = ScreenSize, MouseFilter = Control.MouseFilterEnum.Ignore };
		parent.AddChild(layer);
		return layer;
	}

	private static void Dim(Control parent, float alpha)
	{
		parent.AddChild(new ColorRect { Color = new Color(0, 0, 0, alpha), Size = ScreenSize, MouseFilter = Control.MouseFilterEnum.Ignore });
	}

	private static void Shade(Control parent, float y, float height, float top, float bottom)
	{
		var gradient = new Gradient { Offsets = new[] { 0.0f, 1.0f }, Colors = new[] { new Color(0, 0, 0, top), new Color(0, 0, 0, bottom) } };

		parent.AddChild(new TextureRect
		{
			Texture = new GradientTexture2D { Gradient = gradient, FillFrom = Vector2.Zero, FillTo = new Vector2(0, 1), Width = 4, Height = 64 },
			Position = new Vector2(0, y),
			Size = new Vector2(ScreenSize.X, height),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
	}

	private static StyleBoxFlat Flat(Color color, int radius)
	{
		var box = new StyleBoxFlat { BgColor = color, ContentMarginLeft = 4, ContentMarginRight = 4 };
		box.SetCornerRadiusAll(radius);
		return box;
	}

	private static Panel Rounded(Control parent, Vector2 position, Vector2 size, Color color, int radius)
	{
		var panel = new Panel { Position = position, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", Flat(color, radius));
		parent.AddChild(panel);
		return panel;
	}

	private static Label Text(Control parent, Vector2 position, int size, Color color)
	{
		var label = new Label { Position = position, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		parent.AddChild(label);
		return label;
	}

	// FocusMode.None: a Space (ugrás) ne nyomja meg a kijelölt gombot
	private static Button FlatButton(Control parent, Vector2 position, Vector2 size, string text, Color color, int fontSize)
	{
		var button = new Button { Position = position, Size = size, Text = text, FocusMode = Control.FocusModeEnum.None };
		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeColorOverride("font_color", Colors.White);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", Colors.White);
		button.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.45f));
		button.AddThemeStyleboxOverride("normal", Flat(color, 8));
		button.AddThemeStyleboxOverride("hover", Flat(color.Lightened(0.15f), 8));
		button.AddThemeStyleboxOverride("pressed", Flat(color.Darkened(0.15f), 8));
		button.AddThemeStyleboxOverride("disabled", Flat(color, 8));
		parent.AddChild(button);
		return button;
	}

	public static Control Avatar(Control parent, Vector2 position, float size, Color color, string name)
	{
		Panel circle = Rounded(parent, position, new Vector2(size, size), color, 999);
		((StyleBoxFlat)circle.GetThemeStylebox("panel")).BorderColor = Colors.White;
		((StyleBoxFlat)circle.GetThemeStylebox("panel")).SetBorderWidthAll(1);

		Label initial = Text(circle, Vector2.Zero, (int)(size * 0.45f), Colors.White);
		initial.Size = new Vector2(size, size);
		initial.HorizontalAlignment = HorizontalAlignment.Center;
		initial.VerticalAlignment = VerticalAlignment.Center;
		initial.Text = name.Substring(0, 1).ToUpper();
		return circle;
	}

	private static Texture2D _playerAvatar;

	// Márió feje kör alakban. ponytail: a ClipChildren a (szintén vágott) képernyőn belül nem vág,
	// ezért egyszer kivágjuk magát a képet: négyzet a fejből, körön kívül átlátszó, belül sötét háttér.
	private static void PlayerAvatar(Control parent, Vector2 position, float size)
	{
		if (_playerAvatar == null)
		{
			Image head = GD.Load<Texture2D>("res://Art/Characters/head_player.png").GetImage();
			if (head.IsCompressed())
				head.Decompress();
			head.Convert(Image.Format.Rgba8);

			int side = Mathf.Min(head.GetWidth(), head.GetHeight());
			Image round = Image.CreateEmpty(side, side, false, Image.Format.Rgba8);
			int top = (head.GetHeight() - side) / 2;
			float r = side / 2.0f;

			for (int y = 0; y < side; y++)
				for (int x = 0; x < side; x++)
				{
					if (new Vector2(x + 0.5f - r, y + 0.5f - r).Length() > r)
						continue;

					Color pixel = head.GetPixel(x, y + top);
					round.SetPixel(x, y, new Color(0.25f, 0.25f, 0.28f).Lerp(pixel, pixel.A) with { A = 1 });
				}

			_playerAvatar = ImageTexture.CreateFromImage(round);
		}

		parent.AddChild(new TextureRect
		{
			Texture = _playerAvatar,
			Position = position,
			Size = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
	}
}
