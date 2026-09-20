using Godot;

// Brendon Mercedes C-osztálya: F-fel be/ki, de csak kocsikulccsal (Brendontól lehet elkérni).
// ponytail: vezetés közben a player a kocsin ül rejtve, így a kamera és a követő
// Brendon marad a helyén, nem kell külön kocsi-kamera.
public partial class Car : CharacterBody2D
{
	[Export]
	public float Speed { get; set; } = 400.0f;

	[Export]
	public float TurnSpeed { get; set; } = 2.2f;

	[Export]
	public Node2D Passenger { get; set; }   // Brendon beül a jobb ülésre

	private Label _hint;
	private AudioStreamPlayer _exitSfx;
	private AudioStreamPlayer _enterSfx;
	private Player _near;
	private Player _driver;

	public override void _Ready()
	{
		_hint = GetNode<Label>("Hint");
		_exitSfx = GetNode<AudioStreamPlayer>("ExitSfx");
		_enterSfx = GetNode<AudioStreamPlayer>("EnterSfx");

		var area = GetNode<Area2D>("Area");
		area.BodyEntered += body => { if (body is Player player) { _near = player; UpdateHint(); } };
		area.BodyExited += body => { if (body == _near) { _near = null; UpdateHint(); } };

		UpdateHint();
	}

	public override void _Process(double delta)
	{
		if (Dialogue.IsOpen || !Input.IsActionJustPressed("vehicle"))
			return;

		if (_driver != null)
			GetOut();
		else if (_near != null && _near.HasCarKey)
			GetIn(_near);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_driver == null)
			return;

		float throttle = Input.GetAxis("move_down", "move_up");
		float steer = Input.GetAxis("move_left", "move_right");

		// állva nem fordul, tolatásnál fordítva húz - mint egy igazi tragacs
		if (Mathf.Abs(throttle) > 0.1f)
			Rotation += steer * throttle * TurnSpeed * (float)delta;

		Velocity = Vector2.Up.Rotated(Rotation) * Speed * throttle;
		MoveAndSlide();

		_driver.GlobalPosition = GlobalPosition;
		_hint.Rotation = -Rotation;
	}

	private void GetIn(Player player)
	{
		_driver = player;
		_near = null;

		player.SetInCar(true);
		PlayOnly(_enterSfx);

		// Brendon is beszáll: a kocsi mögött jön tovább, csak nem látszik
		if (Passenger != null)
			Passenger.Visible = false;

		UpdateHint();
	}

	private void GetOut()
	{
		_driver.GlobalPosition = GlobalPosition + Vector2.Right.Rotated(Rotation) * 34.0f;
		_driver.SetInCar(false);
		PlayOnly(_exitSfx);

		if (Passenger != null)
			Passenger.Visible = true;

		_near = _driver;
		_driver = null;
		UpdateHint();
	}

	// ponytail: egyszerre csak az egyik szóljon, kulonben osszemegy a be- es kiszallas hangja
	private void PlayOnly(AudioStreamPlayer sfx)
	{
		_enterSfx.Stop();
		_exitSfx.Stop();
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
