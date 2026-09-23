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

	// ÚJ: részegen (50% felett) vezetve aura jár a haladásért
	[Export]
	public float DrunkDriveAuraSeconds { get; set; } = 5.0f;   // ennyi mp tényleges haladás után...

	[Export]
	public int DrunkDriveAura { get; set; } = 5;   // ...ennyi aura

	private Label _hint;
	private AudioStreamPlayer _exitSfx;
	private AudioStreamPlayer _enterSfx;
	private AudioStreamPlayer _drivingSfx;

	private float _musicPosition = 0.0f;   // itt tart a zene, innen folytatjuk

	private Player _near;
	private Player _driver;

	// ÚJ: a sofőr részegség rendszere (csak amíg vezet)
	private DrunkSystem _drunk;
	private float _drunkDriveTime = 0.0f;   // ÚJ: eddig összegyűjtött haladási idő (mp)

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
			// ÚJ: kiütve nem lehet kiszállni, majd a kiütés kirakja a sofőrt
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

		// ÚJ: részegen összezavarodik a gáz és a kormány (kiütéskor 0, a kocsi megáll)
		if (_drunk != null)
			(throttle, steer) = _drunk.ModifyDrive(throttle, steer, delta);

		// Álló helyzetben nem fordul.
		// Tolatáskor megfordul a kormányzás iránya.
		if (Mathf.Abs(throttle) > 0.1f)
			Rotation += steer * throttle * TurnSpeed * (float)delta;

		Velocity = Vector2.Up.Rotated(Rotation) * Speed * throttle;
		MoveAndSlide();

		AwardDrunkDriving((float)delta);   // ÚJ

		// A player maradjon a kocsin, így a kamera továbbra is őt követi.
		_driver.GlobalPosition = GlobalPosition;

		// A felirat mindig a kocsi felett legyen.
		_hint.GlobalPosition = GlobalPosition + new Vector2(-90, -65);
		_hint.Rotation = -Rotation;
	}

	private void GetIn(Player player)
	{
		_driver = player;
		_drunkDriveTime = 0.0f;   // ÚJ: új menet, új számláló
		_near = null;

		// ÚJ: figyeljük, ha a sofőr kiütközik vezetés közben
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
		var driver = _driver;

		ReleaseDrunkSystem();   // ÚJ

		driver.GlobalPosition =
			GlobalPosition
			+ Vector2.Right.Rotated(Rotation) * 34.0f;

		driver.SetInCar(false);

		Velocity = Vector2.Zero;

		_near = driver;
		_driver = null;

		PlayOnly(_exitSfx);

		if (Passenger != null)
			Passenger.Visible = true;

		UpdateHint();
	}

	// ÚJ: a sofőr kiütötte magát vezetés közben. A kép már fekete, a DrunkSystem mindjárt
	// elteleportálja a játékost, ezért itt engedjük el (különben visszarántanánk a kocsira).
	// A kocsi ott marad, ahol megállt.
	private void OnDriverPassedOut()
	{
		if (_driver == null)
			return;

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

	// ÚJ: 50% részegség fölött minden DrunkDriveAuraSeconds mp tényleges haladás után
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