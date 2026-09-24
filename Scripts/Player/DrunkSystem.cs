using System.Collections.Generic;
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
	[Signal] public delegate void PassedOutEventHandler();   // a kép teljesen fekete, mindjárt jön a teleport

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

	[ExportGroup("Vomit")]
	[Export] public Texture2D VomitTexture { get; set; }
	[Export] public Vector2 VomitOffset { get; set; } = new Vector2(0f, 6f);   // + Y = lejjebb (a láb alá)
	[Export] public float VomitScale { get; set; } = 1.0f;
	[Export] public int MaxPuddles { get; set; } = 20;   // 0 = korlátlan, a legrégebbi tűnik el először
	[Export] public float VomitSortLift { get; set; } = 16.0f;   // ennyivel a tócsa teteje FÖLÉ kerül a rendezési pont (y-sort)

	public float Drunkness { get; private set; }
	public bool IsBlackedOut { get; private set; }

	private Node2D _player = null!;
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Sprite2D> _puddles = new();

	// movement-distortion state
	private float _time;
	private float _confusionTimer;
	private float _randomAngle;
	private bool _inverted;
	private bool _stumbling;
	private Vector2 _stumbleDir;

	// driving-distortion state (kocsi)
	private float _driveTimer;
	private float _driveSteerOffset;
	private float _driveThrottleScale = 1f;
	private bool _driveSteerInverted;

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

	/// <summary>Kocsihoz: gáz (-1..1) és kormány (-1..1) bemenetet zavar össze.</summary>
	public (float throttle, float steer) ModifyDrive(float throttle, float steer, double delta)
	{
		if (IsBlackedOut) return (0f, 0f);
		if (Drunkness < DisorientStart) return (throttle, steer);

		float dt = (float)delta;
		_time += dt;

		// 50-70%: kicsit kanyarog, a kormány magától jobbra-balra ring.
		if (Drunkness < ChaosStart)
		{
			float t = Mathf.InverseLerp(DisorientStart, ChaosStart, Drunkness);
			float sway = Mathf.Sin(_time * 1.9f) * Mathf.Lerp(0.15f, 0.45f, t);
			float surge = 1f + Mathf.Sin(_time * 3.1f) * 0.08f;
			return (throttle * surge, Mathf.Clamp(steer + sway, -1f, 1f));
		}

		// 70%+: kaotikus. Rángatja a kormányt, néha fordítva működik, a gáz hullámzik.
		float chaos = Mathf.InverseLerp(ChaosStart, MaxDrunkness, Drunkness); // 0..1
		_driveTimer -= dt;
		if (_driveTimer <= 0f)
		{
			_driveTimer = _rng.RandfRange(0.2f, 0.6f);
			_driveSteerOffset = _rng.RandfRange(-1f, 1f) * (0.6f + 0.4f * chaos);
			_driveSteerInverted = _rng.Randf() < 0.2f + 0.3f * chaos;
			_driveThrottleScale = _rng.RandfRange(0.4f, 1.0f);
		}

		float s = _driveSteerInverted ? -steer : steer;
		return (throttle * _driveThrottleScale, Mathf.Clamp(s + _driveSteerOffset, -1f, 1f));
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

		// 2) while the screen is black: drop the bar, move the player, leave a present
		EmitSignal(SignalName.PassedOut);   // ha kocsiban ült, a Car itt engedi el a játékost
		SetDrunkness(BlackoutResetValue);
		MoveToRandomWakeSpot();
		SpawnVomit();
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

	// Tócsa a földön, pont ahol felkelünk. Csak látvány, nincs ütközés, nem hat semmire.
	private void SpawnVomit()
	{
		if (VomitTexture == null) return;

		Node parent = _player.GetParent();
		if (parent == null) return;

		float s = Mathf.Max(VomitScale, 0.01f);

		// Y-sortnál az számít, ki van "lejjebb". Hogy a tócsa MINDIG a földön legyen és
		// mindenki rálépjen, a node-ot a tócsa teteje fölé tesszük (ez a rendezési pont),
		// a textúrát pedig az Offsettel visszatoljuk oda, ahol látszania kell.
		float lift = VomitTexture.GetHeight() * s * 0.5f + VomitSortLift;

		var puddle = new Sprite2D
		{
			Texture = VomitTexture,
			Offset = new Vector2(VomitOffset.X, lift / s),
			Scale = new Vector2(s, s),
			FlipH = _rng.Randf() < 0.5f
		};

		// Ugyanabba a szülőbe kerül, mint a játékos, és közvetlenül elé a sorrendben
		// (y-sort nélkül is a játékos rajzolódik rá a tócsára).
		parent.AddChild(puddle);
		parent.MoveChild(puddle, _player.GetIndex());

		Vector2 basePos = _player.GlobalPosition;
		puddle.GlobalPosition = new Vector2(basePos.X, basePos.Y + VomitOffset.Y * s - lift);

		_puddles.Add(puddle);

		if (MaxPuddles > 0 && _puddles.Count > MaxPuddles)
		{
			Sprite2D oldest = _puddles[0];
			_puddles.RemoveAt(0);

			if (IsInstanceValid(oldest))
				oldest.QueueFree();
		}
	}
}