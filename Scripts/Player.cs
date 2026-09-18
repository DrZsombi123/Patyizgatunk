using Godot;

public partial class Player : CharacterBody2D
{
	[Export]
	public float MoveSpeed { get; set; } = 220.0f;

	[Export]
	public float JumpHeight { get; set; } = 20.0f;

	[Export]
	public float JumpDuration { get; set; } = 0.45f;

	private Node2D _visual;
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
