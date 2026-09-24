using Godot;

public partial class Player : CharacterBody2D
{
	private const int VehicleLayer = 3;   // a kocsik ezen a rétegen vannak, ugrás közben átugorjuk őket

	[Export]
	public float MoveSpeed { get; set; } = 150.0f;

	[Export]
	public float JumpHeight { get; set; } = 20.0f;

	[Export]
	public float JumpDuration { get; set; } = 0.45f;

	[Export]
	public float StepRate { get; set; } = 7.0f;   // lépésütem teljes sebességnél (rad/s)

	[Export]
	public Texture2D BadHair { get; set; }

	[Export]
	public Texture2D GoodHair { get; set; }

	[Export]
	public int Money { get; set; } = 20000;

	[Signal] public delegate void AuraChangedEventHandler(int total);

	public int Aura { get; private set; } = 0;

	public bool HasCarKey { get; set; } = false;   // Brendontól lehet elkérni

	public bool InCar { get; private set; } = false;

	public bool IsBlackedOut => _drunk.IsBlackedOut;

	public UltraPatyi Secret { get; set; }   // ha a rejtett gomb mellett állunk, az E azt nyomja meg

	// kaszinós gépek/asztalok, amelyek területén állunk: E-re a legközelebbi játék nyílik meg
	public System.Collections.Generic.HashSet<CasinoStation> Stations { get; } = new();

	private Node2D _visual;
	private Sprite2D _hair;
	private Timer _hairTimer;
	private int _hairAura = 0;
	private Area2D _interactArea;
	private CollisionShape2D _bodyCollision;
	private System.Collections.Generic.Dictionary<Sprite2D, Vector2> _parts;
	private float _stepTime = 0.0f;

	// részegség rendszer (a Player gyereke: "DrunkSystem" Node)
	private DrunkSystem _drunk;

	// ponytail: egyszerre egy szer hat, az új felülírja a régit
	private Timer _effectTimer;
	private float _speedMultiplier = 1.0f;
	private float _wobble = 0.0f;
	private float _time = 0.0f;

	private bool _jumping = false;
	private float _jumpTime = 0.0f;

	private Vector2 _visualStartPosition;

	[Export]
	public int WinAura { get; set; } = 500;   // ennyi aurától nyertünk -> győzelmi oldal

	private bool _won = false;

	// mentéshez: a séró aurája csak kölcsön van (visszanő a haj), azt nem mentjük
	public int SavedAura => _hairTimer.IsStopped() ? Aura : Aura - _hairAura;

	// betöltéskor: csak beállítja, győzelmet nem vált ki
	public void RestoreAura(int value)
	{
		Aura = value;
		EmitSignal(SignalName.AuraChanged, Aura);
	}

	public void AddAura(int amount)
	{
		Aura += amount;
		EmitSignal(SignalName.AuraChanged, Aura);

		if (_won || Aura < WinAura)
			return;

		// deferred: AddAura fizikából/jelből is jöhet, ott nem cserélünk jelenetet
		_won = true;
		Callable.From(() =>
		{
			GetTree().Paused = false;   // a kaszinóban is nyerhetünk, ott áll a játék
			GetTree().ChangeSceneToFile("res://Scenes/Menu/Victory.tscn");
		}).CallDeferred();
	}


	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_interactArea = GetNode<Area2D>("InteractArea");
		_bodyCollision = GetNode<CollisionShape2D>("CollisionShape2D");

		_visualStartPosition = _visual.Position;
		_parts = Step.Parts(_visual);

		_hair = GetNode<Sprite2D>("Visual/Hair");
		_hairTimer = GetNode<Timer>("HairTimer");
		_hairTimer.Timeout += OnHairGrownBack;

		_drunk = GetNode<DrunkSystem>("DrunkSystem");

