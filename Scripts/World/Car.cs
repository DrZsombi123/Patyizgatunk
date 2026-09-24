using Godot;

// Brendon Mercedes C-osztálya: F-fel be/ki, de csak kocsikulccsal (Brendontól lehet elkérni).
// ponytail: vezetés közben a player a kocsin ül rejtve, így a kamera és a követő
// Brendon marad a helyén, nem kell külön kocsi-kamera.
public partial class Car : CharacterBody2D
{
	private const float DriverVolumeDb = 0.0f;
	private const float NearbyVolumeDb = -6.0f;   // közvetlenül a kocsi mellett, innen halkul a távolsággal
	private const float DuckDb = -18.0f;          // be/kiszállás hangja alatt ennyivel halkabb (mint a Ducker)
	private const float FadeSpeed = 8.0f;         // mennyire gyorsan úszik át a hangerő

	[Export]
	public float Speed { get; set; } = 250.0f;   // lassabb, hogy nagyobbnak tűnjön a map

	[Export]
	public float TurnSpeed { get; set; } = 2.2f;

	[Export]
	public Node2D Passenger { get; set; }   // Brendon beül a jobb ülésre

	[Export]
	public Player Listener { get; set; }   // a zene hangereje az ő távolságától függ

	[Export]
	public float MusicRange { get; set; } = 160.0f;   // ennyi px-ről már hallatszik a zene

	// részegen (50% felett) vezetve aura jár a haladásért
	[Export]
	public float DrunkDriveAuraSeconds { get; set; } = 5.0f;   // ennyi mp tényleges haladás után...

	[Export]
	public int DrunkDriveAura { get; set; } = 5;   // ...ennyi aura

	// Rendőrség: részegen vezetve PoliceCheckSeconds mp-enként dobunk (minél részegebb, annál nagyobb eséllyel).
	// Ha jönnek, ChaseSeconds mp-ig nem szabad megállni és kiszállni. Megúszva aura jár,
	// elkapva bírság, auravesztés, és lefoglalják a kulcsot (Brendontól újra el kell kérni).
	[Export]
	public float PoliceCheckSeconds { get; set; } = 10.0f;

	[Export]
	public float PoliceChance { get; set; } = 0.4f;   // esély teljes részegségnél, 50%-nál a fele

	[Export]
	public float ChaseSeconds { get; set; } = 8.0f;

	[Export]
	public float StopLimit { get; set; } = 1.5f;   // üldözés közben ennyi mp állás után elkapnak

	[Export]
	public int Fine { get; set; } = 30000;

	[Export]
	public int BustedAura { get; set; } = 40;

	[Export]
	public int EscapeAura { get; set; } = 20;

	private static readonly Color Red = new Color(1, 0.3f, 0.3f);
	private static readonly Color Yellow = new Color(1, 1, 0);

	public Vector2 ExitPosition => GlobalPosition + Vector2.Right.Rotated(Rotation) * 34.0f;

	private readonly RandomNumberGenerator _rng = new();
	private float _policeTime = 0.0f;
	private float _chase = 0.0f;     // hátralévő üldözés (mp), 0 = nincs
	private float _stopped = 0.0f;   // üldözés közben ennyi mp-e állunk
	private float _driveTime = 0.0f; // küldetéshez: haladás, másodpercenként jelentjük
	private ColorRect _siren;
	private Tween _sirenTween;

	private Label _hint;
	private AudioStreamPlayer _exitSfx;
	private AudioStreamPlayer _enterSfx;
	private AudioStreamPlayer _drivingSfx;

	private float _musicPosition = 0.0f;   // itt tart a zene, innen folytatjuk

	private Player _near;
	private Player _driver;

	// a sofőr részegség rendszere (csak amíg vezet)
	private DrunkSystem _drunk;
	private float _drunkDriveTime = 0.0f;   // eddig összegyűjtött haladási idő (mp)

	public override void _Ready()
	{
		_hint = GetNode<Label>("Hint");
		_exitSfx = GetNode<AudioStreamPlayer>("ExitSfx");
		_enterSfx = GetNode<AudioStreamPlayer>("EnterSfx");
		_drivingSfx = GetNode<AudioStreamPlayer>("DrivingSfx");

		var area = GetNode<Area2D>("Area");

		area.BodyEntered += body =>
		{
			if (body is not Player player || _driver != null)
				return;

			_near = player;
			UpdateHint();
		};

		area.BodyExited += body =>
		{
			if (body != _near)
				return;

			_near = null;
			UpdateHint();
		};

		UpdateHint();
		_drivingSfx.Stop();

		// villogó piros-kék a képernyőn üldözés közben
		var layer = new CanvasLayer { Layer = 8 };
		AddChild(layer);
		_siren = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_siren.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(_siren);
	}

