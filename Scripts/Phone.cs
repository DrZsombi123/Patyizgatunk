using Godot;

// Telefon: F1-re feljön a jobb alsó sarokból (mint a GTA-ban), még egyszer F1 és lecsúszik.
// Rajta a TikTok LIVE, a valódi mintájára: élő kamerakép Márióról, nézőszám, lájkok (szívek),
// kommentek, ajándékok kombóval, követők, a végén összegző képernyő.
// Az ajándékok rózsában (érme) gyűlnek, a főképernyőn pénzre váltjuk.
// Kocsiból élőzve kétszer gyorsabban jönnek a nézők és 4 mp-enként +1 aura jár.
// ponytail: a UI kódból épül, mint a Dialogue és a Hotbar - egy CanvasLayer node a World-ben.
// A képernyő fix méretű, ezért abszolút pozíciókkal dolgozunk, nem containerekkel.
public partial class Phone : CanvasLayer
{
	[Export]
	public Player Player { get; set; }

	[Export]
	public int RoseValue { get; set; } = 50;   // Ft / rózsa beváltáskor

	[Export]
	public int ViewersPerAura { get; set; } = 5;   // nézettség plafon: 50 + aura * ennyi

	[Export]
	public float CarAuraSeconds { get; set; } = 4.0f;   // kocsiból élőzve ennyi mp-enként +1 aura

	private static readonly Color Pink = new Color("fe2c55");
	private static readonly Color Cyan = new Color("25f4ee");
	private static readonly Color Grey = new Color(1, 1, 1, 0.7f);
	private static readonly Color Pill = new Color(0, 0, 0, 0.35f);

	private static readonly Vector2 ScreenSize = new Vector2(280, 540);
	private static readonly Vector2 PhoneSize = ScreenSize + new Vector2(20, 20);
	private static readonly Vector2 Shown = new Vector2(-PhoneSize.X - 20, -PhoneSize.Y - 20);
	private static readonly Vector2 Hidden = new Vector2(-PhoneSize.X - 20, 30);

	private static readonly string[] Names =
	{
		"bence_07", "lili.sz", "kovacs.dani", "reka_xoxo", "gabi1998", "tomi.hun", "pisti_bacsi",
		"zsofi.k", "marci.gamer", "nori_edit", "csabesz", "anna.b", "dominik.x", "vivi_22", "laci.bacsi",
	};

	private static readonly string[] Chat =
	{
		"Márió a király 👑", "hajrá!! 🔥🔥", "honnan vagy?", "köszönj nekem is 🥺", "first", "Brendon hol van?",
		"ez a város durva", "😂😂😂", "szép séró", "mennyi aurád van?", "élő legenda", "gyere a kaszinóba",
		"💀💀", "nézem suli helyett", "Kunu Márió forever", "menő a merci", "hol a patyi?", "tolom a követést ✅",
	};

	private static readonly string[] CarChat = { "lassíts!! 😱", "merci 🔥", "vigyél el engem is", "ne a telefont nézd vezetés közben 😂", "hova mész?" };
	private static readonly string[] DrunkChat = { "részeg vagy tesó? 😂", "menj haza aludni", "hány Finlandia volt? 🍾", "ez már sok lesz", "ne vezess így!!" };
	private static readonly string[] WaveChat = { "szia Márió!! 👋", "köszönt!! 😍", "szia szia 👋👋", "látott engem 🥹" };
	private static readonly string[] GiftChat = { "küldtem! 🌹", "nincs pénzem 😭", "🌹🌹🌹", "tessék király", "ennyi jár 🎁" };

	// ajándék: emoji, név, érték rózsában, esély súly, legalább ennyi néző kell hozzá
	private static readonly (string Emoji, string Name, int Value, int Weight, int MinViewers)[] Gifts =
	{
		("🌹", "Rózsa", 1, 70, 0),
		("💖", "Szív", 5, 16, 0),
		("🍩", "Fánk", 30, 8, 20),
		("👑", "Korona", 99, 5, 60),
		("🦁", "Oroszlán", 500, 1, 200),
	};

	private SubViewport _camera;
	private Camera2D _cameraEye;
	private Control _phone;
	private Control _home;
	private Control _liveView;
	private Control _summary;
	private Label _clock;
	private Label _liveBadge;
	private Tween _slide;

	// főképernyő
	private Label _followersLabel;
	private Label _walletLabel;
	private Button _cashButton;

