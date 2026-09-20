using Godot;
using System.Collections.Generic;

// A szülő NPC helyben táncol: ugyanaz a lépés, csak megállás nélkül és nagyobb kilengéssel.
public partial class Dancer : Node2D
{
	[Export]
	public float Tempo { get; set; } = 9.0f;    // rad/s, ~130 BPM

	[Export]
	public float Amount { get; set; } = 1.6f;

	private Dictionary<Sprite2D, Vector2> _parts;
	private float _time;

	public override void _Ready()
	{
		_parts = Step.Parts(GetParent());
		_time = GD.Randf() * Mathf.Tau;   // ne egyszerre ugráljanak
	}

	public override void _Process(double delta)
	{
		_time += (float)delta * Tempo;

		Step.Apply(_parts, _time, Amount);
	}
}
