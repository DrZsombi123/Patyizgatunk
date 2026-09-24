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
	public int ViewersPerAura { get; set; } = 5;   // ennyi néző jár auránként (alap: 60 + aura * ennyi)

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
		("🌹", "Rózsa", 1, 150, 0),
		("💖", "Szív", 5, 30, 0),
		("🍩", "Fánk", 30, 12, 20),
		("👑", "Korona", 99, 6, 60),
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

	// mentéshez
	public int Followers { get => _followers; set => _followers = value; }
	public int Roses { get => _roses; set => _roses = value; }

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

		// nézők: egy célszám felé tartanak, ami az aurával, az élő hosszával (max 4x) és kocsiban nő.
		// Nincs kemény plafon: a szám folyton ingadozik, mint a valódi élőben, nem fagy be.
		float target = (60 + Mathf.Max(Player.Aura, 0) * ViewersPerAura)
			* Mathf.Min(1.0f + _liveTime / 90.0f, 4.0f)
			* (Player.InCar ? 1.5f : 1.0f);

		_viewers += (target - _viewers) * 0.04f * delta;
		_viewers = Mathf.Max(0, _viewers + _rng.RandfRange(-1.0f, 1.0f) * Mathf.Sqrt(_viewers + 1) * 1.5f * delta);
		_peakViewers = Mathf.Max(_peakViewers, (int)_viewers);

		// lájkok és a jobb oldalon felszálló szívek
		_likes += _viewers * 0.4f * delta;
		_heartBudget += Mathf.Min(_viewers * 0.05f, 6.0f) * delta;
		for (; _heartBudget >= 1.0f; _heartBudget -= 1.0f)
			SpawnHeart();

		_waveCooldown = Mathf.Max(0, _waveCooldown - delta);
		_askCooldown = Mathf.Max(0, _askCooldown - delta);
		_askBoost = Mathf.Max(0, _askBoost - delta);

		if (Chance(delta, 0.4f + _viewers / 50.0f, 3.0f))
			Comment(RandomName(), PickChat());

		if (Chance(delta, 0.2f + _viewers / 80.0f, 1.5f))
			SystemLine($"{RandomName()} csatlakozott 👋");

		// átlag ~10 rózsa / ajándék -> 100 néző nagyjából 1,2 rózsa/mp (~60 Ft/mp)
		if (Chance(delta, _viewers / 800.0f * (_askBoost > 0 ? 3.0f : 1.0f), 2.0f))
			SendGift();

		if (Chance(delta, _viewers / 400.0f, 1.0f))
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

	// átlagosan "rate" esemény/mp (legfeljebb "max"), képkockánként sorsolva - így azonnal követi a nézőszámot
	private bool Chance(float delta, float rate, float max) => _rng.Randf() < Mathf.Min(rate, max) * delta;

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
		int count = chosen.Value == 1 ? _rng.RandiRange(1, 5) : 1;
		int value = chosen.Value * count;

		_roses += value;
		_liveRoses += value;
		Quests.Report("rozsa", value);

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
		Quests.Report("koszones");
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
}
