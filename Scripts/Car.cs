using Godot;

// Brendon Mercedes C-osztálya: F-fel be/ki, de csak kocsikulccsal (Brendontól lehet elkérni).
// ponytail: vezetés közben a player a kocsin ül rejtve, így a kamera és a követő
// Brendon marad a helyén, nem kell külön kocsi-kamera.
public partial class Car : CharacterBody2D
{
	private const float DriverVolumeDb = 0.0f;
	private const float NearbyVolumeDb = -12.0f;

	[Export]
	public float Speed { get; set; } = 400.0f;

	[Export]
	public float TurnSpeed { get; set; } = 2.2f;

	[Export]
	public Node2D Passenger { get; set; }   // Brendon beül a jobb ülésre

	private Label _hint;
	private AudioStreamPlayer _exitSfx;
	private AudioStreamPlayer _enterSfx;
	private AudioStreamPlayer _drivingSfx;

	private Player _near;
	private Player _driver;

	// ÚJ: a sofőr részegség rendszere (csak amíg vezet)
	private DrunkSystem _drunk;

	public override void _Ready()
	{
		_hint = GetNode<Label>("Hint");
		_exitSfx = GetNode<AudioStreamPlayer>("ExitSfx");
		_enterSfx = GetNode<AudioStreamPlayer>("EnterSfx");
		_drivingSfx = GetNode<AudioStreamPlayer>("DrivingSfx");

		_enterSfx.Finished += OnEnterFinished;
		_exitSfx.Finished += OnExitFinished;

		var area = GetNode<Area2D>("Area");

		area.BodyEntered += body =>
		{
			if (body is not Player player || _driver != null)
				return;

			_near = player;
			UpdateHint();
			StartNearbyIdleIfAllowed();
		};

		area.BodyExited += body =>
		{
			if (body != _near)
				return;

			_near = null;
			UpdateHint();

			if (_driver == null)
				_drivingSfx.Stop();
		};

		UpdateHint();
		_drivingSfx.Stop();
	}

	public override void _Process(double delta)
	{
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

		if (_near != null && _near.HasCarKey)
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

		// A player maradjon a kocsin, így a kamera továbbra is őt követi.
		_driver.GlobalPosition = GlobalPosition;

		// A felirat mindig a kocsi felett legyen.
		_hint.GlobalPosition = GlobalPosition + new Vector2(-90, -65);
		_hint.Rotation = -Rotation;
	}

	private void GetIn(Player player)
	{
		_driver = player;
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

		_drivingSfx.Stop();

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

	private void OnEnterFinished()
	{
		if (_driver == null)
			return;

		_drivingSfx.VolumeDb = DriverVolumeDb;
		_drivingSfx.Play();
	}

	private void OnExitFinished()
	{
		if (_driver == null && _near != null)
			StartNearbyIdleIfAllowed();
	}

	private void StartNearbyIdleIfAllowed()
	{
		if (_driver != null ||
		    _near == null ||
		    _enterSfx.Playing ||
		    _exitSfx.Playing)
			return;

		_drivingSfx.VolumeDb = NearbyVolumeDb;

		if (!_drivingSfx.Playing)
			_drivingSfx.Play();
	}

	private void PlayOnly(AudioStreamPlayer sfx)
	{
		_enterSfx.Stop();
		_exitSfx.Stop();
		_drivingSfx.Stop();

		sfx.Play();
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