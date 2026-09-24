using Godot;

// Kaszinós gép / asztal: ha a player a területén áll, E-re megnyílik a játék (CasinoGame).
// Az E-t a Player kezeli (NearestStation), így nem nyílik meg mellé egy NPC párbeszéde is.
// Ha több gép/asztal területén állunk egyszerre, a legközelebbi nyer.
public partial class CasinoStation : Area2D
{
	[Export]
	public string Game { get; set; } = "slots";   // slots, roulette, blackjack, dice

	private Label _hint;
	private Player _player;

	public override void _Ready()
	{
		_hint = new Label
		{
			Text = "E: " + CasinoGame.Title(Game),
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-90, -52),
			Size = new Vector2(180, 20),
			Visible = false,
			ZIndex = 10,
		};

		_hint.AddThemeFontSizeOverride("font_size", 12);
		_hint.AddThemeColorOverride("font_color", new Color(1, 1, 0));
		_hint.AddThemeColorOverride("font_outline_color", Colors.Black);
		_hint.AddThemeConstantOverride("outline_size", 4);
		AddChild(_hint);

		BodyEntered += body =>
		{
			if (body is not Player player)
				return;

			_player = player;
			player.Stations.Add(this);
		};

		BodyExited += body =>
		{
			if (body is not Player player)
				return;

			player.Stations.Remove(this);
			_player = null;
		};
	}

	public override void _Process(double delta)
	{
		_hint.Visible = _player != null && _player.NearestStation() == this;
	}

	// A terület (téglalap) legközelebbi pontjáig mért távolság - a hosszú nyerőgépsornál a közepe
	// félrevezető lenne. Ha két területen belül állunk (mindkettő 0), a középponthoz közelebbi nyer.
	public float DistanceTo(Vector2 point)
	{
		var shape = (RectangleShape2D)GetNode<CollisionShape2D>("CollisionShape2D").Shape;
		Vector2 local = ToLocal(point);
		Vector2 half = shape.Size / 2;

		return (local - local.Clamp(-half, half)).Length() + local.Length() * 0.01f;
	}
}
