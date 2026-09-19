using Godot;

public partial class Player : CharacterBody2D
{
	[Export]
	public float MoveSpeed { get; set; } = 220.0f;

	[Export]
	public float JumpHeight { get; set; } = 20.0f;

	[Export]
	public float JumpDuration { get; set; } = 0.45f;

	[Export]
	public Texture2D BadHair { get; set; }

	[Export]
	public Texture2D GoodHair { get; set; }

	private Node2D _visual;
	private Sprite2D _hair;
	private Timer _hairTimer;
	private int _hairAura = 0;
	private Area2D _interactArea;
	private CollisionShape2D _bodyCollision;

	private bool _jumping = false;
	private float _jumpTime = 0.0f;

	private Vector2 _visualStartPosition;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_interactArea = GetNode<Area2D>("InteractArea");
		_bodyCollision = GetNode<CollisionShape2D>("CollisionShape2D");

		_visualStartPosition = _visual.Position;

		_hair = GetNode<Sprite2D>("Visual/Hair");
		_hairTimer = GetNode<Timer>("HairTimer");
		_hairTimer.Timeout += OnHairGrownBack;
	}

	// Ferike vág: pacek séró, "duration" mp múlva visszanő a rossz haj.
	public void GetHaircut(float duration, int aura)
	{
		if (_hairTimer.IsStopped())
		{
			_hairAura = aura;
			GD.Print($"Aura +{aura}"); // TODO: Statisztika (Gergő) aura
		}

		_hair.Texture = GoodHair;
		_hairTimer.Start(duration);
	}

	private void OnHairGrownBack()
	{
		_hair.Texture = BadHair;
		GD.Print($"Aura -{_hairAura}"); // TODO: Statisztika (Gergő) aura
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleMovement();
		HandleJump((float)delta);
	}

	private void HandleMovement()
	{
		Vector2 direction = Input.GetVector(
			"move_left",
			"move_right",
			"move_up",
            "move_down"
		);

		Velocity = direction * MoveSpeed;

		MoveAndSlide();
	}

	private void HandleJump(float delta)
	{
		if (Input.IsActionJustPressed("jump") && !_jumping)
		{
			_jumping = true;
			_jumpTime = 0.0f;
		}

		if (!_jumping)
			return;

		_jumpTime += delta;

		float progress = _jumpTime / JumpDuration;

		if (progress >= 1.0f)
		{
			_jumping = false;
			_jumpTime = 0.0f;
			_visual.Position = _visualStartPosition;
			return;
		}

		float height =
			4.0f *
			JumpHeight *
			progress *
			(1.0f - progress);

		_visual.Position = new Vector2(
			_visualStartPosition.X,
			_visualStartPosition.Y - height
		);
	}
}