	// élő
	private Label _likesLabel;
	private Label _viewersLabel;
	private Label _rankLabel;
	private VBoxContainer _comments;
	private Control _hearts;
	private Button _waveButton;
	private Button _askButton;
	private Label _balanceLabel;
	private readonly GiftBanner[] _banners = new GiftBanner[2];

	// összegző
	private Label _summaryStats;

	private readonly RandomNumberGenerator _rng = new();

	private bool _open = false;
	private bool _live = false;

	private float _viewers = 0.0f;
	private int _peakViewers = 0;
	private int _roses = 0;          // beváltatlan egyenleg
	private int _liveRoses = 0;      // ebben az élőben kapott
	private float _likes = 0.0f;
	private int _followers = 1234;
	private int _newFollowers = 0;
	private float _liveTime = 0.0f;
	private float _carAuraTime = 0.0f;

	private float _commentTimer = 1.0f;
	private float _giftTimer = 3.0f;
	private float _joinTimer = 1.0f;
	private float _heartBudget = 0.0f;
	private float _waveCooldown = 0.0f;
	private float _askCooldown = 0.0f;
	private float _askBoost = 0.0f;   // "Kérj rózsát" után egy darabig több ajándék jön

	public override void _Ready()
	{
		Layer = 11;
		BuildUi();
		ShowScreen(_home);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (Input.IsActionJustPressed("phone"))
			Toggle();

		_clock.Text = System.DateTime.Now.ToString("HH:mm");

		if (_live && Player != null)
			Stream(dt);

		if (_open && Player != null)
			_cameraEye.GlobalPosition = Player.GlobalPosition + new Vector2(0, -12);

		Refresh();
	}

	private void Toggle()
	{
		_open = !_open;

		// a kamera csak nyitott telefonnál renderel, különben kétszer rajzolnánk a világot
		_camera.RenderTargetUpdateMode = _open ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;

		_slide?.Kill();
		_slide = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(_open ? Tween.EaseType.Out : Tween.EaseType.In);
		_slide.TweenProperty(_phone, "position", _open ? Shown : Hidden, 0.35f);
	}

	private void ShowScreen(Control screen)
	{
		_home.Visible = screen == _home;
		_liveView.Visible = screen == _liveView;
		_summary.Visible = screen == _summary;
	}

	// ---------- élő szimuláció ----------

	private void StartLive()
	{
		_live = true;
		_viewers = 0.0f;
		_peakViewers = 0;
		_liveRoses = 0;
		_likes = 0.0f;
		_newFollowers = 0;
		_liveTime = 0.0f;
		_carAuraTime = 0.0f;

		foreach (Node child in _comments.GetChildren())
			child.QueueFree();

		ShowScreen(_liveView);
		SystemLine("Üdv az élőben! Légy kedves és tartsd tiszteletben a közösségi irányelveket.");
	}

	private void EndLive()
	{
		_live = false;

		_summaryStats.Text =
			$"Időtartam\t{(int)_liveTime / 60:00}:{(int)_liveTime % 60:00}\n" +
			$"Legtöbb néző\t{Short(_peakViewers)}\n" +
			$"Új követők\t+{_newFollowers}\n" +
			$"Lájkok\t{Short((int)_likes)}\n" +
			$"Kapott ajándék\t🌹 {Short(_liveRoses)}";

		ShowScreen(_summary);
	}

	private void Stream(float delta)
	{
		_liveTime += delta;

		// nézők: az aurával és kocsiban gyorsabban jönnek, plafon az aurától függ, közben ingadozik
		float growth = (1.0f + Player.Aura / 25.0f) * (Player.InCar ? 2.0f : 1.0f);
		float cap = 50 + Mathf.Max(Player.Aura, 0) * ViewersPerAura;
		_viewers = Mathf.Clamp(_viewers + growth * delta + _rng.RandfRange(-1.5f, 1.5f) * delta * Mathf.Sqrt(_viewers + 1), 0, cap);
		_peakViewers = Mathf.Max(_peakViewers, (int)_viewers);

		// lájkok és a jobb oldalon felszálló szívek
		_likes += _viewers * 0.4f * delta;
		_heartBudget += Mathf.Min(_viewers * 0.05f, 6.0f) * delta;
		for (; _heartBudget >= 1.0f; _heartBudget -= 1.0f)
			SpawnHeart();

		_waveCooldown = Mathf.Max(0, _waveCooldown - delta);
		_askCooldown = Mathf.Max(0, _askCooldown - delta);
		_askBoost = Mathf.Max(0, _askBoost - delta);

		if (Tick(ref _commentTimer, delta, 0.4f + _viewers / 50.0f, 3.0f))
			Comment(RandomName(), PickChat());

		if (Tick(ref _joinTimer, delta, 0.2f + _viewers / 80.0f, 1.5f))
			SystemLine($"{RandomName()} csatlakozott 👋");

		if (Tick(ref _giftTimer, delta, _viewers / 120.0f * (_askBoost > 0 ? 3.0f : 1.0f), 2.0f))
			SendGift();

		if (_rng.Randf() < _viewers / 400.0f * delta)
		{
			_newFollowers++;
			_followers++;
			SystemLine($"{RandomName()} követ téged ✅");
		}

		if (!Player.InCar)
			return;

		_carAuraTime += delta;

		if (_carAuraTime >= CarAuraSeconds)
		{
			_carAuraTime -= CarAuraSeconds;
			Player.AddAura(1);
		}
	}