	public override void _Process(double delta)
	{
		// a kulcsot a kocsi mellett állva is megkaphatjuk (Brendontól) -> a felirat azonnal kövesse
		if (_driver == null && _near != null)
			UpdateHint();

		UpdateMusic((float)delta);

		if (Dialogue.IsOpen || !Input.IsActionJustPressed("vehicle"))
			return;

		if (_driver != null)
		{
			// kiütve nem lehet kiszállni, majd a kiütés kirakja a sofőrt
			if (_drunk != null && _drunk.IsBlackedOut)
				return;

			GetOut();
			return;
		}

		if (_near != null && _near.HasCarKey && !_near.IsBlackedOut)
			GetIn(_near);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_driver == null)
			return;

		float throttle = Input.GetAxis("move_down", "move_up");
		float steer = Input.GetAxis("move_left", "move_right");

		// részegen összezavarodik a gáz és a kormány (kiütéskor 0, a kocsi megáll)
		if (_drunk != null)
			(throttle, steer) = _drunk.ModifyDrive(throttle, steer, delta);

		// Álló helyzetben nem fordul.
		// Tolatáskor megfordul a kormányzás iránya.
		if (Mathf.Abs(throttle) > 0.1f)
			Rotation += steer * throttle * TurnSpeed * (float)delta;

		Velocity = Vector2.Up.Rotated(Rotation) * Speed * throttle;
		MoveAndSlide();

		AwardDrunkDriving((float)delta);
		ReportDriving((float)delta);
		UpdatePolice((float)delta);

		// az üldözés vége kiszállíthatott minket
		if (_driver == null)
			return;

		// A player maradjon a kocsin, így a kamera továbbra is őt követi.
		_driver.GlobalPosition = GlobalPosition;

