using Godot;

/// <summary>
/// Holds the drunkenness value (0-100), sobers the player up over time,
/// distorts movement input, and runs the blackout sequence at 100%.
/// Attach to a plain Node called "DrunkSystem" that is a CHILD of the Player.
/// </summary>
public partial class DrunkSystem : Node
{
	[Signal] public delegate void DrunkennessChangedEventHandler(float value);
	[Signal] public delegate void BlackoutStartedEventHandler(float fadeTime);
	[Signal] public delegate void BlackoutEndedEventHandler(float fadeTime);

	public const float MaxDrunkness = 100f;

	[ExportGroup("Thresholds (%)")]
	[Export] public float NauseaStart = 30f;     // screen wobble begins
	[Export] public float DisorientStart = 50f;  // slightly off movement
	[Export] public float ChaosStart = 70f;      // insanely random movement
	[Export] public float BlindStart = 90f;      // barely see

	[ExportGroup("Sobering")]
	[Export] public float SoberRate = 0.5f;      // % lost per second

	[ExportGroup("Blackout")]
	[Export] public float BlackoutResetValue = 30f;
	[Export] public float FadeOutTime = 1.0f;    // fade to black
	[Export] public float BlackHoldTime = 2.5f;  // fully black
	[Export] public float FadeInTime = 1.5f;     // wake up
	[Export] public string WakeUpGroup = "wakeup_points";

	public float Drunkness { get; private set; }
	public bool IsBlackedOut { get; private set; }

	private Node2D _player = null!;
	private readonly RandomNumberGenerator _rng = new();

	// movement-distortion state
	private float _time;
	private float _confusionTimer;
	private float _randomAngle;
	private bool _inverted;
	private bool _stumbling;
	private Vector2 _stumbleDir;

	public override void _Ready()
	{
		_player = GetParent<Node2D>();
	}

	public override void _Process(double delta)
	{
		if (IsBlackedOut || Drunkness <= 0f) return;
		SetDrunkness(Drunkness - SoberRate * (float)delta);
	}

	// ---------- Public API (call from Player.cs) ----------

	/// <summary>Call this from your drinking methods, e.g. AddDrunk(8f).</summary>
	public void AddDrunk(float amount)
	{
		if (IsBlackedOut) return;

		SetDrunkness(Drunkness + amount);

		if (Drunkness >= MaxDrunkness)
			StartBlackout();
	}

	/// <summary>Takes raw input and returns the (possibly messed up) direction.</summary>
	public Vector2 ModifyInput(Vector2 input, double delta)
	{
		if (IsBlackedOut) return Vector2.Zero;
		if (Drunkness < DisorientStart) return input;

		float dt = (float)delta;
		_time += dt;

		// 50-70%: slightly disoriented. Direction slowly sways left/right.
		if (Drunkness < ChaosStart)
		{
			float t = Mathf.InverseLerp(DisorientStart, ChaosStart, Drunkness);
			float maxSway = Mathf.DegToRad(Mathf.Lerp(12f, 30f, t));
			float sway = Mathf.Sin(_time * 2.2f) * maxSway;
			float speedWobble = 1f + Mathf.Sin(_time * 3.1f) * 0.1f;
			return input.Rotated(sway) * speedWobble;
		}

		// 70%+: chaos. Every fraction of a second the "rules" change.
		float chaos = Mathf.InverseLerp(ChaosStart, MaxDrunkness, Drunkness); // 0..1
		_confusionTimer -= dt;
		if (_confusionTimer <= 0f)
		{
			_confusionTimer = _rng.RandfRange(0.15f, 0.5f);
			_randomAngle = _rng.RandfRange(-Mathf.Pi, Mathf.Pi) * (0.5f + 0.5f * chaos);
			_inverted = _rng.Randf() < 0.2f + 0.3f * chaos;
			_stumbling = _rng.Randf() < 0.35f;
			_stumbleDir = Vector2.FromAngle(_rng.Randf() * Mathf.Tau);
		}

		// Stumbling: you drift even when not pressing anything.
		if (input == Vector2.Zero)
			return _stumbling ? _stumbleDir * 0.6f : Vector2.Zero;

		Vector2 result = input.Rotated(_randomAngle);
		return _inverted ? -result : result;
	}

	public string GetStatusName()
	{
		if (Drunkness < NauseaStart) return "Józan";
		if (Drunkness < DisorientStart) return "Becsiccsentve";
		if (Drunkness < ChaosStart) return "Részeg";
		if (Drunkness < BlindStart) return "Atomrészeg";
		return "Fullgatya";
	}

	// ---------- Internals ----------

	private void SetDrunkness(float value)
	{
		float clamped = Mathf.Clamp(value, 0f, MaxDrunkness);
		if (Mathf.IsEqualApprox(clamped, Drunkness)) return;

		Drunkness = clamped;
		EmitSignal(SignalName.DrunkennessChanged, Drunkness);
	}

	private SignalAwaiter Wait(float seconds) =>
		ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private async void StartBlackout()
	{
		IsBlackedOut = true;

		// 1) fade to black
		EmitSignal(SignalName.BlackoutStarted, FadeOutTime);
		await Wait(FadeOutTime);

		// 2) while the screen is black: drop the bar and move the player
		SetDrunkness(BlackoutResetValue);
		MoveToRandomWakeSpot();
		await Wait(BlackHoldTime);

		// 3) wake up
		EmitSignal(SignalName.BlackoutEnded, FadeInTime);
		await Wait(FadeInTime);

		IsBlackedOut = false;
	}

	private void MoveToRandomWakeSpot()
	{
		var spots = GetTree().GetNodesInGroup(WakeUpGroup);
		if (spots.Count == 0)
		{
			GD.PushWarning($"DrunkSystem: no nodes in group '{WakeUpGroup}'. Player stays where they are.");
			return;
		}

		var spot = (Node2D)spots[_rng.RandiRange(0, spots.Count - 1)];
		_player.GlobalPosition = spot.GlobalPosition;

		if (_player is CharacterBody2D body)
			body.Velocity = Vector2.Zero;
	}
}