	// rate esemény/mp szerinti véletlen időzítő; max: legfeljebb ennyi esemény/mp
	private bool Tick(ref float timer, float delta, float rate, float max)
	{
		if (rate <= 0.001f)
			return false;

		timer -= delta;

		if (timer > 0)
			return false;

		timer = _rng.RandfRange(0.5f, 1.5f) / Mathf.Min(rate, max);
		return true;
	}

	private string PickChat()
	{
		var drunk = Player.GetNodeOrNull<DrunkSystem>("DrunkSystem");

		if (drunk != null && drunk.Drunkness >= 50 && _rng.Randf() < 0.4f)
			return Pick(DrunkChat);

		if (Player.InCar && _rng.Randf() < 0.4f)
			return Pick(CarChat);

		return Pick(Chat);
	}

	private void SendGift()
	{
		int total = 0;
		foreach (var gift in Gifts)
			if (_viewers >= gift.MinViewers)
				total += gift.Weight;

		int roll = _rng.RandiRange(0, total - 1);
		var chosen = Gifts[0];

		foreach (var gift in Gifts)
		{
			if (_viewers < gift.MinViewers)
				continue;

			if (roll < gift.Weight)
			{
				chosen = gift;
				break;
			}

			roll -= gift.Weight;
		}

		// rózsából kombóban szokás küldeni
		int count = chosen.Value == 1 ? _rng.RandiRange(1, 15) : _rng.RandiRange(1, 3);
		int value = chosen.Value * count;

		_roses += value;
		_liveRoses += value;

		string name = RandomName();
		FreeBanner().Show(name, chosen.Name, chosen.Emoji, count, AvatarColor(name));
		Comment(name, $"küldött: {chosen.Name} {chosen.Emoji} x{count}", gift: true);
	}

	private void Wave()
	{
		if (_waveCooldown > 0)
			return;

		_waveCooldown = 10.0f;
		_viewers += 3 + _viewers * 0.05f;
		Comment("kunu.mario", "Sziasztok, köszönöm hogy itt vagytok! 👋", host: true);

		for (int i = 0; i < 3; i++)
			Comment(RandomName(), Pick(WaveChat));
	}

	private void AskForGifts()
	{
		if (_askCooldown > 0)
			return;

		_askCooldown = 20.0f;
		_askBoost = 8.0f;
		Comment("kunu.mario", "Ha tetszik, küldjetek egy rózsát! 🌹🙏", host: true);
		Comment(RandomName(), Pick(GiftChat));
	}

	private void CashOut()
	{
		if (_roses == 0 || Player == null)
			return;

		Player.Money += _roses * RoseValue;
		_roses = 0;
	}

	// ---------- kommentek, szívek ----------

	private void Comment(string name, string text, bool host = false, bool gift = false)
	{
		string nameColor = host ? Pink.ToHtml(false) : "c8c8c8";
		string badge = host ? " [bgcolor=#fe2c55][color=white] Házigazda [/color][/bgcolor]" : "";
		string textColor = gift ? Cyan.ToHtml(false) : "ffffff";

		AddLine($"[color=#{nameColor}][b]{name}[/b][/color]{badge}  [color=#{textColor}]{text}[/color]");
	}

	private void SystemLine(string text) => AddLine($"[color=#{new Color(1, 1, 1, 0.6f).ToHtml(false)}]{text}[/color]");