		// A felirat mindig a kocsi felett legyen.
		_hint.GlobalPosition = GlobalPosition + new Vector2(-90, -65);
		_hint.Rotation = -Rotation;
	}

	private void GetIn(Player player)
	{
		_driver = player;
		_drunkDriveTime = 0.0f;   // új menet, új számláló
		_near = null;

		// figyeljük, ha a sofőr kiütközik vezetés közben
		_drunk = player.GetNodeOrNull<DrunkSystem>("DrunkSystem");
		if (_drunk != null)
			_drunk.PassedOut += OnDriverPassedOut;

		player.SetInCar(true);

		PlayOnly(_enterSfx);

		// Brendon is beszáll: a kocsi mögött jön tovább, csak nem látszik
		if (Passenger != null)
			Passenger.Visible = false;

		UpdateHint();
	}

	private void GetOut()
	{
		// üldözés közben kiszállni = feladjuk magunkat
		if (_chase > 0.0f)
			Busted();

		var driver = _driver;

		ReleaseDrunkSystem();

		driver.GlobalPosition = ExitPosition;

		driver.SetInCar(false);

		Velocity = Vector2.Zero;

		_near = driver;
		_driver = null;

		PlayOnly(_exitSfx);

		if (Passenger != null)
			Passenger.Visible = true;

		UpdateHint();
	}

	// a sofőr kiütötte magát vezetés közben. A kép már fekete, a DrunkSystem mindjárt
	// elteleportálja a játékost, ezért itt engedjük el (különben visszarántanánk a kocsira).
	// A kocsi ott marad, ahol megállt.
	private void OnDriverPassedOut()
	{
		if (_driver == null)
			return;

		if (_chase > 0.0f)
			Busted();

		var driver = _driver;

		ReleaseDrunkSystem();

		driver.SetInCar(false);

		Velocity = Vector2.Zero;

		_near = null;
		_driver = null;

		PauseMusic();

		if (Passenger != null)
			Passenger.Visible = true;

		UpdateHint();
	}

	private void ReleaseDrunkSystem()
	{
		if (_drunk != null)
			_drunk.PassedOut -= OnDriverPassedOut;

		_drunk = null;
	}

	// 50% részegség fölött minden DrunkDriveAuraSeconds mp tényleges haladás után
	// DrunkDriveAura pont jár. Állva (vagy falnak tolatva) nem gyűlik az idő.
	private void AwardDrunkDriving(float delta)
	{
		if (_drunk == null || _drunk.IsBlackedOut || _drunk.Drunkness < _drunk.DisorientStart)
			return;

		if (GetRealVelocity().Length() < 10.0f)
			return;

		float interval = Mathf.Max(DrunkDriveAuraSeconds, 0.1f);
		_drunkDriveTime += delta;

		while (_drunkDriveTime >= interval)
		{
			_drunkDriveTime -= interval;
			_driver.AddAura(DrunkDriveAura);
		}
	}

	// küldetéshez: minden tényleges haladással töltött másodperc számít
	private void ReportDriving(float delta)
	{
		if (GetRealVelocity().Length() < 10.0f)
			return;

		_driveTime += delta;

		if (_driveTime >= 1.0f)
		{
			_driveTime -= 1.0f;
			Quests.Report("vezetes");
		}
	}

	private void UpdatePolice(float delta)
	{
		if (_chase > 0.0f)
		{
			_chase -= delta;
			_stopped = GetRealVelocity().Length() < 40.0f ? _stopped + delta : 0.0f;
			_hint.Text = $"🚨 Menekülj! {Mathf.CeilToInt(_chase)}";

			if (_stopped >= StopLimit)
				GetOut();   // megálltunk: elkapnak, a GetOut intézi
			else if (_chase <= 0.0f)
				Escaped();

			return;
		}

		if (_drunk == null || _drunk.IsBlackedOut || _drunk.Drunkness < _drunk.DisorientStart)
		{
			_policeTime = 0.0f;
			return;
		}

		_policeTime += delta;

		if (_policeTime < PoliceCheckSeconds)
			return;

		_policeTime = 0.0f;

		float drunkness = Mathf.InverseLerp(_drunk.DisorientStart, DrunkSystem.MaxDrunkness, _drunk.Drunkness);

		if (_rng.Randf() < PoliceChance * (0.5f + 0.5f * drunkness))
			StartChase();
	}

	private void StartChase()
	{
		_chase = ChaseSeconds;
		_stopped = 0.0f;

		Quests.Toast("🚨 RENDŐRSÉG! Ne állj meg és ne szállj ki!", Red);

		_siren.Visible = true;
		_sirenTween?.Kill();
		_sirenTween = CreateTween().SetLoops();
		_sirenTween.TweenProperty(_siren, "color", new Color(1, 0, 0, 0.18f), 0.25f);
		_sirenTween.TweenProperty(_siren, "color", new Color(0, 0.3f, 1, 0.18f), 0.25f);
	}

	private void EndChase()
	{
		_chase = 0.0f;
		_sirenTween?.Kill();
		_siren.Visible = false;
		UpdateHint();
	}

	private void Escaped()
	{
		EndChase();
		_driver.AddAura(EscapeAura);
		Quests.Toast($"Leráztad a zsarukat!  +{EscapeAura} AURA", Yellow);
		Quests.Report("menekules");
	}

	// Elkaptak: bírság (amennyi van), auravesztés, a kulcs lefoglalva. A sofőr még a kocsiban ül,
	// a kiszállást a hívó intézi.
	private void Busted()
	{
		EndChase();

		int fine = Mathf.Min(Fine, _driver.Money);
		_driver.Money -= fine;
		_driver.AddAura(-BustedAura);
		_driver.HasCarKey = false;
		Hotbar.Current?.Remove("kulcs");

		Quests.Toast($"Elkaptak! -{fine} Ft, -{BustedAura} AURA, a kulcsot lefoglalták", Red);
	}

	// Vezetés közben teljes hangerő. Kívülről MusicRange-en belül szól (csak ha nálunk a kulcs),
	// és minél közelebb állunk, annál hangosabb. Be/kiszállás hangja alatt nem áll le, csak
	// lehalkul, mint a klubban a Finlandiánál.
	private void UpdateMusic(float delta)
	{
		float target = DriverVolumeDb;

		if (_driver == null)
		{
			float distance = Listener == null ? float.MaxValue : GlobalPosition.DistanceTo(Listener.GlobalPosition);

			if (distance >= MusicRange || !Listener.HasCarKey)
			{
				PauseMusic();
				return;
			}

			target = NearbyVolumeDb + Mathf.LinearToDb(1.0f - distance / MusicRange);
		}

		if (_enterSfx.Playing || _exitSfx.Playing)
			target += DuckDb;

		// -inf dB-vel a Lerp NaN-t adna
		target = Mathf.Max(target, -60.0f);

		if (!_drivingSfx.Playing)
		{
			_drivingSfx.VolumeDb = target;
			_drivingSfx.Play(_musicPosition);
			return;
		}

		_drivingSfx.VolumeDb = Mathf.Lerp(_drivingSfx.VolumeDb, target, Mathf.Min(1.0f, delta * FadeSpeed));
	}

	private void PlayOnly(AudioStreamPlayer sfx)
	{
		_enterSfx.Stop();
		_exitSfx.Stop();

		sfx.Play();
	}

	// ponytail: Stop + elmentett pozíció, nem StreamPaused - úgy a Playing is egyértelmű marad
	private void PauseMusic()
	{
		if (!_drivingSfx.Playing)
			return;

		_musicPosition = _drivingSfx.GetPlaybackPosition();
		_drivingSfx.Stop();
	}

	private void UpdateHint()
	{
		_hint.Visible = _near != null || _driver != null;
		_hint.Rotation = -Rotation;   // a felirat maradjon vízszintes

		if (_driver != null)
			_hint.Text = "F: kiszállás";
		else if (_near != null)
			_hint.Text = _near.HasCarKey ? "F: beszállás" : "Kell hozzá a kocsikulcs";
	}
}