		_effectTimer = new Timer { OneShot = true };
		AddChild(_effectTimer);
		_effectTimer.Timeout += () => { _speedMultiplier = 1.0f; _wobble = 0.0f; };
	}

	public override void _Process(double delta)
	{
		// kiütés közben nem lehet beszélgetni
		if (Dialogue.IsOpen || InCar || _drunk.IsBlackedOut || Dialogue.Current == null)
			return;

		if (!Input.IsActionJustPressed("pickup"))
			return;

		// a rejtett gomb elsőbbséget kap, különben a mellettünk álló Brendonnal is dumálnánk
		if (Secret != null)
		{
			Secret.Trigger();
			return;
		}

		CasinoStation station = NearestStation();

		if (station != null && CasinoGame.Current != null)
		{
			CasinoGame.Current.Open(station.Game, this);
			return;
		}

		Npc npc = NearestNpc();

		if (npc != null)
			Dialogue.Current.Open(npc, this);
	}

	public CasinoStation NearestStation()
	{
		CasinoStation best = null;

		foreach (CasinoStation station in Stations)
			if (best == null || station.DistanceTo(GlobalPosition) < best.DistanceTo(GlobalPosition))
				best = station;

		return best;
	}

	// A legközelebbi NPC-vel beszélünk. Brendon követ minket, így mindig az InteractArea-ban van:
	// ő csak akkor szólal meg, ha rajta kívül senki sincs a közelben.
	private Npc NearestNpc()
	{
		Npc best = null;
		Npc follower = null;
		float bestDistance = float.MaxValue;

		foreach (Node2D body in _interactArea.GetOverlappingBodies())
		{
			Npc npc = body.GetNodeOrNull<Npc>("Npc");

			if (npc == null)
				continue;

			if (body is Follower)
			{
				follower = npc;
				continue;
			}

			float distance = GlobalPosition.DistanceSquaredTo(body.GlobalPosition);

			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = npc;
			}
		}

		return best ?? follower;
	}

	// Beültünk/kiszálltunk a kocsiból: a testünk ilyenkor nem mozog és nem ütközik.
	public void SetInCar(bool value)
	{
		InCar = value;
		_visual.Visible = !value;
		_bodyCollision.SetDeferred(CollisionShape2D.PropertyName.Disabled, value);
	}

	// Kiugrott valaki a kukából: megiramodunk és kapkodunk egy kicsit.
	public void Scare()
	{
		// ne vegye el a hosszabb szerhatást (patyi, energiaital)
		if (_effectTimer.TimeLeft > 2.5)
			return;

		_speedMultiplier = 1.45f;
		_wobble = 0.5f;
		_effectTimer.Start(2.5f);
	}

	// A hotbar innen süti el a tárgyat. false: nem használható (pl. kocsikulcs).
	public bool Use(string item)
	{
		if (_drunk.IsBlackedOut)   // kiütve nem lehet inni/használni
			return false;

		switch (item)
		{
			case "patyi": TakePatyi(); return true;
			case "jack": DrinkPia(15.0f); return true;        // mennyit ad a részegséghez (%)
			case "finlandia": DrinkPia(20.0f); return true;
			case "energia": DrinkEnergy(); return true;
		}

		return false;
	}

	// Kristályos por a sikátorból: felpörget.
	public void TakePatyi()
	{
		_speedMultiplier = 1.7f;
		_wobble = 0.0f;
		_effectTimer.Start(45.0f);
		AddAura(25);
	}

	// Kemény pia a pulttól (Finlandia, Jack): a részegség sáv töltődik,
	// a lassulást/kacsázást már a DrunkSystem adja.
	public void DrinkPia(float drunkAmount)
	{
		_drunk.AddDrunk(drunkAmount);
		AddAura(5);
		Quests.Report("pia");
	}

	// Energiaital a boltból: kicsit gyorsabb, de nem zavarja össze.
	public void DrinkEnergy()
	{
		_speedMultiplier = 1.25f;
		_wobble = 0.0f;
		_effectTimer.Start(30.0f);
	}

	// Ferike vág: pacek séró, "duration" mp múlva visszanő a rossz haj.
	public void GetHaircut(float duration, int aura)
	{
		if (_hairTimer.IsStopped())
		{
			_hairAura = aura;
			AddAura(aura);
		}

		_hair.Texture = GoodHair;
		_hairTimer.Start(duration);
	}

	private void OnHairGrownBack()
	{
		_hair.Texture = BadHair;
		AddAura(-_hairAura);
	}

	public override void _PhysicsProcess(double delta)
	{
		_time += (float)delta;

		// kocsiban ülünk, dumálunk vagy ki vagyunk ütve: nem mozgunk,
		// a végtagok is nyugalomba állnak
		if (InCar || Dialogue.IsOpen || _drunk.IsBlackedOut)
		{
			Velocity = Vector2.Zero;
			Step.Apply(_parts, _stepTime, 0.0f);
			return;
		}

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

		if (_wobble > 0.0f)
			direction = direction.Rotated(Mathf.Sin(_time * 4.0f) * _wobble);

		// részegségtől függően elcsúszik / összevissza megy az irány
		direction = _drunk.ModifyInput(direction, GetPhysicsProcessDeltaTime());

		Velocity = direction * MoveSpeed * _speedMultiplier;

		MoveAndSlide();

		// a lépés üteme a tényleges sebességgel skálázódik (patyitól pörgősebb)
		float pace = Velocity.Length() / MoveSpeed;

		_stepTime += (float)GetPhysicsProcessDeltaTime() * StepRate * pace;

		Step.Apply(_parts, _stepTime, pace > 0.05f ? 1.0f : 0.0f);
	}

	private void HandleJump(float delta)
	{
		if (Input.IsActionJustPressed("jump") && !_jumping)
		{
			_jumping = true;
			_jumpTime = 0.0f;
			SetCollisionMaskValue(VehicleLayer, false);
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
			SetCollisionMaskValue(VehicleLayer, true);
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