	private void AddLine(string bbcode)
	{
		var line = new RichTextLabel
		{
			BbcodeEnabled = true,
			Text = bbcode,
			FitContent = true,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(200, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0),
		};

		foreach (string style in new[] { "normal", "bold" })
			line.AddThemeFontSizeOverride(style + "_font_size", 11);

		line.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
		line.AddThemeConstantOverride("shadow_offset_x", 1);
		line.AddThemeConstantOverride("shadow_offset_y", 1);

		_comments.AddChild(line);
		line.CreateTween().TweenProperty(line, "modulate:a", 1.0f, 0.2f);

		// csak az utolsó pár sor látszik, a régiek kiesnek felül
		while (_comments.GetChildCount() > 7)
		{
			Node old = _comments.GetChild(0);
			_comments.RemoveChild(old);
			old.QueueFree();
		}
	}

	private void SpawnHeart()
	{
		var heart = new Label
		{
			Text = Pick(new[] { "❤️", "💖", "💗", "💜", "💙", "🧡" }),
			Position = new Vector2(236 + _rng.RandfRange(-6, 6), 470),
			PivotOffset = new Vector2(10, 12),
			Scale = new Vector2(0.4f, 0.4f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		heart.AddThemeFontSizeOverride("font_size", 20);
		_hearts.AddChild(heart);

		float seconds = _rng.RandfRange(1.4f, 2.0f);
		Tween tween = heart.CreateTween().SetParallel();
		tween.TweenProperty(heart, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(heart, "position:y", 470 - _rng.RandfRange(150, 210), seconds).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(heart, "position:x", heart.Position.X + _rng.RandfRange(-30, 20), seconds).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(heart, "modulate:a", 0.0f, seconds * 0.5f).SetDelay(seconds * 0.5f);
		tween.Chain().TweenCallback(Callable.From(heart.QueueFree));
	}

	private GiftBanner FreeBanner()
	{
		foreach (GiftBanner banner in _banners)
			if (!banner.Busy)
				return banner;

		// mindkettő foglalt: a régebbit írjuk felül
		return _banners[0].StartedAt < _banners[1].StartedAt ? _banners[0] : _banners[1];
	}

	// ---------- kijelzés ----------

	private void Refresh()
	{
		int viewers = (int)_viewers;

		_viewersLabel.Text = $"👁 {Short(viewers)}";
		_likesLabel.Text = $"{Short((int)_likes)} lájk";
		_rankLabel.Text = $"🔥 Óránkénti rangsor · {Mathf.Max(1, 99 - viewers / 5)}.";
		_balanceLabel.Text = $"🌹 {Short(_roses)}";
		_waveButton.Text = _waveCooldown > 0 ? $"👋 {Mathf.CeilToInt(_waveCooldown)}" : "👋 Köszönj";
		_waveButton.Disabled = _waveCooldown > 0;
		_askButton.Text = _askCooldown > 0 ? $"🌹 {Mathf.CeilToInt(_askCooldown)}" : "🌹 Kérj rózsát";
		_askButton.Disabled = _askCooldown > 0;

		_followersLabel.Text = $"@kunu.mario · {Short(_followers)} követő";
		_walletLabel.Text = $"🌹 {_roses} rózsa  =  {Ft(_roses * RoseValue)}";
		_cashButton.Disabled = _roses == 0;

		// csukott telefonnál is lássuk, hogy megy az élő
		_liveBadge.Visible = _live && !_open;
		_liveBadge.Text = $" LIVE  👁 {Short(viewers)} ";
	}

	// TikTok-os rövid számok: 1234 -> 1,2E
	private static string Short(int n)
	{
		if (n < 1000)
			return n.ToString();

		if (n < 1_000_000)
			return (n / 1000.0f).ToString("0.#").Replace('.', ',') + "E";

		return (n / 1_000_000.0f).ToString("0.#").Replace('.', ',') + "M";
	}

	private static string Ft(int amount) => amount.ToString("#,0").Replace(",", " ") + " Ft";

	private string RandomName() => Pick(Names);

	private string Pick(string[] from) => from[_rng.RandiRange(0, from.Length - 1)];

	private static Color AvatarColor(string name) => Color.FromHsv(Mathf.Abs(name.GetHashCode() % 360) / 360.0f, 0.55f, 0.85f);

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

	// Márió feje kör alakban (a kör kivágja a képet)
	private static void PlayerAvatar(Control parent, Vector2 position, float size)
	{
		Panel circle = Rounded(parent, position, new Vector2(size, size), new Color(0.2f, 0.2f, 0.2f), 999);
		circle.ClipChildren = CanvasItem.ClipChildrenMode.AndDraw;

		circle.AddChild(new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://Art/head_player.png"),
			Size = new Vector2(size, size * 1.2f),
			Position = new Vector2(0, -size * 0.05f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
	}

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
