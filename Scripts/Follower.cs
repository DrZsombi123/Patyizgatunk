using Godot;
using System.Collections.Generic;

// Lakatos Brendon követi Kunu Máriót: a player nyomvonalán megy, de lemaradva.
// Így nem akad el az épületekben és nem lóg a nyakunkon.
// ponytail: a scene-ben collision_layer/mask = 0, vagyis átmegy mindenen. A nyomvonal
// eleve járható, és így nem tolja el a playert az ajtókban (casino, bolt, fodrászat).
public partial class Follower : CharacterBody2D
{
	[Export]
	public Node2D Target { get; set; }

	[Export]
	public float Distance { get; set; } = 90.0f;   // ennyi marad köztünk

	[Export]
	public float Speed { get; set; } = 230.0f;

	private const float Step = 8.0f;               // ilyen sűrűn jegyezzük a nyomvonalat
	private readonly List<Vector2> _trail = new();

	public override void _PhysicsProcess(double delta)
	{
		if (Target == null)
			return;

		// ajtón át teleportált a player -> Brendon utána megy
		if (GlobalPosition.DistanceTo(Target.GlobalPosition) > 600.0f)
		{
			_trail.Clear();
			GlobalPosition = Target.GlobalPosition + new Vector2(0, 24);
			return;
		}

		if (_trail.Count == 0 || _trail[^1].DistanceTo(Target.GlobalPosition) > Step)
			_trail.Add(Target.GlobalPosition);

		Velocity = Vector2.Zero;

		// ponytail: a nyomvonal hosszát a pontok számából becsüljük (Step-enként vesszük fel)
		if (_trail.Count * Step > Distance)
		{
			if (GlobalPosition.DistanceTo(_trail[0]) < 6.0f)
				_trail.RemoveAt(0);
			else
				Velocity = GlobalPosition.DirectionTo(_trail[0]) * Speed;
		}

		MoveAndSlide();
	}
}
